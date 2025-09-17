using System;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
class SharableSpreadSheet
{
    // Sheet parameters
    private int nRows; // Number of rows in the spreadsheet
    private int nCols; // Number of columns in the spreadsheet
    private int nUsers; // Maximum number of concurrent users (-1 for unlimited)

    // Data
    public static List<List<String>> _spreadsheet; // The actual spreadsheet data, represented as a list of rows, each row is a list of strings

    // Locks and synchronization
    private SemaphoreSlim _userSemaphore; // Semaphore to limit the number of concurrent users
    private ReaderWriterLockSlim _sizeLock; // Lock to manage resizing operations (lock all sheet)

    public List<ChunkManager> _chunkManagers; // List of chunk managers to handle different parts of the spreadsheet concurrently

    /// <summary>
    /// Initializes a new instance of the <see cref="SharableSpreadSheet"/> class.
    /// </summary>
    /// <param name="nRows">Number of rows in the spreadsheet.</param>
    /// <param name="nCols">Number of columns in the spreadsheet.</param>
    /// <param name="nUsers">Maximum number of concurrent users (-1 for unlimited).</param>
    /// <exception cref="ArgumentException">Thrown if invalid arguments are provided.</exception>
    public SharableSpreadSheet(int nRows, int nCols, int nUsers = -1)
    {
        // Check validation for nRows and nCols
        if (nRows <= 0 || nCols <= 0)
        {
            throw new ArgumentException("Constructor: Number of rows and columns must be greater than zero.");
        }
        // Check validation for nUsers
        if (nUsers > 0)
        {
            _userSemaphore = new SemaphoreSlim(nUsers, nUsers);
        }
        else if (nUsers == -1)
        {
            _userSemaphore = null;
        }
        if (nUsers < -1 || nUsers == 0)
        {
            throw new ArgumentException("Constructor: Number of users must be -1 or greater than zero.");
        }

        // nUsers used for setConcurrentSearchLimit, -1 mean no limit.
        // construct a nRows*nCols spreadsheet
        this.nRows = nRows;
        this.nCols = nCols;
        this.nUsers = nUsers;

        _spreadsheet = new List<List<string>>(nRows);
        for (int i = 0; i < nRows; i++)
        {
            var row = new List<string>();

            for (int j = 0; j < nCols; j++)
                row.Add("");

            _spreadsheet.Add(row);
        }

        this._chunkManagers = this.GetChuncksList(nRows, nCols, Environment.ProcessorCount);

        // Initialize the semaphore for user access if nUsers is specified
        _sizeLock = new ReaderWriterLockSlim();
    }

    /// <summary>
    /// Gets the value of the cell at the specified row and column.
    /// </summary>
    /// <param name="row">Row index.</param>
    /// <param name="col">Column index.</param>
    /// <returns>The string value at the specified cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of range.</exception>
    public String getCell(int row, int col)
    {
        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();
        // return the string at [row,col]
        if (_userSemaphore != null)
            _userSemaphore.Wait();

        String result;

        try
        {
            int chunckNumber = getManagerRangeIndex(row, col);
            ChunkManager chunck = _chunkManagers[chunckNumber];

            result = chunck.readIndex(row, col);
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }

        return result;
    }

    /// <summary>
    /// Sets the value of the cell at the specified row and column.
    /// </summary>
    /// <param name="row">Row index.</param>
    /// <param name="col">Column index.</param>
    /// <param name="str">The value to set.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of range.</exception>
    public void setCell(int row, int col, String str)
    {
        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();
        // set the string at [row,col]
        if (_userSemaphore != null)
            _userSemaphore.Wait();

        try
        {
            int chunckNumber = getManagerRangeIndex(row, col);
            ChunkManager chunck = _chunkManagers[chunckNumber];

            chunck.writeIndex(row, col, str);
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Searches for the first cell containing the specified string.
    /// </summary>
    /// <param name="str">The string to search for.</param>
    /// <returns>
    /// A tuple (row, col) of the first cell containing the string, or null if not found.
    /// </returns>
    public Tuple<int, int> searchString(String str)
    {
        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();

        if (_userSemaphore != null)
            _userSemaphore.Wait();

        try
        {
            // perform search and return first cell indexes that contains the string
            List<Task<Tuple<int, int>>> tasks = new List<Task<Tuple<int, int>>>();

            for (int i = 0; i < this._chunkManagers.Count; i++)
            {
                int chunkIndex = i;
                // Start the thread
                tasks.Add(Task.Run(() => _chunkManagers[chunkIndex].searchStringInRange(str)));
            }
            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());

            // Check results from all tasks
            foreach (var task in tasks)
            {
                if (task.Result != null)
                    return task.Result;
            }
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }
        // return first cell indexes that contains the string (search from first row to the last row)
        return null;
    }

    /// <summary>
    /// Exchanges the contents of two rows.
    /// </summary>
    /// <param name="row1">The first row index.</param>
    /// <param name="row2">The second row index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of range.</exception>
    public void exchangeRows(int row1, int row2)
    {
        if (row1 < 0 || row2 < 0 || row1 >= nRows || row2 >= nRows)
            throw new ArgumentOutOfRangeException("exchangeRows: Row index is out of range.");

        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();
        try
        {
            // exchange the content of row1 and row2
            if (_userSemaphore != null)
                _userSemaphore.Wait();
            try
            {
                // Find chunks that contain row1 and row2
                ChunkManager chunk1 = null;
                ChunkManager chunk2 = null;

                foreach (ChunkManager chunk in _chunkManagers)
                {
                    if (row1 >= chunk.start_row && row1 <= chunk.end_row)
                        chunk1 = chunk;
                    if (row2 >= chunk.start_row && row2 <= chunk.end_row)
                        chunk2 = chunk;
                }

                // If we can't find both rows, exit
                if (chunk1 == null || chunk2 == null)
                    return;

                // Get the full column range of the spreadsheet
                int minCol = _chunkManagers.Min(c => c.start_col);
                int maxCol = _chunkManagers.Max(c => c.end_col);

                // Exchange data column by column across all chunks
                for (int col = minCol; col <= maxCol; col++)
                {
                    // Find which chunks contain this column
                    ChunkManager colChunk1 = null;
                    ChunkManager colChunk2 = null;

                    foreach (ChunkManager chunk in _chunkManagers)
                    {
                        if (col >= chunk.start_col && col <= chunk.end_col)
                        {
                            if (row1 >= chunk.start_row && row1 <= chunk.end_row)
                                colChunk1 = chunk;
                            if (row2 >= chunk.start_row && row2 <= chunk.end_row)
                                colChunk2 = chunk;
                        }
                    }

                    // If both cells exist, exchange them
                    if (colChunk1 != null && colChunk2 != null)
                    {
                        String temp = colChunk1.readIndex(row1, col);
                        String value2 = colChunk2.readIndex(row2, col);
                        colChunk1.writeIndex(row1, col, value2);
                        colChunk2.writeIndex(row2, col, temp);
                    }
                }
            }
            finally
            {
                if (_userSemaphore != null)
                    _userSemaphore.Release();
            }
        }
        finally
        {
            // Release resize Lock
            _sizeLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Exchanges the contents of two columns.
    /// </summary>
    /// <param name="col1">The first column index.</param>
    /// <param name="col2">The second column index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of range.</exception>
    public void exchangeCols(int col1, int col2)
    {
        if (col1 < 0 || col2 < 0 || col1 >= nRows || col2 >= nRows)
            throw new ArgumentOutOfRangeException("exchangeCols: Col index is out of range.");

        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();
        try
        {
            // exchange the content of col1 and col2
            if (_userSemaphore != null)
                _userSemaphore.Wait();
            try
            {
                // Data of col 1
                List<Task<List<String>>> tasks1 = new List<Task<List<String>>>();
                for (int i = 0; i < this._chunkManagers.Count(); i++)
                {
                    int chunckIndex = i;
                    tasks1.Add(Task.Run(() => _chunkManagers[chunckIndex].getColData(col1)));
                }
                Task.WaitAll(tasks1.ToArray());

                // Data of col 2
                List<Task<List<String>>> tasks2 = new List<Task<List<String>>>();
                for (int i = 0; i < this._chunkManagers.Count(); i++)
                {
                    int chunckIndex = i;
                    tasks2.Add(Task.Run(() => _chunkManagers[chunckIndex].getColData(col2)));
                }
                Task.WaitAll(tasks2.ToArray());

                // Exchange data
                for (int i = 0; i < this._chunkManagers.Count(); i++)
                {
                    ChunkManager chunk = _chunkManagers[i];
                    List<String> colData1 = tasks1[i].Result;
                    List<String> colData2 = tasks2[i].Result;

                    // Skip chunks that don't contain the specified columns
                    if (colData1 == null || colData2 == null)
                        continue;

                    // Calculate the correct row indices for the data arrays
                    for (int row = chunk.start_row; row <= chunk.end_row; row++)
                    {
                        int dataIndex = row - chunk.start_row; // Convert absolute row to relative index
                        String temp = colData1[dataIndex];
                        chunk.writeIndex(row, col1, colData2[dataIndex]);
                        chunk.writeIndex(row, col2, temp);
                    }
                }
            }
            finally
            {
                if (_userSemaphore != null)
                    _userSemaphore.Release();
            }
        }
        finally
        {
            // Release resize Lock
            _sizeLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Searches for the specified string in a given row.
    /// </summary>
    /// <param name="row">Row index.</param>
    /// <param name="str">The string to search for.</param>
    /// <returns>The column index if found, otherwise -1.</returns>
    public int searchInRow(int row, String str)
    {
        if (row < 0 || row >= nRows)
            throw new ArgumentOutOfRangeException("searchInRow: Row index is out of range.");

        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();

        if (_userSemaphore != null)
            _userSemaphore.Wait();

        try
        {
            // Data of row
            List<Task<int>> tasks = new List<Task<int>>();
            for (int i = 0; i < this._chunkManagers.Count(); i++)
            {
                int chunckIndex = i;
                tasks.Add(Task.Run(() => _chunkManagers[chunckIndex].searchInRow(row, str)));
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var task in tasks)
            {
                if (task.Result != -1)
                {
                    return task.Result; // Return the first found column index
                }
            }
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }
        // If no match found, return -1
        return -1;
    }

    /// <summary>
    /// Searches for the specified string in a given column.
    /// </summary>
    /// <param name="col">Column index.</param>
    /// <param name="str">The string to search for.</param>
    /// <returns>The row index if found, otherwise -1.</returns>
    public int searchInCol(int col, String str)
    {
        if (col < 0 || col >= nCols)
            throw new ArgumentOutOfRangeException("searchInCol: Col index is out of range.");

        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();

        if (_userSemaphore != null)
            _userSemaphore.Wait();
        try
        {
            // Data of col
            List<Task<int>> tasks = new List<Task<int>>();
            for (int i = 0; i < this._chunkManagers.Count(); i++)
            {
                int chunckIndex = i;
                tasks.Add(Task.Run(() => _chunkManagers[chunckIndex].searchInCol(col, str)));
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var task in tasks)
            {
                if (task.Result != -1)
                {
                    return task.Result; // Return the first found row index
                }
            }
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }

        // If no match found, return -1
        return -1;
    }

    /// <summary>
    /// Searches for the specified string within a specific range of rows and columns.
    /// </summary>
    /// <param name="col1">Start column index.</param>
    /// <param name="col2">End column index.</param>
    /// <param name="row1">Start row index.</param>
    /// <param name="row2">End row index.</param>
    /// <param name="str">The string to search for.</param>
    /// <returns>
    /// A tuple (row, col) of the first cell containing the string in the range, or null if not found.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of range.</exception>
    public Tuple<int, int> searchInRange(int col1, int col2, int row1, int row2, String str)
    {
        // Check valid input
        if (col1 < 0 || col2 < 0 || row1 < 0 || row2 < 0)
            throw new ArgumentOutOfRangeException("searchInRange: Row or column index is out of range. (lower)");
        if (col1 > nCols || row1 > nRows || col2 > nCols || row2 > nRows)
            throw new ArgumentOutOfRangeException("searchInRange: Row or column index is out of range. (higher)");
        if (col1 > col2 || row1 > row2)
            throw new ArgumentOutOfRangeException("searchInRange: Invalid cols or rows, 1 is need to be lower than 2.");

        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();

        if (_userSemaphore != null)
            _userSemaphore.Wait();

        try
        {

            // Get index of the chunks
            int startChunkIndex = getManagerRangeIndex(row1, col1);
            int endChunkIndex = getManagerRangeIndex(row2, col2);

            List<Task<Tuple<int, int>>> tasks = new List<Task<Tuple<int, int>>>();
            for (int i = startChunkIndex; i <= endChunkIndex; i++)
            {
                int chunckIndex = i;
                tasks.Add(Task.Run(() => _chunkManagers[chunckIndex].searchStringInRangeSpecific(col1, col2, row1, row2, str)));
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var task in tasks)
            {
                if (task.Result != null)
                {
                    return task.Result;
                }
            }
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }
        return null;
    }

    /// <summary>
    /// Adds a new row after the specified row index.
    /// </summary>
    /// <param name="row1">The row index after which to add a new row.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the row index is out of range.</exception>
    public void addRow(int row1)
    {
        // Check if row1 is valid - allow inserting at the end (row1 == nRows is valid)
        if (row1 < 0 || row1 > nRows)
        {
            throw new ArgumentOutOfRangeException("addRow: Row index is out of range.");
        }

        // Lock all the table because changing size
        _sizeLock.EnterWriteLock();

        try
        {
            if (_userSemaphore != null)
                _userSemaphore.Wait();

            try
            {
                // Create new row with empty strings (not default values)
                List<string> newRow = new List<string>();
                for (int i = 0; i < nCols; i++)
                {
                    newRow.Add(string.Empty);
                }

                // Add the row after row1 (or at the end if row1 == nRows)
                if (row1 == nRows)
                {
                    // Insert at the end
                    _spreadsheet.Add(newRow);
                }
                else
                {
                    // Insert after row1
                    _spreadsheet.Insert(row1, newRow);
                }

                // Increase the number of rows
                nRows++;

                // Update chunk managers with proper processor count logic
                int numProcessors = Math.Max(1, Environment.ProcessorCount);
                this._chunkManagers = this.GetChuncksList(nRows, nCols, numProcessors);
            }
            finally
            {
                if (_userSemaphore != null)
                    _userSemaphore.Release();
            }
        }
        finally
        {
            _sizeLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds a new column after the specified column index.
    /// </summary>
    /// <param name="col1">The column index after which to add a new column.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the column index is out of range.</exception>
    public void addCol(int col1)
    {
        // Check if col1 is valid - allow inserting at the end (col1 == nCols is valid)
        if (col1 < 0 || col1 > nCols)
        {
            throw new ArgumentOutOfRangeException("addCol: Column index is out of range.");
        }

        // Lock all the table because changing size
        _sizeLock.EnterWriteLock();

        try
        {
            if (_userSemaphore != null)
                _userSemaphore.Wait();

            try
            {
                // Increase the number of columns
                nCols++;

                // Add a column after col1 (or at the end if col1 == nCols-1 after increment)
                for (int i = 0; i < nRows; i++)
                {
                    if (col1 == nCols - 1)
                    {
                        // Insert at the end of each row
                        _spreadsheet[i].Add(string.Empty);
                    }
                    else
                    {
                        // Insert after col1
                        _spreadsheet[i].Insert(col1, string.Empty);
                    }
                }

                // Update chunk managers with proper processor count logic
                int numProcessors = Math.Max(1, Environment.ProcessorCount);
                this._chunkManagers = this.GetChuncksList(nRows, nCols, numProcessors);
            }
            finally
            {
                if (_userSemaphore != null)
                    _userSemaphore.Release();
            }
        }
        finally
        {
            _sizeLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Finds all cells containing the specified string.
    /// </summary>
    /// <param name="str">The string to search for.</param>
    /// <param name="caseSensitive">Whether the search is case sensitive.</param>
    /// <returns>An array of tuples (row, col) for all matching cells.</returns>
    public Tuple<int, int>[] findAll(String str, bool caseSensitive)
    {
        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();

        // perform search and return all relevant cells according to caseSensitive param
        if (_userSemaphore != null)
            _userSemaphore.Wait();

        List<Tuple<int, int>> results;
        try
        {
            // Create a list of tasks to search in each chunk manager
            List<Task<List<Tuple<int, int>>>> tasks = new List<Task<List<Tuple<int, int>>>>();
            for (int i = 0; i < this._chunkManagers.Count(); i++)
            {
                int index = i;
                tasks.Add(Task.Run(() => _chunkManagers[index].searchAllStringInRange(str, caseSensitive)));
            }

            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());

            // Collect results from tasks
            results = new List<Tuple<int, int>>();
            foreach (var task in tasks)
            {
                if (task.Result != null)
                {
                    results.AddRange(task.Result);
                }
            }
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();

            // Release resize Lock
            _sizeLock.ExitReadLock();
        }

        return results.ToArray();
    }

    /// <summary>
    /// Replaces all occurrences of a string with a new string in the spreadsheet.
    /// </summary>
    /// <param name="oldStr">The string to replace.</param>
    /// <param name="newStr">The new string value.</param>
    /// <param name="caseSensitive">Whether the replacement is case sensitive.</param>
    public void setAll(String oldStr, String newStr, bool caseSensitive)
    {
        // replace all oldStr cells with the newStr str according to caseSensitive param
        // Get old
        Tuple<int, int>[] arr = this.findAll(oldStr, caseSensitive);

        // perform search and return all relevant cells according to caseSensitive param

        if (_userSemaphore != null)
            _userSemaphore.Wait();
        try
        {
            // Change all
            for (int i = 0; i < arr.Length; i++)
            {
                int i_row = arr[i].Item1;
                int i_col = arr[i].Item2;
                int index = getManagerRangeIndex(i_row, i_col);
                _chunkManagers[index].writeIndex(i_row, i_col, newStr);
            }
        }
        finally
        {
            if (_userSemaphore != null)
                _userSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets the current size of the spreadsheet.
    /// </summary>
    /// <returns>A tuple (nRows, nCols) representing the size.</returns>
    public Tuple<int, int> getSize()
    {
        // Check there isn't lock on resize
        _sizeLock.EnterReadLock();

        int nRows = this.nRows;
        int nCols = this.nCols;

        // Release resize Lock
        _sizeLock.ExitReadLock();

        // return the size of the spreadsheet in nRows, nCols
        return Tuple.Create(nRows, nCols);
    }

    /// <summary>
    /// Saves the spreadsheet to a file in CSV format.
    /// </summary>
    /// <param name="fileName">The file path to save to.</param>
    public void save(String fileName)
    {
        // save the spreadsheet to a file fileName.
        // you can decide the format you save the data. There are several options.

        // Save the spreadsheet to a file in CSV format - The best option
        _sizeLock.EnterReadLock();
        try
        {
            using (var writer = new StreamWriter(fileName))
            {
                for (int i = 0; i < nRows; i++)
                {
                    var row = _spreadsheet[i];
                    // Check that each row has the same number of columns
                    while (row.Count < nCols)
                        row.Add("");
                    string line = string.Join(",", row.Select(cell => cell.Replace("\"", "\"\"").Contains(",") ? $"\"{cell}\"" : cell));
                    // Write the row to the file
                    writer.WriteLine(line);
                }
            }
        }
        finally
        {
            // Release resize Lock
            _sizeLock.ExitReadLock();
        }

    }

    /// <summary>
    /// Loads the spreadsheet from a CSV file, replacing all current data.
    /// </summary>
    /// <param name="fileName">The file path to load from.</param>
    public void load(String fileName)
    {
        // load the spreadsheet from fileName
        // replace the data and size of the current spreadsheet with the loaded data

        // Load the spreadsheet from fileName (CSV format)
        _sizeLock.EnterWriteLock();
        try
        {
            // Create new spreadsheet
            var newSpreadsheet = new List<List<string>>();
            int maxCols = 0;

            // Read the file
            using (var reader = new StreamReader(fileName))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // CSV parsing (for quoted cells with commas)
                    var row = new List<string>();
                    int i = 0;
                    while (i < line.Length)
                    {
                        // Skip whitespace
                        if (line[i] == '"')
                        {
                            // Quoted cell
                            i++;
                            int start = i;
                            while (i < line.Length)
                            {
                                if (line[i] == '"' && (i + 1 == line.Length || line[i + 1] == ','))
                                    break;
                                if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
                                    i++; // skip escaped quote
                                i++;
                            }
                            row.Add(line.Substring(start, i - start).Replace("\"\"", "\""));
                            i++; // skip closing quote
                            if (i < line.Length && line[i] == ',') i++;
                        }
                        else
                        {
                            // Normal cell without quotes
                            int start = i;
                            while (i < line.Length && line[i] != ',') i++;
                            row.Add(line.Substring(start, i - start));
                            if (i < line.Length && line[i] == ',') i++;
                        }
                    }
                    // Add the row to the new spreadsheet
                    newSpreadsheet.Add(row);
                    // Update maxCols if this row has more columns
                    if (row.Count > maxCols) maxCols = row.Count;
                }
            }

            // Update spreadsheet size
            nRows = newSpreadsheet.Count;
            nCols = maxCols;

            // Normalize all rows to have the same number of columns
            foreach (var row in newSpreadsheet)
            {
                while (row.Count < nCols)
                    row.Add("");
            }

            // Set the new spreadsheet
            _spreadsheet = newSpreadsheet;

            // Update chunk managers
            this._chunkManagers = this.GetChuncksList(nRows, nCols, Environment.ProcessorCount);
        }
        finally
        {
            // Release resize Lock
            _sizeLock.ExitWriteLock();
        }
    }

    // ################### Internal methods for managing chunk managers and their indices ###################

    /// <summary>
    /// Gets the chunk manager index for a given cell.
    /// </summary>
    /// <param name="rowIndex">Row index.</param>
    /// <param name="colIndex">Column index.</param>
    /// <returns>The index of the chunk manager responsible for the cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of range.</exception>
    public int getManagerRangeIndex(int rowIndex, int colIndex)
    {
        // Validate input parameters
        if (rowIndex < 0 || rowIndex >= nRows || colIndex < 0 || colIndex >= nCols)
        {
            throw new ArgumentOutOfRangeException("getManagerRangeIndex: Row or column index is out of range.");
        }

        if (nRows <= 0 || nCols <= 0)
        {
            throw new ArgumentException("Invalid table dimensions.");
        }

        // Get number of processors
        int num_of_processors;
        try
        {
            num_of_processors = Environment.ProcessorCount;
            num_of_processors = Math.Max(1, num_of_processors); // Ensure at least one processor

            // Limit processors based on table dimensions (same logic as GetChuncksList)
            if (nRows >= nCols)
            {
                num_of_processors = Math.Min(num_of_processors, nRows);
            }
            else
            {
                num_of_processors = Math.Min(num_of_processors, nCols);
            }
        }
        catch
        {
            throw new InvalidOperationException("getManagerRangeIndex: Could not determine the number of processors.");
        }

        // Calculate manager index based on the same logic as GetChuncksList
        if (nRows >= nCols)
        {
            // Divide by rows - same logic as GetChuncksList
            int rowsPerManager = nRows / num_of_processors;
            int remainingRows = nRows % num_of_processors;

            // Find which manager this row belongs to
            int currentRowStart = 0;
            for (int i = 0; i < num_of_processors; i++)
            {
                int extraRow = (i < remainingRows) ? 1 : 0;
                int currentRowEnd = currentRowStart + rowsPerManager - 1 + extraRow;

                if (rowIndex >= currentRowStart && rowIndex <= currentRowEnd)
                {
                    return i;
                }

                currentRowStart = currentRowEnd + 1;
            }

            // Fallback (should not reach here with valid input)
            return num_of_processors - 1;
        }
        else
        {
            // Divide by columns - same logic as GetChuncksList
            int colsPerManager = nCols / num_of_processors;
            int remainingCols = nCols % num_of_processors;

            // Find which manager this column belongs to
            int currentColStart = 0;
            for (int i = 0; i < num_of_processors; i++)
            {
                int extraCol = (i < remainingCols) ? 1 : 0;
                int currentColEnd = currentColStart + colsPerManager - 1 + extraCol;

                if (colIndex >= currentColStart && colIndex <= currentColEnd)
                {
                    return i;
                }

                currentColStart = currentColEnd + 1;
            }

            // Fallback (should not reach here with valid input)
            return num_of_processors - 1;
        }
    }

    /// <summary>
    /// Divides the spreadsheet into chunks for parallel processing.
    /// </summary>
    /// <param name="tableRows">Number of rows in the table.</param>
    /// <param name="tableCols">Number of columns in the table.</param>
    /// <param name="numRectangles">Number of chunks to create.</param>
    /// <returns>A list of chunk managers.</returns>
    public List<ChunkManager> GetChuncksList(int tableRows, int tableCols, int numRectangles)
    {

        if (tableRows <= 0 || tableCols <= 0 || numRectangles <= 0)
        {
            throw new ArgumentException("GetChuncksList: Invalid table dimensions or number of rectangles.");
        }

        numRectangles = Math.Max(1, numRectangles); // Ensure at least one rectangle
        List<ChunkManager> chunkManagers;

        chunkManagers = new List<ChunkManager>();

        // If there are more rows than columns, divide by rows
        if (tableRows >= tableCols)
        {
            numRectangles = Math.Min(numRectangles, tableRows); // Limit to number of rows

            int rowsPerRectangle = tableRows / numRectangles;
            int remainingRows = tableRows % numRectangles;

            int currentRow = 0;
            for (int i = 0; i < numRectangles; i++)
            {
                ChunkManager chunkManager = new ChunkManager(i);
                chunkManager.start_row = currentRow;
                chunkManager.start_col = 0;
                chunkManager.end_col = tableCols - 1;

                // Add extra row to first few rectangles if there's remainder
                int extraRow = (i < remainingRows) ? 1 : 0;
                chunkManager.end_row = currentRow + rowsPerRectangle - 1 + extraRow;

                chunkManagers.Add(chunkManager);
                currentRow = chunkManager.end_row + 1;
            }
        }
        else // If there are more columns than rows, divide by columns
        {
            numRectangles = Math.Min(numRectangles, tableCols); // Limit to number of columns

            int colsPerRectangle = tableCols / numRectangles;
            int remainingCols = tableCols % numRectangles;

            int currentCol = 0;
            for (int i = 0; i < numRectangles; i++)
            {
                ChunkManager chunkManager = new ChunkManager(i);
                chunkManager.start_col = currentCol;
                chunkManager.start_row = 0;
                chunkManager.end_row = tableRows - 1;

                // Add extra column to first few rectangles if there's remainder
                int extraCol = (i < remainingCols) ? 1 : 0;
                chunkManager.end_col = currentCol + colsPerRectangle - 1 + extraCol;

                chunkManagers.Add(chunkManager);
                currentCol = chunkManager.end_col + 1;
            }
        }

        return chunkManagers;
    }
    
    public class ChunkManager
    {
        public int id;
        public int start_row, end_row;
        public int start_col, end_col;

        ReaderWriterLockSlim rw;

        public ChunkManager(int id)
        {
            this.rw = new ReaderWriterLockSlim();
            this.id = id;
        }

        /// <summary>
        /// Reads the value at the specified cell within the chunk.
        /// </summary>
        /// <param name="row">Row index.</param>
        /// <param name="col">Column index.</param>
        /// <returns>The string value at the specified cell.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of the chunk's range.</exception>
        public String readIndex(int row, int col)
        {
            // Check valid input
            if (row < start_row || row > end_row || col < start_col || col > end_col)
            {
                return null;
            }
            // Check if the row and col are within the range
            rw.EnterReadLock();
            try
            {
                return SharableSpreadSheet._spreadsheet[row][col];
            }
            finally
            {
                rw.ExitReadLock();
            }
        }

        /// <summary>
        /// Writes a value to the specified cell within the chunk.
        /// </summary>
        /// <param name="row">Row index.</param>
        /// <param name="col">Column index.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if indices are out of the chunk's range.</exception>
        /// <exception cref="ArgumentException">Thrown if value is null.</exception>
        public void writeIndex(int row, int col, String value)
        {
            // Check if the row and col are within the range
            if (row < start_row || row > end_row || col < start_col || col > end_col)
            {
                return;
            }
            // Check valid str
            if (value == null)
                throw new ArgumentException("ChunkManager.writeIndex: Value canoot be null.");
            rw.EnterWriteLock();
            try
            {
                SharableSpreadSheet._spreadsheet[row][col] = value;
            }
            finally
            {
                rw.ExitWriteLock();
            }
        }

        /// <summary>
        /// Searches for a string in the chunk's range.
        /// </summary>
        /// <param name="str">The string to search for.</param>
        /// <param name="caseSensitive">Whether the search is case sensitive.</param>
        /// <returns>A tuple (row, col) if found, otherwise null.</returns>
        public Tuple<int, int> searchStringInRange(String str, bool caseSensitive = true)
        {
            rw.EnterReadLock();
            try
            {
                for (int row = start_row; row <= end_row; row++)
                    for (int col = start_col; col <= end_col; col++)
                    {
                        String t1 = _spreadsheet[row][col];
                        String t2 = str;
                        if (!caseSensitive)
                        {
                            t1 = t1.ToLower();
                            t2 = t2.ToLower();
                        }
                        if (t1 == t2)
                        {
                            return Tuple.Create(row, col);
                        }
                    }
            }
            finally
            {
                rw.ExitReadLock();
            }

            return null;
        }

        /// <summary>
        /// Searches for a string in ALL the chunk's range.
        /// </summary>
        /// <param name="str">The string to search for.</param>
        /// <param name="caseSensitive">Whether the search is case sensitive.</param>
        /// <returns>A List of tuples (row, col) if found, otherwise null.</returns>
        public List<Tuple<int, int>> searchAllStringInRange(String str, bool caseSensitive = true)
        {
            List<Tuple<int, int>> results = new List<Tuple<int, int>>();
            rw.EnterReadLock();
            try
            {
                for (int row = start_row; row <= end_row; row++)
                    for (int col = start_col; col <= end_col; col++)
                    {
                        String t1 = _spreadsheet[row][col];
                        String t2 = str;
                        if (!caseSensitive)
                        {
                            t1 = t1.ToLower();
                            t2 = t2.ToLower();
                        }
                        if (t1 == t2)
                        {
                            results.Add(Tuple.Create(row, col));
                        }
                    }
            }
            finally
            {
                rw.ExitReadLock();
            }

            return results;
        }

        /// <summary>
        /// Searches for a string in a specific subrange of the chunk.
        /// </summary>
        /// <param name="col1">Start column index.</param>
        /// <param name="col2">End column index.</param>
        /// <param name="row1">Start row index.</param>
        /// <param name="row2">End row index.</param>
        /// <param name="str">The string to search for.</param>
        /// <returns>A tuple (row, col) if found, otherwise null.</returns>
        public Tuple<int, int> searchStringInRangeSpecific(int col1, int col2, int row1, int row2, String str)
        {
            rw.EnterReadLock();
            try
            {
                for (int row = Math.Max(start_row, row1); row < Math.Min(end_row, row2); row++)
                    for (int col = Math.Max(start_col, col1); col < Math.Min(end_col, col2); col++)
                        if (_spreadsheet[row][col] == str)
                        {
                            return Tuple.Create(row, col);
                        }
            }
            finally
            {
                rw.ExitReadLock();
            }

            return null;
        }

        /// <summary>
        /// Searches for a string in a specific row within the chunk.
        /// </summary>
        /// <param name="row">Row index.</param>
        /// <param name="str">The string to search for.</param>
        /// <returns>The column index if found, otherwise -1.</returns>
        public int searchInRow(int row, String str)
        {
            if (row < start_row || row > end_row)
            {
                return -1;
            }

            rw.EnterReadLock();
            try
            {
                for (int col = start_col; col <= end_col; col++)
                {
                    if (SharableSpreadSheet._spreadsheet[row][col] == str)
                    {
                        return col;
                    }
                }
            }
            finally
            {
                rw.ExitReadLock();
            }

            return -1;
        }

        /// <summary>
        /// Searches for a string in a specific column within the chunk.
        /// </summary>
        /// <param name="col">Column index.</param>
        /// <param name="str">The string to search for.</param>
        /// <returns>The row index if found, otherwise -1.</returns>
        public int searchInCol(int col, String str)
        {
            if (col < start_col || col > end_col)
            {
                return -1;
            }

            rw.EnterReadLock();
            try
            {
                for (int row = start_row; row <= end_row; row++)
                {
                    if (SharableSpreadSheet._spreadsheet[row][col] == str)
                    {
                        return row;
                    }
                }
            }
            finally
            {
                rw.ExitReadLock();
            }

            return -1;
        }

        /// <summary>
        /// Gets all data from a specific row within the chunk.
        /// </summary>
        /// <param name="row">Row index.</param>
        /// <returns>A list of string values for the row, or null if out of range.</returns>
        public List<String> getRowData(int row)
        {
            if (row < start_row || row > end_row)
            {
                return null;
            }

            rw.EnterReadLock();
            try
            {
                List<String> rowData = new List<String>();
                for (int col = start_col; col <= end_col; col++)
                {
                    rowData.Add(SharableSpreadSheet._spreadsheet[row][col]);
                }

                return rowData;
            }
            finally
            {
                rw.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all data from a specific column within the chunk.
        /// </summary>
        /// <param name="col">Column index.</param>
        /// <returns>A list of string values for the column, or null if out of range.</returns>
        public List<String> getColData(int col)
        {
            if (col < start_col || col > end_col)
            {
                return null;
            }

            rw.EnterReadLock();
            try
            {
                List<String> colData = new List<String>();
                for (int row = start_row; row <= end_row; row++)
                {
                    colData.Add(SharableSpreadSheet._spreadsheet[row][col]);
                }

                return colData;
            }
            finally
            {
                rw.ExitReadLock();
            }
        }
    }
}

using System.Data;
using System.Windows.Forms;

namespace SpreadsheetApp
{
    public partial class main_form : Form
    {

        SharableSpreadSheet sheet;
        String selectedFile = "";
        public main_form()
        {
            this.sheet = new SharableSpreadSheet(1, 1, -1);
            InitializeComponent();

            InitButtons();
        }

        private void Form_Load(object sender, EventArgs e)
        {
         
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.AllowUserToAddRows = false;

           defaultImage.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        private void InitButtons()
        {
            int spacing = 20; // Space between buttons
            int totalButtonWidth = load_button.Width + save_button.Width + spacing;

            // Calculate center position
            int centerX = (this.ClientSize.Width - totalButtonWidth) / 2;
            int bottomY = this.ClientSize.Height - load_button.Height - 20; // 20px from bottom

            // Position buttons
            load_button.Location = new Point(centerX, bottomY);
            save_button.Location = new Point(centerX + load_button.Width + spacing, bottomY);
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            InitButtons();
        }

        private void AutoResizeFormWidthForColumns()
        {
            if (grid.DataSource != null)
            {
                // Force the grid to calculate column sizes
                grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

                // Wait for layout to complete
                Application.DoEvents();

                // Calculate required width
                int totalColumnsWidth = 0;
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (column.Visible)
                    {
                        totalColumnsWidth += column.Width;
                    }
                }

                int rowHeaderWidth = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
                int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
                int borderWidth = 4; // Border padding
                int formPadding = 40; // Extra padding for form

                int requiredWidth = totalColumnsWidth + rowHeaderWidth + scrollBarWidth + borderWidth + formPadding;

                // Set reasonable limits
                int minWidth = 400;
                int maxWidth = Screen.PrimaryScreen.WorkingArea.Width - 100;

                // Apply width
                int finalWidth = Math.Max(minWidth, Math.Min(maxWidth, requiredWidth)) + 20;

                // Resize form
                this.Width = finalWidth;

                // If we hit the max width, enable horizontal scrolling
                if (requiredWidth > maxWidth)
                {
                    grid.ScrollBars = ScrollBars.Both;
                }
                else
                {
                    grid.ScrollBars = ScrollBars.Vertical;
                }

                grid.RowHeadersWidth = this.Width / 11;
            }
        }

        private void LoadCsvToDataGridView(string filePath)
        {
            DataTable dataTable = new DataTable();
            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length > 0)
            {
                // Add columns from header row
                string[] headers = lines[0].Split(',');
                int colNum = 1;
                foreach (string header in headers)
                {
                    String s = "Col" + colNum++;
                    dataTable.Columns.Add(s);
                }

                // Add data rows
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] fields = lines[i].Split(',');
                    DataRow row = dataTable.NewRow();

                    for (int j = 0; j < fields.Length && j < headers.Length; j++)
                    {
                        row[j] = fields[j].Trim();
                    }

                    dataTable.Rows.Add(row);
                }

                grid.DataSource = dataTable;
            }

            sheet.load(filePath);
        }


        private void DataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // Check if it's a valid cell (not header row)
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridView dgv = sender as DataGridView;

                // Get the changed cell value
                object newValue = dgv[e.ColumnIndex, e.RowIndex].Value;
                string columnName = dgv.Columns[e.ColumnIndex].Name;

                sheet.setCell(e.RowIndex, e.ColumnIndex, newValue.ToString());
            }
        }
        private void load_button_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Filter to only show CSV files
            openFileDialog.Filter = "CSV files (*.csv)|*.csv";
            openFileDialog.Title = "Select CSV File";
            openFileDialog.InitialDirectory = @"C:\";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFile = openFileDialog.FileName;

                // Load the CSV file into DataGridView
                LoadCsvToDataGridView(selectedFile);

                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    if (!grid.Rows[i].IsNewRow)
                    {
                        grid.Rows[i].HeaderCell.Value = $"Row{i + 1}";
                    }
                }

                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                AutoResizeFormWidthForColumns();
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                defaultImage.Hide();
            }
        }

        

        private void save_button_Click(object sender, EventArgs e)
        {
            if (selectedFile != "")
            {
                sheet.save(selectedFile);
                MessageBox.Show("File saved successfully!", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show("Please load a file before saving.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

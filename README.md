# SpreadsheetApp

A multi-user, thread-friendly spreadsheet application built with C# and Windows Forms.

## Overview

SpreadsheetApp is a Windows Forms application that provides a simple, interactive spreadsheet interface. It is designed to support concurrent access by multiple users, making it suitable for collaborative environments or scenarios where thread safety is essential.

## Features

- Editable spreadsheet grid with dynamic resizing
- Save and load spreadsheet data
- Thread-friendly: supports concurrent access and editing
- User-friendly graphical interface

## Thread Safety

The core spreadsheet logic is implemented in [`SharableSpreadSheet`](SharableSpreadSheet.cs), which is designed to be thread-friendly:

- **Concurrent Access:** The spreadsheet is divided into chunks, each managed by a `ChunkManager` with its own `ReaderWriterLockSlim` for fine-grained locking.
- **User Limiting:** A `SemaphoreSlim` controls the maximum number of concurrent users.
- **Global Operations:** A global `ReaderWriterLockSlim` ensures safe resizing and structural changes.
- **Safe Cell Access:** All cell read/write operations are protected by appropriate locks to prevent race conditions.

## Project Structure

- [`Form1.cs`](Form1.cs): Main Windows Form and UI logic
- [`SharableSpreadSheet.cs`](SharableSpreadSheet.cs): Spreadsheet data structure and thread-safe logic
- [`Program.cs`](Program.cs): Application entry point
- [`Properties/Resources.resx`](Properties/Resources.resx): Application resources
- [`Resources/`](Resources/): Images and other resources

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or later (recommended for WinForms designer support)

### Building

Open [`SpreadsheetApp.sln`](SpreadsheetApp.sln) in Visual Studio and build the project, or use the command line:

```sh
dotnet build
```

### Running

You can run the application from Visual Studio or with:

```sh
dotnet run --project SpreadsheetApp.csproj
```

## Usage

- Launch the application.
- Use the spreadsheet grid to enter and edit data.
- Multiple users/threads can safely access and modify the spreadsheet concurrently.
- Save or load spreadsheets using the menu options.

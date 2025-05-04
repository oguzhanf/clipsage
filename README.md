# ClipSage

## Free and Open Source Clipboard History & Snippet Manager for Windows

ClipSage is a powerful, free, and open-source clipboard management tool designed for Windows users who need more than just basic copy-paste functionality. It provides an intelligent way to track, organize, and reuse your clipboard history while offering advanced snippet management capabilities.

### Key Features

- **Comprehensive Clipboard History**: Automatically saves everything you copy, allowing you to access your clipboard history anytime.
- **Smart Search**: Quickly find past clips with powerful search functionality.
- **Snippet Management**: Create, organize, and reuse frequently used text snippets.
- **Keyboard Shortcuts**: Access all functionality without taking your hands off the keyboard (Ctrl+Shift+V).
- **Format Preservation**: Maintains formatting for rich text, code, images, and more.
- **Secure Local Storage**: All your clipboard data stays on your computer.
- **Metro UI**: Modern Windows interface with light and dark themes.

### Getting Started

1. Download the latest release from the [Releases page](https://github.com/oguzhanf/clipsage/releases)
2. Extract the ZIP file to any location
3. Run ClipSage.App.exe
4. The first time you run ClipSage, it will ask if you want to set up in a new location
5. ClipSage will appear in your system tray
6. Press Ctrl+Shift+V to access your clipboard history

### Portable Mode

ClipSage is now a fully portable application:

- Run it from any location, including USB drives
- All data is stored in a "Cache" folder next to the executable
- Create shortcuts during first run or manually later
- No installation required

### Building from Source

#### Prerequisites

- Windows 10 or later
- .NET 9 SDK
- Visual Studio 2022 or later (optional)

#### Build Instructions

1. Clone the repository:
   ```
   git clone https://github.com/oguzhanf/clipsage.git
   cd clipsage
   ```

2. Build the solution:
   ```
   dotnet build
   ```

3. Create a portable package:
   ```
   .\scripts\build-portable.ps1
   ```

   This will:
   - Build the solution
   - Create a ZIP file in the `bin` directory containing the portable application

### Project Structure

- `ClipSage.App`: The main WPF application
- `ClipSage.Core`: Core functionality and business logic
- `ClipSage.Tests`: Unit tests
- `scripts`: Build scripts
- `bin`: Output directory for builds

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

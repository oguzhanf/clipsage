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
2. Run the installer and follow the instructions
3. ClipSage will start automatically and appear in your system tray
4. Press Ctrl+Shift+V to access your clipboard history

### Building from Source

#### Prerequisites

- Windows 10 or later
- .NET 9 SDK
- Visual Studio 2022 or later (optional)
- WiX Toolset v6.0 (for MSI installers)

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

3. Create an installer:
   ```
   .\build-msi.ps1
   ```

   This will:
   - Build the solution
   - Create an MSI installer in the `bin` directory

#### Advanced Build Options

For advanced build options, see the [build documentation](build/README.md).

### Project Structure

- `src/Clipper.App`: The main WPF application
- `src/Clipper.Core`: Core functionality and business logic
- `src/Clipper.Tests`: Unit tests
- `src/Clipper.Installer`: WiX installer project
- `build`: Build scripts and tools
- `bin`: Output directory for builds and installers

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

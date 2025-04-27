# ClipSage (formerly ClipperMVP)

## Advanced Clipboard History & Snippet Manager for Windows

ClipSage is a powerful clipboard management tool designed for Windows users who need more than just basic copy-paste functionality. It provides an intelligent way to track, organize, and reuse your clipboard history while offering advanced snippet management capabilities.

### Key Features

- **Comprehensive Clipboard History**: Automatically saves everything you copy, allowing you to access your clipboard history anytime.
- **Smart Search**: Quickly find past clips with powerful search functionality.
- **Snippet Management**: Create, organize, and reuse frequently used text snippets.
- **Keyboard Shortcuts**: Access all functionality without taking your hands off the keyboard.
- **Format Preservation**: Maintains formatting for rich text, code, images, and more.
- **Secure Storage**: Protects sensitive information with optional encryption.
- **Cloud Sync**: Synchronize your clipboard history across multiple devices (coming soon).

### Getting Started

ClipSage is currently in development. Stay tuned for the first release!

### Building from Source

#### Prerequisites

- Windows 10 or later
- .NET 9 SDK
- Visual Studio 2022 or later (optional)
- WiX Toolset v3.11 or v6.0 (for MSI installers)

#### Build Instructions

1. Clone the repository:
   ```
   git clone https://github.com/oguzhanf/clipsage.git
   cd clipsage
   ```

2. Build the solution:
   ```
   .\build.bat
   ```

   This will:
   - Increment the build version
   - Clean and build the solution
   - Publish the application
   - Create MSI and ZIP installers in the `bin` directory

#### Advanced Build Options

For advanced build options, see the [build documentation](build/README.md).

### Project Structure

- `src/Clipper.App`: The main WPF application
- `src/Clipper.Core`: Core functionality and business logic
- `src/Clipper.Tests`: Unit tests
- `src/Clipper.Web`: Web API for future cloud integration
- `build`: Build scripts and tools
- `bin`: Output directory for builds and installers

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

# ClipSage

<div align="center">
  <img src="ClipSage.App/Resources/clipboard.ico" alt="ClipSage Logo" width="128" height="128">
  <h3>Free and Open Source Clipboard History & Snippet Manager for Windows</h3>
</div>

## 📋 Overview

ClipSage is a powerful, free, and open-source clipboard management tool designed for Windows users who need more than just basic copy-paste functionality. It provides an intelligent way to track, organize, and reuse your clipboard history while offering advanced snippet management capabilities.

## ✨ Key Features

- **📚 Comprehensive Clipboard History**: Automatically saves everything you copy, allowing you to access your clipboard history anytime.
- **🔍 Smart Search**: Quickly find past clips with powerful search functionality.
- **📝 Multiple Content Types**: Support for text, images, and file paths.
- **⌨️ Keyboard Shortcuts**: Access all functionality without taking your hands off the keyboard (Ctrl+Shift+V).
- **🎨 Format Preservation**: Maintains formatting for rich text, code, images, and more.
- **🔒 Secure Local Storage**: All your clipboard data stays on your computer.
- **🖥️ Metro UI**: Modern Windows interface with clean, minimalist design.
- **💾 Portable Mode**: Run from any location without installation.
- **🔄 Auto-Updates**: Stay up-to-date with the latest features and improvements.

## 🚀 Getting Started

1. Download the latest release from the [Releases page](https://github.com/oguzhanf/clipsage/releases)
2. Run the downloaded `ClipSage-x.x.xx.exe` file
3. The first time you run ClipSage, it will ask if you want to set up in a new location
4. ClipSage will appear in your system tray
5. Press `Ctrl+Shift+V` to access your clipboard history

## 💻 Usage

### Basic Operations

- **Copy anything**: Just use `Ctrl+C` as usual
- **Access clipboard history**: Press `Ctrl+Shift+V`
- **Search clips**: Start typing in the search box
- **Use a clip**: Click on any item in the history to paste it
- **Pin important clips**: Click the pin icon to keep items at the top

### System Tray

Right-click the ClipSage icon in the system tray to:
- Pause/Resume clipboard monitoring
- Open settings
- Check for updates
- Exit the application

## 📱 Portable Mode

ClipSage is a fully portable application:

- Run it from any location, including USB drives
- All data is stored in a "Cache" folder next to the executable
- Create shortcuts during first run or manually later
- No installation required

## 🛠️ Building from Source

### Prerequisites

- Windows 10 or later
- .NET 9 SDK
- Visual Studio 2022 or later (optional)

### Build Instructions

1. Clone the repository:
   ```
   git clone https://github.com/oguzhanf/clipsage.git
   cd clipsage
   ```

2. Build the solution:
   ```
   dotnet build
   ```

3. Create a release build:
   ```
   dotnet publish -c Release -r win-x64 --self-contained false
   ```

### Project Structure

- `ClipSage.App`: The main WPF application
- `ClipSage.Core`: Core functionality and business logic
- `ClipSage.Tests`: Unit tests

### Creating a Release

The project includes automated GitHub Actions workflows for building and releasing:

**Option 1: Manual Trigger (Recommended)**
1. Go to the [Actions tab](../../actions/workflows/build-and-release.yml)
2. Click "Run workflow"
3. Ensure "Create a GitHub release" is checked
4. Click "Run workflow"
5. The workflow will build the application and create a release with artifacts

**Option 2: Tag-based Release**
1. Ensure the version in `Directory.Build.props` is updated
2. Create and push a tag:
   ```bash
   git tag v1.0.32
   git push origin v1.0.32
   ```
3. The workflow will automatically build and create a release

**Option 3: Local Build (Windows only)**
```powershell
# Build and create release
.\scripts\build-and-release.ps1
```

## 🤝 Contributing

Contributions are welcome! Feel free to:
- Report bugs
- Suggest features
- Submit pull requests

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Thanks to all contributors who have helped make ClipSage better
- Special thanks to the .NET and WPF communities for their excellent tools and resources

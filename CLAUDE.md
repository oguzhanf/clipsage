# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository

ClipSage is a Windows-only WPF clipboard history & snippet manager targeting `net9.0-windows`, x64 only. Builds must run on Windows — WPF will not build on Linux.

## Common commands

Build / test (run from the repo root, where `ClipSage.sln` lives):

```powershell
dotnet build ClipSage.sln -c Release            # build all three projects
dotnet test  ClipSage.Tests/ClipSage.Tests.csproj
dotnet test  ClipSage.Tests/ClipSage.Tests.csproj --filter "FullyQualifiedName~XmlHistoryStoreTests"  # single class
dotnet test  ClipSage.Tests/ClipSage.Tests.csproj --filter "DisplayName~MethodName"                   # single test
dotnet publish ClipSage.App/ClipSage.App.csproj -c Release -r win-x64 --self-contained false
```

Higher-level scripts in `scripts/`:

- `scripts/build-portable.ps1` — publishes to `bin/ClipSage-{version}/` and zips it to `bin/ClipSage-{version}.zip`. Version is read from `Directory.Build.props`.
- `scripts/build-and-release.ps1` — full release path: build → copy `ClipSage.App.exe` to `bin/ClipSage-{version}.exe` → tag `v{version}` → `gh release create`. Requires `gh` authenticated.
- `scripts/create-release.ps1` — release step only (no build).
- `scripts/build.ps1` — wrapper that delegates to `build/build.ps1` (MSI/ZIP installer flow, requires WiX Toolset v6.0 at `C:\Program Files\WiX Toolset v6.0\bin`).

Test framework is xUnit. `ClipSage.Core` exposes `InternalsVisibleTo ClipSage.Tests`.

## Versioning — critical

The single source of truth for the app's version is `Directory.Build.props`. **All four version fields** must be updated together before a release:

```xml
<Version>1.0.32</Version>
<AssemblyVersion>1.0.32.0</AssemblyVersion>
<FileVersion>1.0.32.0</FileVersion>
<InformationalVersion>1.0.32</InformationalVersion>
```

The in-app update checker (`ClipSage.Core/Update/UpdateChecker.cs`) reads `Assembly.GetName().Version` and compares against the latest GitHub release tag at `api.github.com/repos/oguzhanf/clipsage/releases`. If the code version lags behind the latest released tag, every user sees a perpetual "update available" prompt — so bump `Directory.Build.props` *before* building the release, never after. See `docs/RELEASE_PROCESS.md` for the full flow.

## Architecture

Three projects, all `net9.0-windows`, x64:

- **`ClipSage.Core`** — business logic, no WPF. Includes WinForms (`UseWindowsForms=true`) only because clipboard monitoring needs a hidden message-only window.
- **`ClipSage.App`** — WPF UI (`OutputType=WinExe`, `PublishSingleFile=true`, `PublishReadyToRun=true`). Depends on `Core`. Packages: `Hardcodet.NotifyIcon.Wpf` (tray), `MaterialDesignThemes` (Metro look), `NHotkey.Wpf` (global `Ctrl+Shift+V`).
- **`ClipSage.Tests`** — xUnit. References both Core and App.

### Clipboard capture (`ClipSage.Core/ClipboardService.cs`)

Singleton (`ClipboardService.Instance`) that spins up a hidden STA-thread WinForms message-only window and registers it with `AddClipboardFormatListener`. On `WM_CLIPBOARDUPDATE` it waits ~75 ms (to avoid contention with the source app), opens the clipboard, builds a `ClipboardEntry`, and raises `ClipboardChanged`. Snipping Tool special case: when the clipboard holds both text *and* an image, and the text is a `file:///*.png` path, treat it as Image — otherwise the same screenshot gets stored twice. Always call `CloseClipboard` so other apps regain access.

### Storage (`ClipSage.Core/Storage/`)

`IHistoryStore` is the contract (Add/GetRecent/Delete/Pin/CleanupDuplicates, all async). Production implementation is `XmlHistoryStore`:

- Files live under `{cachingFolder}/History/history-{MachineName}.xml`, one file per computer — designed for syncing the cache folder across machines (e.g., Dropbox/OneDrive).
- Hard cap of `MaxHistorySize = 500` entries.
- A `FileSystemWatcher` on the History folder fires `HistoryExternallyUpdated` so the UI reloads when another machine writes its file. Don't disable the watcher when adding multi-system features.
- Image blobs are offloaded to `FileBasedClipboardStore` rather than embedded in XML.

`ClipboardEntry.ComputerName` and `SourceFile` exist specifically for the multi-system merge.

### Portable mode (`ClipSage.App/App.xaml.cs`, `PortableHelper.cs`)

The app has no installer — it is always "portable." On startup it probes whether `AppContext.BaseDirectory` is writable; if yes, treat the current location as the portable install and store the cache next to the EXE. If no (e.g., launched from `Program Files`), prompt the user to copy itself to a writable location. Settings (`CachingFolder`, `CachingFolderConfigured`, `StartMinimized`, `MinimizeToTray`) live in standard `Properties.Settings`. The first run with no configured cache folder shows `CachingFolderDialog` and exits if cancelled.

### Update flow (`ClipSage.Core/Update/`)

- `UpdateChecker` — GET releases JSON, parse the latest tag, compare to `CurrentVersion`. Returns `UpdateInfo?`.
- `PortableUpdater` — downloads the new EXE and swaps it on disk. Triggered from the UI or by relaunching with the `-checkupdate` arg (handled in `App.xaml.cs` → `PortableUpdateDialog`).

### UI (`ClipSage.App/`)

MVVM-ish: `MainViewModel` (`MainWindow`) holds an `ObservableCollection<ClipboardEntryViewModel>`, subscribes to `ClipboardService.ClipboardChanged` and `XmlHistoryStore.HistoryExternallyUpdated`, and persists via `IHistoryStore`. `Converters/` holds the standard WPF value converters. The `NHotkey.Wpf` global hotkey is `Ctrl+Shift+V`.

## Conventions worth knowing

- Nullable reference types are enabled across all projects.
- Solution defines only `x64` build configurations — `Any CPU` and `x86` are mapped to `x64`. Don't add `AnyCPU` configs.
- Logging goes through `ClipSage.Core.Logging.Logger.Instance` (NLog). The logger is initialized in `App.OnStartup` *after* the cache folder is known, so log files land inside the cache folder.

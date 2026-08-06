# Local Screen Recorder

A compact, fully local Windows screen recorder built with C#, WPF, and .NET 10 LTS. It records one monitor, the full multi-monitor desktop, or a custom physical-pixel region to H.264/AAC MP4 with optional system audio and microphone input.

## Requirements

- Windows 10 version 1803 (build 17134) or newer, or Windows 11.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build. The self-contained publish does not require a .NET runtime on the destination PC.
- [Microsoft Visual C++ 2015–2022 Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe).
- Windows Media Foundation. It is included in normal Windows 10/11 editions. On Windows N/KN, install the Media Feature Pack from **Settings > Apps > Optional features**.

No FFmpeg installation, account, cloud service, network access, telemetry, or OBS component is used at runtime.

## Ready-to-run executable

The self-contained single-file build is included here:

```powershell
.\dist\LocalScreenRecorder.exe
```

Only `LocalScreenRecorder.exe` is needed. It includes the .NET 10 runtime and extracts its native recording bridge into the current user's temporary bundle directory when launched.

## Build, test, run, and publish

Run these commands from the repository root in PowerShell:

```powershell
dotnet restore LocalScreenRecorder.sln
dotnet build LocalScreenRecorder.sln -c Release --no-restore
dotnet test LocalScreenRecorder.sln -c Release --no-build
dotnet run --project src\LocalScreenRecorder.App\LocalScreenRecorder.App.csproj -c Release
```

Create a self-contained Windows x64 build:

```powershell
dotnet publish src\LocalScreenRecorder.App\LocalScreenRecorder.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o artifacts\publish\win-x64
```

Create the standalone executable used in `dist`:

```powershell
dotnet publish src\LocalScreenRecorder.App\LocalScreenRecorder.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist
```

Run the published application:

```powershell
.\artifacts\publish\win-x64\LocalScreenRecorder.exe
```

The project is x64-only because its Media Foundation bridge contains architecture-specific native code.

## Using the recorder

1. Choose **Entire screen**, **Selected monitor**, or **Custom area**.
2. For a custom area, select **Select Area**, drag across any connected monitor, and release. Escape cancels.
3. Choose system audio, microphone, volume levels, quality, frame rate, and output folder.
4. Select **Start Recording**. Pause, resume, and stop from the window or global shortcuts.
5. After MP4 finalization, use **Open File** or **Open Folder**.

Default global shortcuts:

- `Ctrl+Shift+R`: start or stop
- `Ctrl+Shift+P`: pause or resume
- `Ctrl+Shift+A`: select an area

Change them under **Settings and global shortcuts**. Registration conflicts are reported and the previous working shortcuts remain registered.

## Technical approach

- **Capture:** every display source explicitly uses `Windows.Graphics.Capture`. Multi-monitor and custom-region recordings are assembled from per-monitor physical-pixel slices, including monitors with negative coordinates.
- **Audio:** WASAPI loopback captures the default playback endpoint. WASAPI input captures the selected microphone. Both streams are resampled to 48 kHz stereo, volume-adjusted, normalized to avoid clipping, and mixed by the native recording engine.
- **Encoding:** Microsoft Media Foundation writes H.264 video and AAC audio directly to MP4. Hardware H.264 encoding is preferred and Custom quality can disable it for software fallback. Variable frame timing is enabled to avoid unnecessary duplicate frames.
- **Pause and synchronization:** the native Media Foundation presentation clock pauses and resumes both streams together, preserving timestamps during longer recordings.
- **Files:** recording goes to a hidden `.partial.mp4` in the destination folder. It is moved to a unique `Recording_yyyy-MM-dd_HH-mm-ss.mp4` name only after successful finalization. Failed partial files are removed.
- **Privacy:** the main window and recording indicator use `WDA_EXCLUDEFROMCAPTURE` where Windows supports it. The custom-area border closes before capture begins. Recording only starts from a button or registered shortcut.
- **Hotkeys:** `RegisterHotKey` handles process-wide shortcuts with conflict detection and `MOD_NOREPEAT`.
- **Application structure:** WPF MVVM with dependency injection and interfaces around capture, audio, encoding, recording, hotkeys, display enumeration, settings, logging, and region selection.

The native bridge is [ScreenRecorderLib 6.6.0](https://www.nuget.org/packages/ScreenRecorderLib/6.6.0), which wraps Windows Graphics Capture, WASAPI, and Media Foundation. Its source is available on [GitHub](https://github.com/sskodje/ScreenRecorderLib).

## Quality presets

| Preset | Maximum output | Video | Audio |
| --- | --- | --- | --- |
| Low | 1280×720, aspect-preserving | 15/30 FPS, 2 Mbps | 96 kbps |
| Medium | 1920×1080, aspect-preserving | 30 FPS, 5 Mbps | 128 kbps |
| High | Source resolution | 30/60 FPS, 10 Mbps | 192 kbps |
| Very High | Source resolution | up to 60 FPS, 20 Mbps | 192 kbps |
| Custom | User bounds, aspect-preserving | 1–120 FPS, custom bitrate, hardware toggle | nearest supported AAC rate |

Windows Media Foundation's AAC path in the selected native bridge supports 96, 128, 160, and 192 kbps. Very High therefore uses the most reliable supported value, 192 kbps, rather than requesting an unsupported 256 kbps mode.

## Settings and logs

- Settings: `%LOCALAPPDATA%\LocalScreenRecorder\settings.json`
- Application logs: `%LOCALAPPDATA%\LocalScreenRecorder\logs\recorder-yyyy-MM-dd.log`
- Native recording log: `%LOCALAPPDATA%\LocalScreenRecorder\logs\native-recorder.log`

Settings are human-readable JSON. A malformed file is preserved as `settings.corrupt-yyyyMMdd-HHmmss.json`, and defaults are loaded automatically.

## Tests

The test project covers:

- quality preset and aspect-ratio conversion
- filename formatting and collision handling
- DPI/negative-coordinate region conversion and cross-monitor slicing
- settings JSON round trips and corrupt input recovery
- hotkey parsing, validation, and duplicate detection

## Troubleshooting

- **Encoder initialization fails:** update the GPU driver, try Custom quality with hardware encoding disabled, and confirm Media Foundation is installed.
- **No system audio:** confirm an enabled default playback device exists and is not disconnected.
- **Microphone fails:** refresh devices, choose the default microphone, or disable microphone recording.
- **Capture fails:** confirm Windows 10 1803+ and current display drivers. Remote/locked sessions may not expose a capturable display.
- **Shortcut conflict:** choose a different modified key combination and select **Apply shortcuts**.
- **MP4 cannot finalize:** verify the output folder is writable and the drive has free space.

The application makes no runtime network requests and never uploads recordings.

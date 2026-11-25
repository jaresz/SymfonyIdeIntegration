# Project guide

This guide explains where to find the Symfony IDE Integration code, how a Symfony link is handled, and which tools build the application. You do not need prior knowledge of .NET, Windows Forms, or Inno Setup; the terms are explained below in the context of this repository.

## What the application does

Symfony can include a link to a file and line of code in its configuration. When clicked, Windows starts this application with a `Q3a-Symfony2ide://...` address. The application resolves the project location from the link, finds the editor selected in its settings, and passes it the file path and line number. Projects can be in a local Windows directory, in a WSL distribution, or on an SSH host. The application runs mainly in the system tray, where users can choose an editor and configure settings.

## Repository map

```text
.
├── src/SymfonyIdeIntegration/   Windows application code
├── installer/                   Inno Setup installer definition
├── artifacts/                   local build outputs and tools
├── README.md                    user guide and Symfony configuration
├── PROJECT-GUIDE.md             this guide to the codebase
├── AGENTS.md                    repository and build instructions
├── VERSION                      current MAJOR.MINOR.PATCH version
├── build-installer.ps1          installer build script
└── .gitignore                   files ignored by Git
```

### `src/SymfonyIdeIntegration/` — the application

This is a C# project for Windows. C# is the programming language, .NET 10 provides the runtime and libraries, and Windows Forms (part of .NET) provides menus, dialog boxes, and the system tray icon. The `.csproj` file describes the project to .NET tools: it targets `net10.0-windows`, enables Windows Forms and nullable reference types (checks and warnings for values that may be empty), and embeds sound and image files in the application. It reads the version from the root `VERSION` file.

The easiest way to follow the code is in the order a link is handled:

1. `Program.cs` is the application entry point. The `--open` argument means the app was launched through a protocol link; with no arguments, it starts the tray app. This file also handles unexpected errors and shows a message to the user.
2. `EditorLauncher.cs` loads settings, asks another component to interpret the address, and prepares process arguments for VS Code or PhpStorm. `ProcessStartInfo.ArgumentList` passes arguments separately instead of assembling a command line manually.
3. `EditorLink.cs` checks the link syntax, line number, and parameters, then resolves the project and file paths. The `connect_to` parameter selects WSL or SSH. For a local Windows link, the application can find the `.idea` directory.
4. `EditorDiscovery.cs` finds the editor executable using the configured path, the `PATH` environment variable, or common Windows installation directories.
5. `UserSettings.cs` reads and writes `settings.json` in `%LOCALAPPDATA%\SymfonyIdeIntegration`. A C# `record` is a type for holding data, and `System.Text.Json` converts the settings object to and from JSON text.
6. `IntegrationLog.cs` records events and errors in `integration.log` in the same data directory. The log is periodically cleaned up; `README.md` describes its retention period and size limit.

User interface and supporting files:

| File | Purpose |
|---|---|
| `TrayApplication.cs` | Creates the tray menu, switches editors, opens settings and About windows, refreshes the icon, and schedules periodic log cleanup. |
| `SettingsDialog.cs` | Settings window for selecting editor paths and validating them before saving. |
| `AboutDialog.cs` | Window showing the application name, version, and illustrated scene. |
| `BridgeScene.cs` | Draws and animates the illustration in the About window using .NET graphics APIs; plays the embedded theme audio. |
| `PilotArm.cs` | Helper renderer for the character's arm in the animation. It uses the forearm bitmap, a pixel buffer, and a simple depth buffer to draw the bent arm correctly. |
| `TrayIcons.cs` | Loads an icon from an editor executable or draws a fallback icon labeled VS/PS. |
| `IntegrationException.cs` | Exception type for invalid links and other user-readable integration errors. |
| `BridgeTheme.wav` | Audio file embedded in the application and used by the animation. WAV is an uncompressed audio file format. |
| `Art/` | PNG images embedded in the application and used to draw the animated scene. |

Files in `Art/`: `Bridge.png` is the bridge background; `Head.png`, `HeadLeft.png`, and `HeadRight.png` contain the head and rotation frames; `Shirt.png`, `Seat.png`, and `Hand.png` are character parts; `Forearm.png` is the texture used by `PilotArm`. PNG supports transparency, so image layers can be combined without rectangular backgrounds.

### `installer/` — installer definition

`SymfonyIdeIntegration.iss` is a script for Inno Setup, a Windows installer compiler. It defines the name, version, x64 architecture, per-user installation location, Start menu shortcut, optional startup entry, and registration of the `Q3a-Symfony2ide` protocol. It expects the build script to provide a ready-to-install application in a publish directory. Inno Setup packages the executable into an `.exe` installer.

### `artifacts/` — local outputs

This directory holds generated files that are not application source code: installers, published application files, temporary build directories, and locally used tools. Its contents vary by computer, and some may be ignored by Git. `.gitignore` explicitly ignores `/artifacts/` as well as the `bin/` and `obj/` directories generated by .NET builds. Files in `artifacts/` should not be treated as source code.

### Other root files

- `README.md` describes installation and usage, Symfony's `framework.ide` configuration, and basic build commands.
- `AGENTS.md` contains repository instructions for coding agents, including the recommended build command and a warning that a successful installer build changes the version.
- `VERSION` stores the version in `MAJOR.MINOR.PATCH` format. MSBuild reads it during compilation. After successfully creating a package, the installer script increments the appropriate version component and writes it back.
- `build-installer.ps1` coordinates a release: it checks tools and version, publishes the application, calls the Inno Setup compiler, places the installer in `artifacts/`, and updates `VERSION` only after success. PowerShell is the Windows shell and scripting language.
- `.gitignore` tells Git which locally generated files to leave out of version control.

## Tools and what they do

- **Git** tracks changes to source code and documentation. `.gitignore` helps keep build outputs out of the repository history.
- **PowerShell** runs `build-installer.ps1` and .NET commands. Run the commands in `AGENTS.md` from the repository root.
- **.NET 10 SDK** provides `dotnet`, which builds and publishes the application. A build compiles C# into an application; publishing prepares files for distribution.
- **MSBuild** is the build engine used by `dotnet build`. It reads `.csproj` settings, resolves dependencies, and writes intermediate output to `obj/` and build output to `bin/`.
- **Inno Setup 6.3+** (`ISCC.exe`) creates the Windows installer from `installer/SymfonyIdeIntegration.iss`.
- **Visual Studio Code or PhpStorm** are the editors launched by the finished application. Neither is required to build the integration application itself.
- **WSL2** is a Linux environment that runs alongside Windows. For WSL paths, the application opens a remote folder in VS Code or uses the WSL network path in PhpStorm. Docker containers and Symfony belong to the user's project environment; this repository does not build them.

## Typical build workflow

To build just the application, as described in `AGENTS.md`:

```powershell
dotnet build .\src\SymfonyIdeIntegration\SymfonyIdeIntegration.csproj
```

This checks that the C# project builds and writes output to `src/SymfonyIdeIntegration/bin/`. It does not create an installer or change `VERSION`.

To build the installer, which publishes a self-contained Windows x64 application and increments the version on success:

```powershell
.\build-installer.ps1
```

This requires the .NET 10 SDK and Inno Setup. The output is `artifacts/SymfonyIdeIntegration-<version>-Setup-x64.exe`. For portable tools or custom tool locations, see `AGENTS.md` and `README.md`. Before running a release build, remember that it changes `VERSION` and check that the installer filename does not already exist in `artifacts/`.

## Where to start when making a change

- Link parsing or validation: start with `EditorLink.cs`.
- Editor launch arguments: `EditorLauncher.cs`; executable discovery: `EditorDiscovery.cs`.
- Settings or tray menu: `UserSettings.cs`, `SettingsDialog.cs`, and `TrayApplication.cs`.
- Logging errors: `IntegrationLog.cs` and error handling in `Program.cs`.
- Tray icon or About window: `TrayIcons.cs`, `AboutDialog.cs`, `BridgeScene.cs`, `PilotArm.cs`, and the `Art/` resources.
- Installer behavior or release versioning: `installer/SymfonyIdeIntegration.iss`, `build-installer.ps1`, and `VERSION`.
- User instructions and Symfony link format: `README.md`.

After following the change through the code, run the build command in `AGENTS.md` if you need to confirm the application compiles. Building the installer also increments the version.

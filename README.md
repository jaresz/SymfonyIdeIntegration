# Symfony IDE Integration

This project opens files referenced by Symfony links in the correct project and at the specified line. The `Q3a-Symfony2ide` protocol supports projects running in Docker on WSL2 (Visual Studio Code or PhpStorm) as well as Linux projects reached through Visual Studio Code Remote - SSH and local Windows copies synchronized with a remote server through PhpStorm.

## License

Copyright (C) 2026 Jakub Reiter. This project is licensed under the GNU General Public License, version 3 only. See [LICENSE](LICENSE) for the full license text.

For an overview of directories, source files, and developer tools, see the [project guide](PROJECT-GUIDE.md).

## Windows 11 application

1. Run **SymfonyIdeIntegration-<version>-Setup-x64.exe**. It installs the application for the current user and registers `Q3a-Symfony2ide://`. You can disable startup at sign-in in the installer.
2. Click the **Symfony IDE Integration** tray icon. Windows may place it in the hidden icons menu. If automatic editor detection does not work, open **Settings…** and use **Browse…** to locate `Code.exe` or `phpstorm64.exe`. Save the settings.
3. Select **Visual Studio Code** or **PhpStorm** from the tray menu. The icon changes immediately, and the editor selection applies to the next link and persists after closing the menu. If the editor is unavailable, the tray shows a **VS** or **PS** icon. You can launch the tray app again from the Start menu. Find the application version under **About…**.

Uninstall the application from **Settings → Apps → Installed apps**. If it is running, select **Exit** from the tray menu first. The uninstaller removes the `Q3a-Symfony2ide` protocol and startup entry. Settings and `integration.log` remain in `%LOCALAPPDATA%\SymfonyIdeIntegration`.

If `settings.json` is damaged, the application starts with default settings. Open **Settings…**, fill in the fields, and select **Save**. The application keeps the damaged file as `settings.corrupt-<identifier>.json` beside the new `settings.json`.

The log records the date, event type, and editor name or error type when applicable. The application removes entries older than 7 days and trims the oldest entries to keep the file under 1 MiB. It checks the log when writing and every minute while the tray app is running.

### Building the installer

On Windows x64 with the .NET 10 SDK and Inno Setup 6.3 or later, run this in PowerShell:

```powershell
.\build-installer.ps1
```

The script builds the next version according to [SemVer](https://semver.org/). By default, it increments `PATCH` after every successful installer build, including builds of unchanged code. Use `-Bump Minor` for backward-compatible features and `-Bump Major` for breaking changes. The script writes the new version to `VERSION` after creating the installer. Include the `VERSION` change in the release commit.

The installer is created at `artifacts\SymfonyIdeIntegration-<version>-Setup-x64.exe`. Previous packages remain in this directory. If you use a portable SDK or a custom Inno Setup location, provide paths with the `-DotNet` and `-InnoSetupCompiler` parameters. The package includes the .NET runtime required to run the application.
```ps
.\build-installer.ps1 -DotNet "artifacts\tools\dotnet\dotnet.exe" -InnoSetupCompiler "artifacts\tools\inno\ISCC.exe"
```

To build only the application using the version in `VERSION`, run `dotnet build .\src\SymfonyIdeIntegration\SymfonyIdeIntegration.csproj`.

## Symfony configuration

In `config/packages/framework.yaml`, add the `ide` setting to the existing `framework` section.

For remote projects, `connect_to` accepts `WSL` and `SSH` in any letter case. Query parameters can appear in any order.

### Projects in Docker on WSL2

```yaml
framework:
    ide: 'Q3a-Symfony2ide://open/?connect_to=WSL&dist=Ubuntu&project_path=/home/z0/projects/ecp2&file=%%f&line=%%l'
```

Set `dist` to the WSL distribution name and `project_path` to the absolute project directory in that distribution. Symfony fills in `%%f` with the container file path and `%%l` with the line number. The application maps a path such as `/var/www/html/src/Controller/HomeController.php` to `/home/z0/projects/ecp2/src/Controller/HomeController.php`. VS Code requires the [WSL extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-wsl).

If you used a link with `project=ecp2`, update `framework.ide` to the format above. Set `project_path` to the former WSL projects directory followed by the project name, and copy the former WSL distribution into `dist`.

### Projects on a Linux host over SSH

Install the [Remote - SSH extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-ssh), configure an SSH host alias such as `symfony-dev`, and select **Visual Studio Code** in the tray menu. Use the alias from your SSH configuration and the absolute project directory as seen on the SSH host.

```yaml
framework:
    ide: 'Q3a-Symfony2ide://open/?connect_to=SSH&ssh_host=symfony-dev&project_path=/srv/projects/shop&file=%%f&line=%%l'
```

Set `ssh_host` to your SSH alias and `project_path` to the absolute project directory on that host. Symfony supplies the file path directly from the host, such as `/srv/projects/shop/src/Controller/HomeController.php`. Clicking a link opens the remote project folder and the referenced file at its line in VS Code.

### Local Windows projects synchronized through PhpStorm

If you already have `phpstorm://open?url=file://…` links, replace `phpstorm://` with `Q3a-Symfony2ide://` in `framework.ide`. In the mapping at the end of the link, put the server directory before `>` and the local project directory after it. Symfony uses the mapping to generate a Windows file path.

```yaml
framework:
    ide: 'Q3a-Symfony2ide://open?url=file://%%f&line=%%l&/var/www/crooked_vase_simulator/>C:\Users\stefan\PhpstormProjects\crookedVaseSimulator/'
```

With the `url=file://…` format, the application searches upward for the nearest parent directory containing `.idea`. To specify the project directory explicitly, use `projectDir=<project directory>` with `file=%%f` or `url=file://%%f`. The file opens in the editor selected in the tray. PhpStorm remains responsible for synchronizing the project with the server.

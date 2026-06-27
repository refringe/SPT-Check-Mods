# SPT Check Mods

A .NET 9 console application that validates Single Player Tarkov (SPT) mod compatibility using the Forge API.

<img width="1013" height="314" alt="image" src="https://github.com/user-attachments/assets/00878387-024c-4961-b66f-b977f4e550c0" />

## Features

- **Forge API Integration**: Verifies mods against the official SPT Forge database
- **Version Compatibility**: Checks installed mod versions against SPT version requirements
- **Update Detection**: Identifies mods with available updates and provides download links
- **Bulk Update Pages**: Opens every out-of-date mod's Forge page in your browser from the end-of-run menu
- **Dependency Analysis**: Builds dependency trees, identifies missing dependencies, and detects conflicts
- **Dependency Change Notices**: When an update is available, shows the dependencies it adds or removes, flagging ones you'll need to download or update
- **Installation Checks**: Detects mods installed in the wrong folder and excludes them from the rest of the run
- **Dismissable Update Prompts**: Lets you ignore false-positive "update available" prompts for mods whose files are already current, with an optional shared community list
- **SPT Update Checking**: Notifies you when a new SPT version is available
- **Self-Update Checking**: Notifies you when a newer version of Check Mods is available

## Requirements

- A valid SPT 4.0+ installation
- .NET 9 SDK or a published Check Mods binary for your operating system

## Installation

### Option 1: Download Release

Download the latest release (`CheckMods-win-x64.exe`) from the [Releases](https://github.com/refringe/SPT-Check-Mods/releases) page, then move it into the root of your SPT installation directory. Running it from there checks the mods in that installation.

### Option 2: Build from Source

```bash
git clone https://github.com/refringe/SPT-Check-Mods.git
cd SPT-Check-Mods
dotnet build
```

## Usage

If you downloaded the Windows release executable and placed it in your SPT installation directory, run it from there:

```bash
CheckMods-win-x64.exe
```

It checks the mods in the current directory. You can also point it at an SPT installation elsewhere by passing the path:

```bash
CheckMods-win-x64.exe "C:\path\to\spt"
```

If you built from source, use `dotnet run` instead. The `--` passes the path through to the application rather than to the .NET CLI:

```bash
# Run from your SPT installation directory
dotnet run

# Or specify the SPT path as an argument
dotnet run -- /path/to/spt
```

### Linux dedicated-server usage

On Linux, run Check Mods from the project checkout or from a published Linux binary and pass the base SPT installation directory explicitly. Use the directory that contains the nested `SPT/` folder, not the nested folder itself:

```bash
# Source checkout form
dotnet run -- /path/to/your/spt

# Published binary form; adjust the binary name/path for your publish output
./CheckMods-linux-x64 /path/to/your/spt
```

A valid dedicated-server layout must include the SPT server core assembly at `SPT/SPTarkov.Server.Core.dll` beneath the installation root and server mods under `SPT/user/mods`. Client plugins under `BepInEx/plugins` are optional for server-only Linux checks; when that directory is absent, Check Mods should report the missing client plugin directory as a non-fatal condition and continue with server mods only.

## Troubleshooting

### Missing or incomplete SPT path

If Check Mods reports that it cannot find `SPTarkov.Server.Core.dll`, verify that the path argument points at the base SPT installation directory, not the nested `SPT/` folder. For example, if your base SPT directory is `/path/to/your/spt`, the expected file is `/path/to/your/spt/SPT/SPTarkov.Server.Core.dll`.

### Server-only Linux checks

Dedicated Linux servers may not have `BepInEx/plugins`. That limits client plugin scanning only; server mod scanning from `SPT/user/mods` should still proceed.

## Configuration

### Local Storage

Check Mods keeps its data under `%APPDATA%\SptCheckMods` on Windows. On Linux, .NET resolves the application data directory according to the host environment.

- **Logs**: `%APPDATA%\SptCheckMods\logs\checkmod.log` on Windows
- **Ignored updates**: `%APPDATA%\SptCheckMods\ignored-updates.json` on Windows

### Supported Mod Formats

- **Server Mods**: SPT mods with `AbstractModMetadata` in `SPT/user/mods`
- **Client Mods**: BepInEx plugins with `BepInPlugin` attribute in `BepInEx/plugins`

## Contributing

Please read [CONTRIBUTING.md](.github/CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Security

For security concerns, please review our [Security Policy](.github/SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

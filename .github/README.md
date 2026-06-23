# SPT Check Mods

A .NET 9 console application that validates Single Player Tarkov (SPT) mod compatibility using the Forge API.

<img width="1013" height="314" alt="image" src="https://github.com/user-attachments/assets/00878387-024c-4961-b66f-b977f4e550c0" />

## Features

- **Forge API Integration**: Verifies mods against the official SPT Forge database
- **Version Compatibility**: Checks installed mod versions against SPT version requirements
- **Update Detection**: Identifies mods with available updates and provides download links
- **Bulk Update Pages**: Opens every out-of-date mod's Forge page in your browser from the end-of-run menu
- **Dependency Analysis**: Builds dependency trees, identifies missing dependencies, and detects conflicts
- **Installation Checks**: Detects mods installed in the wrong folder and excludes them from the rest of the run
- **Dismissable Update Prompts**: Lets you ignore false-positive "update available" prompts for mods whose files are already current, with an optional shared community list
- **SPT Update Checking**: Notifies you when a new SPT version is available
- **Self-Update Checking**: Notifies you when a newer version of Check Mods is available

## Requirements

- A valid SPT 4.0+ installation

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

If you downloaded the release executable and placed it in your SPT installation directory, run it from there:

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

## Configuration

### Local Storage
Check Mods keeps its data under `%APPDATA%\SptCheckMods`:

- **Logs**: `%APPDATA%\SptCheckMods\logs\checkmod.log`
- **Ignored updates**: `%APPDATA%\SptCheckMods\ignored-updates.json`

### Supported Mod Formats
- **Server Mods**: SPT mods with `AbstractModMetadata` in `SPT/user/mods`
- **Client Mods**: BepInEx plugins with `BepInPlugin` attribute in `BepInEx/plugins`

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Security

For security concerns, please review our [Security Policy](SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

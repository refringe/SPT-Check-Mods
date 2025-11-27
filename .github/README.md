# SPT Check Mods

A .NET 10 console application that validates Single Player Tarkov (SPT) mod compatibility using the Forge API.

<img width="1013" height="314" alt="image" src="https://github.com/user-attachments/assets/00878387-024c-4961-b66f-b977f4e550c0" />

## Features

- **Forge API Integration**: Verifies mods against the official SPT Forge database
- **Version Compatibility**: Checks installed mod versions against SPT version requirements
- **Update Detection**: Identifies mods with available updates and provides download links
- **Dependency Analysis**: Builds dependency trees, identifies missing dependencies, and detects conflicts
- **SPT Update Checking**: Notifies you when a new SPT version is available

## Requirements

- Valid SPT installation
- [Forge API key](https://forge.sp-tarkov.com/user/api-tokens) with read permissions

## Installation

### Option 1: Download Release
Download the latest release from the [Releases](https://github.com/refringe/SPT-Check-Mods/releases) page.

### Option 2: Build from Source
```bash
git clone https://github.com/refringe/SPT-Check-Mods.git
cd SPT-Check-Mods
dotnet build
```

## Usage

### Basic Usage
```bash
# Run from your SPT installation directory
dotnet run

# Or specify the SPT path as an argument
dotnet run /path/to/spt
```

### First Run
On first run, you'll be prompted to enter your Forge API key. The key is securely stored for future use.

## Architecture

SPT Mod Checker uses a pipeline-based service architecture with dependency injection:

- **ApplicationService**: Main orchestrator that coordinates the entire workflow
- **ServerModService**: Validates SPT installation and extracts SPT version from `SPTarkov.Server.Core.dll`
- **ModScannerService**: Scans both server (`SPT/user/mods`) and client (`BepInEx/plugins`) mods
- **ModReconciliationService**: Matches server/client components of the same mod
- **ModMatchingService**: Matches local mods with the Forge API using GUID and fuzzy name matching
- **ModEnrichmentService**: Enriches matched mods with version data from the API
- **ModDependencyService**: Analyzes dependency trees and identifies missing/conflicting dependencies
- **ForgeApiService**: HTTP client for the Forge API
- **RateLimitService**: Prevents API throttling

## Configuration

### API Key Storage
The Forge API key is stored in: `%APPDATA%\SptModChecker\apikey.txt`

### Log Files
Application logs are stored in: `%APPDATA%\SptModChecker\logs\checkmod.log`

### Supported Mod Formats
- **Server Mods**: SPT mods with `AbstractModMetadata` in `SPT/user/mods`
- **Client Mods**: BepInEx plugins with `BepInPlugin` attribute in `BepInEx/plugins`

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Security

For security concerns, please review our [Security Policy](SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

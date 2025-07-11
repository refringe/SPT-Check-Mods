# CheckMods

A .NET 9.0 console application that validates Single Player Tarkov mod compatibility using the Forge API.

<img width="1013" height="314" alt="image" src="https://github.com/user-attachments/assets/00878387-024c-4961-b66f-b977f4e550c0" />

## Features

- **Server Mod Validation**: Scans `user/mods` directory for server-side mods
- **Client Mod Detection**: Scans and analyzes `BepInEx/plugins` directory for client-side mods
- **Fuzzy Matching**: Intelligent mod name matching for accurate identification
- **Version Compatibility**: Checks mod version requirements against SPT version
- **Rate Limited API**: Respects forge.sp-tarkov.com API limits with built-in rate limiting

## Requirements

- .NET 9.0 Runtime
- Valid SPT installation
- [Forge API key](https://forge.sp-tarkov.com/user/api-tokens)

## Installation

### Option 1: Download Release
Download the latest release from the [Releases](https://github.com/refringe/SPT-Check-Mods/releases) page.

### Option 2: Build from Source
```bash
git clone https://github.com/refringe/CheckMods.git
cd CheckMods
dotnet build
```

## Usage

### Basic Usage
```bash
# Run with current directory as SPT path
dotnet run

# Run with specific SPT path
dotnet run /path/to/spt
```

## Architecture

CheckMods follows a service-oriented architecture with dependency injection:

- **ApplicationService**: Main orchestrator
- **ModService**: Server mod validation and scanning
- **ClientModService**: BepInEx client mod detection
- **ForgeApiService**: API communication with caching
- **RateLimitService**: API rate limiting

## Configuration

### API Key Storage
The Forge API key is stored in: `%APPDATA%\SptModChecker\apikey.txt`

### Supported Mod Formats
- **Server Mods**: Standard SPT mod structure in `user/mods`
- **Client Mods**: BepInEx plugins in `BepInEx/plugins`

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Security

For security concerns, please review our [Security Policy](SECURITY.md).

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

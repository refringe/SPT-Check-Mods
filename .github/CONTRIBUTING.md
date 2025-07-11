# Contributing to CheckMods

Thank you for your interest in contributing to CheckMods! This document provides guidelines for contributing to the project.

## Code of Conduct

This project adheres to the Contributor Covenant [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Git
- A valid SPT installation for testing

### Development Setup
1. Fork the repository
2. Clone your fork: `git clone https://github.com/your-username/CheckMods.git`
3. Navigate to the project: `cd CheckMods`
4. Restore dependencies: `dotnet restore`
5. Build the project: `dotnet build`

## Development Guidelines

### Code Style
- Follow standard C# conventions
- Use meaningful variable and method names
- Add XML documentation for public methods
- Keep methods focused and concise

### Architecture Patterns
- Use dependency injection for services
- Follow the existing service-oriented architecture
- Register services as singletons in `Program.cs`
- Use async/await for I/O operations

### Testing
- Test your changes with various SPT installations
- Verify both server and client mod detection
- Test with different mod configurations
- Ensure rate limiting works correctly

## Submitting Changes

### Pull Request Process
1. Create a feature branch: `git checkout -b feature/your-feature-name`
2. Make your changes following the guidelines above
3. Test thoroughly
4. Commit with clear, descriptive messages
5. Push to your fork: `git push origin feature/your-feature-name`
6. Create a Pull Request

### Pull Request Guidelines
- Provide a clear description of the changes
- Reference any related issues
- Include screenshots for UI changes
- Update documentation, if needed

### Commit Messages
Use clear, descriptive commit messages:
- `feat: add client mod version validation`
- `fix: resolve path traversal vulnerability`
- `docs: update API documentation`
- `refactor: improve mod scanning performance`

## Types of Contributions

### Issues
- Check existing issues before creating new ones
- Provide clear reproduction steps
- Include system information (OS, .NET version, SPT version)

### New Features
- Discuss major features in an issue first
- Ensure features align with project goals
- Maintain backward compatibility when possible

### Documentation
- Fix typos and improve clarity
- Add examples for complex features

## Development Areas

### Priority Areas
- Performance improvements for large mod collections
- Enhanced mod matching
- Improved error handling and user feedback

### Technical Debt
- Test coverage
- Code organization improvements
- Dependency updates
- Performance optimizations

## Questions and Support

- Create an issue for bug reports
- Use discussions for questions
- Check existing issues before creating new ones

## Recognition

Contributors will be acknowledged in release notes. Significant contributions may result in maintainer status.

Thank you for contributing!

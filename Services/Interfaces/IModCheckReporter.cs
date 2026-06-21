namespace CheckMods.Services.Interfaces;

/// <summary>
/// Renders the user-facing output of the mod-check workflow. This is the single boundary between workflow logic and
/// the console, so the orchestrator and services stay free of direct console dependencies (and remain testable).
/// </summary>
public interface IModCheckReporter
{
    /// <summary>Writes the application banner and introductory information.</summary>
    void Banner();

    /// <summary>Writes a horizontal rule separator.</summary>
    void Rule();

    /// <summary>Writes a blank line.</summary>
    void Blank();
}

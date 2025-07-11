namespace CheckMods.Models;

/// <summary>
/// Extension methods for the ModStatus enum to provide display formatting. Uses Spectre.Console markup for colored
/// console output.
/// </summary>
public static class ModStatusExtensions
{
    /// <summary>
    /// Converts a ModStatus enum value to a formatted display string with color markup.
    /// </summary>
    /// <param name="status">The ModStatus to format.</param>
    /// <returns>A formatted string with Spectre.Console color markup.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the status is not a valid ModStatus value.</exception>
    public static string ToDisplayString(this ModStatus status) => status switch
    {
        ModStatus.Verified => "[green]Verified[/]",
        ModStatus.NoMatch => "[red]No Match[/]",
        ModStatus.Incompatible => "[maroon]Incompatible[/]",
        ModStatus.InvalidVersion => "[red]Invalid SPT Version Range[/]",
        ModStatus.NeedsConfirmation => "[yellow]Needs Confirmation[/]",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
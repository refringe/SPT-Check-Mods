namespace CheckMods.Models;

/// <summary>
/// Represents a mod discovered in the wrong installation directory.
/// </summary>
/// <param name="IsServerMod">
/// True when the DLL is a server mod (and was found in the client folder); false when it is a client mod (and was found
/// in the server folder).
/// </param>
/// <param name="Guid">The mod's GUID, if it could be read.</param>
/// <param name="Name">The mod's name, if it could be read.</param>
/// <param name="Version">The mod's version, if it could be read.</param>
/// <param name="FilePath">The full path to the misplaced DLL.</param>
public sealed record MisplacedMod(bool IsServerMod, string Guid, string Name, string Version, string FilePath);

/// <summary>
/// A single BepInEx/plugins subdirectory that holds two or more unrelated mods. The signature of one mod having been
/// copied into another mod's folder.
/// </summary>
/// <param name="Directory">The full path to the offending plugins subdirectory.</param>
/// <param name="Misplaced">
/// The mod(s) identified as not belonging in this folder. Empty when blame could not be attributed (see
/// <paramref name="Ambiguous"/>).
/// </param>
/// <param name="Mods">Every mod found in the directory, used to describe the conflict when it is ambiguous.</param>
/// <param name="Ambiguous">
/// True when it cannot be determined which mod is the intruder (two unrelated mods with no other signal), in which case
/// the user is pointed at the directory and asked to review all of its mods.
/// </param>
public sealed record CrossInstalledDirectory(
    string Directory,
    IReadOnlyList<MisplacedMod> Misplaced,
    IReadOnlyList<MisplacedMod> Mods,
    bool Ambiguous
);

/// <summary>
/// The full result of misplaced-mod detection: mods in the wrong top-level folder, plus plugins subdirectories that
/// contain unrelated mods.
/// </summary>
/// <param name="WrongFolder">
/// Mods found in the wrong installation folder entirely (a server mod under BepInEx/plugins, or a client mod under
/// SPT/user/mods).
/// </param>
/// <param name="CrossInstalled">Plugins subdirectories that contain two or more unrelated mods.</param>
public sealed record MisplacedModReport(
    IReadOnlyList<MisplacedMod> WrongFolder,
    IReadOnlyList<CrossInstalledDirectory> CrossInstalled
)
{
    /// <summary>True when any misplaced or cross-installed mods were detected.</summary>
    public bool Any
    {
        get { return WrongFolder.Count > 0 || CrossInstalled.Count > 0; }
    }

    /// <summary>
    /// Exact DLL paths to drop from the remaining checks: every wrong-folder mod, plus the identified intruder(s) in
    /// each non-ambiguous cross-installed directory. Ambiguous directories are excluded by folder instead (see
    /// <see cref="ExcludedDirectories"/>).
    /// </summary>
    public IReadOnlyList<string> ExcludedFilePaths
    {
        get
        {
            return WrongFolder
                .Select(mod => mod.FilePath)
                .Concat(
                    CrossInstalled
                        .Where(directory => !directory.Ambiguous)
                        .SelectMany(directory => directory.Misplaced)
                        .Select(mod => mod.FilePath)
                )
                .ToList();
        }
    }

    /// <summary>
    /// Cross-installed directories whose intruder could not be identified. Every mod inside such a folder is dropped
    /// from the remaining checks, since none of them can be trusted to be correctly placed.
    /// </summary>
    public IReadOnlyList<string> ExcludedDirectories
    {
        get
        {
            return CrossInstalled
                .Where(directory => directory.Ambiguous)
                .Select(directory => directory.Directory)
                .ToList();
        }
    }
}

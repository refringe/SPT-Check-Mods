# Mod Version or Data Mismatch

If Check Mods is reporting incorrect version information, a false compatibility warning, or a mod mismatch, **this is not a bug in Check Mods**. This is a data issue that must be resolved by the mod author.

## How Check Mods Works

Check Mods validates your installed mods by comparing local mod metadata against data published on the [SPT Forge](https://forge.sp-tarkov.com) API. The version numbers, compatibility ranges, and dependency information displayed by Check Mods come directly from what each mod author has published on Forge.

Check Mods cannot correct or override this data. It can only report what it finds.

## What To Do

If you believe a mod's version data is incorrect:

1. **Identify the mod** shown in the Check Mods output that has the mismatch.
2. **Visit the mod's Forge page** and verify the version and compatibility information listed there.
3. **Contact the mod author** through the mod's Forge page or the [SPT Discord](https://discord.com/invite/Xn9msqQZan) and let them know their published metadata needs to be updated.

The mod author is the only person who can update the version, compatibility, and dependency data for their mod on Forge.

## Common Scenarios

- **"Mod shows as outdated but I have the latest version"** -- The mod author may not have updated their Forge listing to reflect the latest release.
- **"Mod shows as incompatible but it works fine"** -- The mod author may not have updated their SPT version compatibility range on Forge.
- **"Mod is not found on Forge"** -- The mod may not be published on Forge, or its GUID/name may not match the Forge listing.
- **"Wrong dependency information"** -- The mod author needs to update their dependency declarations on Forge.

## When To Open an Issue

Please **do** open a [bug report](https://github.com/refringe/SPT-Check-Mods/issues/new?template=bug_report.yml) if:

- Check Mods crashes or produces an error.
- Check Mods is misreading local mod files (wrong name, wrong version from disk).
- The application behaves unexpectedly regardless of Forge data accuracy.

These are actual bugs and we want to hear about them.

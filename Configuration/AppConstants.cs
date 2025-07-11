using CheckMods.Models;

namespace CheckMods.Configuration;

/// <summary>
/// Application constants including blacklisted mods and mod name/author update mappings. Used to handle edge cases
/// where mod names or authors have changed over time. This will be heavily used to improve API matching until 4.0 is
/// released, and mods are updated to include a better local ID system.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// List of server mods that are blacklisted and should not be checked against the Forge API. These mods are either
    /// not available on Forge or are internal/helper mods.
    /// </summary>
    public static readonly List<(string Name, string Author)> BlacklistedMods = 
    [
        ("server", "Fika"), // Not on Forge
        ("Skills Extended", "dirtbikercj"), // Not on Forge
        ("ProfileEditorHelper", "SkiTles55"), // In-app helper mod, not on Forge
    ];
    
    /// <summary>
    /// List of client mods that are blacklisted and should not be checked against the Forge API. These include core
    /// SPT components, Fika components, and other internal/helper mods.
    /// </summary>
    public static readonly List<(string Name, string Author)> BlacklistedClientMods = 
    [
        ("core", "spt"), // Core SPT
        ("common", "spt"), // Core SPT
        ("debugging", "spt"), // Core SPT
        ("custom", "spt"), // Core SPT
        ("singleplayer", "spt"), // Core SPT
        ("core", "fika"), // Not on Forge
        ("HideSpecialIcon", ""), // In-app helper mod, not on Forge
        ("RemoveTimeGateFromQuests", "Cj"), // Not on Forge
        ("Skills Extended", "dirtbikercj"), // Not on Forge
        ("Sense", ""), // Not on Forge
        ("Hitmarker", ""), // Not on Forge
        ("exfilbots", ""), // Not on Forge
    ];

    /// <summary>
    /// List of server mod name/author updates for mods that have changed names or authors over time. Used to improve
    /// API matching by mapping old mod identifiers to current ones.
    /// </summary>
    public static readonly List<ModUpdateInfo> ModUpdates =
    [
        new ModUpdateInfo { FromName = "BRNVG - N-15 Adapter", FromAuthor = "Borkel", ToName = "Borkel's Realistic Night Vision Goggles (NVGs and T-7)", ToAuthor = "Borkel" },
        new ModUpdateInfo { FromName = "MOAR", FromAuthor = "DewardianDev", ToName = "MOAR + Bagels - Ultra lite spawn mod", ToAuthor = "DewardianDev" },
        new ModUpdateInfo { FromName = "MoreCheckmarksBackend", FromAuthor = "VIP", ToName = "MoreCheckmarks", ToAuthor = "VIPkiller17" },
        new ModUpdateInfo { FromName = "uifixes", FromAuthor = "Tyfon", ToName = "UI Fixes", ToAuthor = "Tyfon" },
        new ModUpdateInfo { FromName = "SAIN", FromAuthor = "zSolarint", ToName = "SAIN - Solarint's AI Modifications - Full AI Combat System Replacement", ToAuthor = "Solarint" },
        new ModUpdateInfo { FromName = "BOOBS", FromAuthor = "Jehree", ToName = "Balanced Overhaul Of Bullet Spawns (BOOBS)", ToAuthor = "Jehree" },
        new ModUpdateInfo { FromName = "OldPk06Remake", FromAuthor = "Boogle", ToName = "Old PK-06 Restoration", ToAuthor = "Boogle" },
        new ModUpdateInfo { FromName = "Bot Callsigns", FromAuthor = "Helldiver, harmony", ToName = "Bot Callsigns - Reloaded", ToAuthor = "harmony" },
        new ModUpdateInfo { FromName = "RheddElBozo - SPT Battlepass", FromAuthor = "RheddElBozo", ToName = "SPT Battlepass", ToAuthor = "Pluto!" },
        new ModUpdateInfo { FromName = "MarkedRoomLoot", FromAuthor = "Valens", ToName = "Make Marked Room Loot Great Again", ToAuthor = "Valens" },
        new ModUpdateInfo { FromName = "SVM", FromAuthor = "GhostFenixx", ToName = "Server Value Modifier [SVM]", ToAuthor = "GhostFenixx" },
        new ModUpdateInfo { FromName = "WTT-PackNStrap", FromAuthor = "", ToName = "WTT - Pack 'n' Strap", ToAuthor = "GrooveypenguinX" },
        new ModUpdateInfo { FromName = "Wisps-InfMeds", FromAuthor = "", ToName = "INFMEDS-Update BY WispsFlame", ToAuthor = "WispsFlame" },
        new ModUpdateInfo { FromName = "Rocka's Corner Store", FromAuthor = "RockaHorse", ToName = "WTT - Corner Store", ToAuthor = "rockahorse" },
        new ModUpdateInfo { FromName = "props-doorbreacher", FromAuthor = "Props", ToName = "Backdoor Bandit", ToAuthor = "MakerMacher" },
        new ModUpdateInfo { FromName = "ProfileEditorHelper", FromAuthor = "SkiTles55", ToName = "SPT-AKI Profile Editor", ToAuthor = "SkiTles55" },
        new ModUpdateInfo { FromName = "Painter - Trader", FromAuthor = "MoxoPixel", ToName = "Painter", ToAuthor = "MoxoPixel" },
        new ModUpdateInfo { FromName = "TraderModding", FromAuthor = "ChooChoo", ToName = "Trader Modding And Improved Weapon Building", ToAuthor = "ChooChoo" },
        new ModUpdateInfo { FromName = "croupier", FromAuthor = "turbodestroyer_1337", ToName = "Croupier (random loadouts + flea quicksell)", ToAuthor = "turbodestroyer" },
        new ModUpdateInfo { FromName = "Echoes of Tarkov - Requisitions", FromAuthor = "RheddElBozo", ToName = "Echoes of Tarkov - Requisitions", ToAuthor = "Pluto!" },
        new ModUpdateInfo { FromName = "Weapons", FromAuthor = "EpicRangeTime", ToName = "Epic's All in One", ToAuthor = "EpicRangeTime" },
        new ModUpdateInfo { FromName = "revival-mod", FromAuthor = "KaikiNoodles", ToName = "RevivalMod: Second Chance Survival System for Single Player Tarkov", ToAuthor = "KaikiNoodles" },
        new ModUpdateInfo { FromName = "LiveBitcoinPricesREDUX", FromAuthor = "Shardbyte", ToName = "LiveBitcoinPricesREDUX", ToAuthor = "AmightyTank" },
        new ModUpdateInfo { FromName = "DeDistortionizer", FromAuthor = "zSolarint", ToName = "DeDistortionizer", ToAuthor = "Hauzman" },
        new ModUpdateInfo { FromName = "SPT-InsuranceFraud", FromAuthor = "DragonX86", ToName = "Insurance Fraud (Port to 3.11)", ToAuthor = "DragonX86" },
        new ModUpdateInfo { FromName = "WeightlessAmmo", FromAuthor = "MNSTR", ToName = "Weight-Less-Ammo", ToAuthor = "Turok" },
        new ModUpdateInfo { FromName = "The Blacklist", FromAuthor = "Platinum", ToName = "The Blacklist - flea market enhancements", ToAuthor = "Platinum" },
        new ModUpdateInfo { FromName = "Wolfiks Heavy Troopers", FromAuthor = "SerWolfik, reuploaded by AMightyTank", ToName = "Wolfik's Heavy Trooper Masks - Reupload", ToAuthor = "AmightyTank" },
        new ModUpdateInfo { FromName = "MusicManiac-KeysInLoot", FromAuthor = "MusicManiac", ToName = "Keys In Loot (KIL)", ToAuthor = "MusicManiac" },
        new ModUpdateInfo { FromName = "PoseServComp", FromAuthor = "", ToName = "More Mannequin Pose", ToAuthor = "Choccy Milk" },
        new ModUpdateInfo { FromName = "GuidingLight", FromAuthor = "", ToName = "Guiding Light", ToAuthor = "LightoftheWorld" },
        new ModUpdateInfo { FromName = "AES", FromAuthor = "Flowless", ToName = "AES (Ultimate Questing Traders)", ToAuthor = "Flowless" },
        new ModUpdateInfo { FromName = "no-fir-hideout", FromAuthor = "schkuromi", ToName = "No FIR Hideout", ToAuthor = "sch_kuromi" },
        new ModUpdateInfo { FromName = "LootValueBackend", FromAuthor = "IhanaMies", ToName = "lootvalue", ToAuthor = "IhanaMies" },
        new ModUpdateInfo { FromName = "Adam's Boxes at Ref (BARF)", FromAuthor = "", ToName = "Boxes at ReF (BARF)", ToAuthor = "AcidMC" },
        new ModUpdateInfo { FromName = "shiny_airdrop_guns", FromAuthor = "leaves", ToName = "Shiny Airdrop Guns!", ToAuthor = "DeadLeaves" },
        new ModUpdateInfo { FromName = "ReflexSightsRework", FromAuthor = "SamSwat", ToName = "Reflex Sights Rework - Updated", ToAuthor = "stckytwl" },
        new ModUpdateInfo { FromName = "no-pmcbot-response", FromAuthor = "schkuromi", ToName = "No PMC Bot Response", ToAuthor = "sch_kuromi" },
    ];
    
    /// <summary>
    /// List of client mod name/author updates for mods that have changed names or authors over time. Used to improve
    /// API matching by mapping old mod identifiers to current ones.
    /// </summary>
    public static readonly List<ModUpdateInfo> ClientModUpdates =
    [
        new ModUpdateInfo { FromName = "Graphics", FromAuthor = "", ToName = "Amands's Graphics", ToAuthor = "Amands2Mello" },
        new ModUpdateInfo { FromName = "FOVFix", FromAuthor = "", ToName = "Fontaine's FOV Fix", ToAuthor = "Fontaine" },
        new ModUpdateInfo { FromName = "TraderScrolling", FromAuthor = "Kaeno", ToName = "Kaeno-TraderScrolling", ToAuthor = "CWX" },
        new ModUpdateInfo { FromName = "LootingBots", FromAuthor = "", ToName = "Looting Bots", ToAuthor = "Skwizzy" },
        new ModUpdateInfo { FromName = "GildedKeyStorage", FromAuthor = "DrakiaXYZ", ToName = "Gilded Key Storage", ToAuthor = "Jehree" },
        new ModUpdateInfo { FromName = "UseLooseLoot", FromAuthor = "SPT", ToName = "Use Loose Loot", ToAuthor = "gaylatea" },
        new ModUpdateInfo { FromName = "FlareEventNotifier", FromAuthor = "Terkoiz", ToName = "Exfil Flare Notification", ToAuthor = "Terkoiz" },
        new ModUpdateInfo { FromName = "SetSpeed", FromAuthor = "DrakiaXYZ", ToName = "Set Speed - Set Player Speed with Hotkeys", ToAuthor = "DrakiaXYZ" },
        new ModUpdateInfo { FromName = "Zones", FromAuthor = "VCQL", ToName = "Virtual's Custom Quest Loader", ToAuthor = "Virtual" },
        new ModUpdateInfo { FromName = "ContinuousLoadAmmo", FromAuthor = "", ToName = "Continuous Load Ammo", ToAuthor = "ozen" },
        new ModUpdateInfo { FromName = "Choo² Trader Modding", FromAuthor = "", ToName = "Trader Modding And Improved Weapon Building", ToAuthor = "ChooChoo" },
        new ModUpdateInfo { FromName = "SPTLeftStanceWallFix", FromAuthor = "", ToName = "Left Stance Wall Fix", ToAuthor = "pein" },
        new ModUpdateInfo { FromName = "RamCleanerInterval", FromAuthor = "", ToName = "Ram Cleaner Fix", ToAuthor = "Devraccoon" },
        new ModUpdateInfo { FromName = "AILimit", FromAuthor = "dvize", ToName = "Ai Limit", ToAuthor = "wizard83" },
        new ModUpdateInfo { FromName = "RevivalMod", FromAuthor = "", ToName = "SPT Leaderboard", ToAuthor = "harmony" },
        new ModUpdateInfo { FromName = "Adds the Skeleton Key Item", FromAuthor = "", ToName = "Skeleton Key", ToAuthor = "Boogle" },
        new ModUpdateInfo { FromName = "ActuallyFoundInRaid", FromAuthor = "privateryan", ToName = "Actually Found In Raid (UPDATED)", ToAuthor = "RuKira" },
        new ModUpdateInfo { FromName = "LootValue", FromAuthor = "IhanaMies", ToName = "lootvalue", ToAuthor = "IhanaMies" },
        new ModUpdateInfo { FromName = "Dynamic External Resolution", FromAuthor = "", ToName = "Dynamic External Resolution Patch (DERP) 3.11 port", ToAuthor = "Sh1ba" },
        new ModUpdateInfo { FromName = "UnderFire", FromAuthor = "rpmwpm", ToName = "UnderFire - An Adrenaline Effect", ToAuthor = "rpmwpm" },
        new ModUpdateInfo { FromName = "DeHazardifier", FromAuthor = "Tetris", ToName = "DeHazardifier - Updated by Tetris", ToAuthor = "TetrisGG" },
    ];
}
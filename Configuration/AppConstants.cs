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
        ("ProfileEditorHelper", "SkiTles55"), // In-app helper mod, not on Forge
        ("RemoveTimeGateFromQuests", "Cj"), // Not on Forge
        ("server", "Fika"), // Not on Forge
        ("SERVPH's Tarky Menu", ""), // Not on Forge
        ("Skills Extended", "dirtbikercj"), // Not on Forge
    ];

    /// <summary>
    /// List of client mods that are blacklisted and should not be checked against the Forge API. These include core
    /// SPT components, Fika components, and other internal/helper mods.
    /// </summary>
    public static readonly List<(string Name, string Author)> BlacklistedClientMods =
    [
        ("common", "spt"), // Core SPT
        ("core", "fika"), // Not on Forge
        ("core", "spt"), // Core SPT
        ("custom", "spt"), // Core SPT
        ("debugging", "spt"), // Core SPT
        ("exfilbots", "phen"), // Not on Forge
        ("HideSpecialIcon", ""), // In-app helper mod, not on Forge
        ("Hitmarker", ""), // Not on Forge
        ("RemoveTimeGateFromQuests", "Cj"), // Not on Forge
        ("Sense", ""), // Not on Forge
        ("singleplayer", "spt"), // Core SPT
        ("Skills Extended", "dirtbikercj"), // Not on Forge
    ];

    /// <summary>
    /// List of server mod name/author updates for mods that have changed names or authors over time. Used to improve
    /// API matching by mapping old mod identifiers to current ones.
    /// </summary>
    public static readonly List<ModUpdateInfo> ModUpdates =
    [
        new()
        {
            FromName = "Adam's Boxes at Ref (BARF)",
            FromAuthor = "Adam",
            ToName = "Boxes at ReF (BARF)",
            ToAuthor = "AcidMC",
        },
        new()
        {
            FromName = "AES",
            FromAuthor = "Flowless",
            ToName = "AES (Ultimate Questing Traders)",
            ToAuthor = "Flowless",
        },
        new()
        {
            FromName = "BOOBS",
            FromAuthor = "Jehree",
            ToName = "Balanced Overhaul Of Bullet Spawns (BOOBS)",
            ToAuthor = "Jehree",
        },
        new()
        {
            FromName = "Bot Callsigns",
            FromAuthor = "Helldiver, harmony",
            ToName = "Bot Callsigns - Reloaded",
            ToAuthor = "harmony",
        },
        new()
        {
            FromName = "BRNVG - N-15 Adapter",
            FromAuthor = "Borkel",
            ToName = "Borkel's Realistic Night Vision Goggles (NVGs and T-7)",
            ToAuthor = "Borkel",
        },
        new()
        {
            FromName = "croupier",
            FromAuthor = "turbodestroyer_1337",
            ToName = "Croupier (random loadouts + flea quicksell)",
            ToAuthor = "turbodestroyer",
        },
        new()
        {
            FromName = "DeDistortionizer",
            FromAuthor = "zSolarint",
            ToName = "DeDistortionizer",
            ToAuthor = "Hauzman",
        },
        new()
        {
            FromName = "Echoes of Tarkov - Requisitions",
            FromAuthor = "RheddElBozo",
            ToName = "Echoes of Tarkov - Requisitions",
            ToAuthor = "Pluto!",
        },
        new()
        {
            FromName = "Expanded Task Text",
            FromAuthor = "Dirtbikercj",
            ToName = "Expanded Task Text (ETT)",
            ToAuthor = "FriedEngineer",
        },
        new()
        {
            FromName = "GuidingLight",
            FromAuthor = "",
            ToName = "Guiding Light",
            ToAuthor = "LightoftheWorld",
        },
        new()
        {
            FromName = "LiveBitcoinPricesREDUX",
            FromAuthor = "Shardbyte",
            ToName = "LiveBitcoinPricesREDUX",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "LootValueBackend",
            FromAuthor = "IhanaMies",
            ToName = "lootvalue",
            ToAuthor = "IhanaMies",
        },
        new()
        {
            FromName = "MarkedRoomLoot",
            FromAuthor = "Valens",
            ToName = "Make Marked Room Loot Great Again",
            ToAuthor = "Valens",
        },
        new()
        {
            FromName = "Mass_ISOH",
            FromAuthor = "AmightyTank",
            ToName = "Massivesoft Guns",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "Mass_MCW",
            FromAuthor = "AmightyTank",
            ToName = "Massivesoft Guns",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "Mass_QBZ03",
            FromAuthor = "AmightyTank",
            ToName = "Massivesoft Guns",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "Mass_QBZ97",
            FromAuthor = "AmightyTank",
            ToName = "Massivesoft Guns",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "Mass_Tavor_95",
            FromAuthor = "AmightyTank",
            ToName = "Massivesoft Guns",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "MOAR",
            FromAuthor = "DewardianDev",
            ToName = "MOAR + Bagels - Ultra lite spawn mod",
            ToAuthor = "DewardianDev",
        },
        new()
        {
            FromName = "MoreCheckmarksBackend",
            FromAuthor = "VIP",
            ToName = "MoreCheckmarks",
            ToAuthor = "VIPkiller17",
        },
        new()
        {
            FromName = "MusicManiac-KeysInLoot",
            FromAuthor = "MusicManiac",
            ToName = "Keys In Loot (KIL)",
            ToAuthor = "MusicManiac",
        },
        new()
        {
            FromName = "no-fir-hideout",
            FromAuthor = "schkuromi",
            ToName = "No FIR Hideout",
            ToAuthor = "sch_kuromi",
        },
        new()
        {
            FromName = "no-pmcbot-response",
            FromAuthor = "schkuromi",
            ToName = "No PMC Bot Response",
            ToAuthor = "sch_kuromi",
        },
        new()
        {
            FromName = "OldPk06Remake",
            FromAuthor = "Boogle",
            ToName = "Old PK-06 Restoration",
            ToAuthor = "Boogle",
        },
        new()
        {
            FromName = "Painter - Trader",
            FromAuthor = "MoxoPixel",
            ToName = "Painter",
            ToAuthor = "MoxoPixel",
        },
        new()
        {
            FromName = "PoseServComp",
            FromAuthor = "Choccy",
            ToName = "More Mannequin Pose",
            ToAuthor = "Choccy Milk",
        },
        new()
        {
            FromName = "ProfileEditorHelper",
            FromAuthor = "SkiTles55",
            ToName = "SPT-AKI Profile Editor",
            ToAuthor = "SkiTles55",
        },
        new()
        {
            FromName = "props-doorbreacher",
            FromAuthor = "Props",
            ToName = "Backdoor Bandit",
            ToAuthor = "MakerMacher",
        },
        new()
        {
            FromName = "ReflexSightsRework",
            FromAuthor = "SamSwat",
            ToName = "Reflex Sights Rework - Updated",
            ToAuthor = "stckytwl",
        },
        new()
        {
            FromName = "revival-mod",
            FromAuthor = "KaikiNoodles",
            ToName = "RevivalMod: Second Chance Survival System for Single Player Tarkov",
            ToAuthor = "KaikiNoodles",
        },
        new()
        {
            FromName = "RheddElBozo - SPT Battlepass",
            FromAuthor = "RheddElBozo",
            ToName = "SPT Battlepass",
            ToAuthor = "Pluto!",
        },
        new()
        {
            FromName = "Rocka's Corner Store",
            FromAuthor = "RockaHorse",
            ToName = "WTT - Corner Store",
            ToAuthor = "rockahorse",
        },
        new()
        {
            FromName = "SAIN",
            FromAuthor = "zSolarint",
            ToName = "SAIN - Solarint's AI Modifications - Full AI Combat System Replacement",
            ToAuthor = "Solarint",
        },
        new()
        {
            FromName = "shiny_airdrop_guns",
            FromAuthor = "leaves",
            ToName = "Shiny Airdrop Guns!",
            ToAuthor = "DeadLeaves",
        },
        new()
        {
            FromName = "SPT-InsuranceFraud",
            FromAuthor = "DragonX86",
            ToName = "Insurance Fraud (Port to 3.11)",
            ToAuthor = "DragonX86",
        },
        new()
        {
            FromName = "SVM",
            FromAuthor = "GhostFenixx",
            ToName = "Server Value Modifier [SVM]",
            ToAuthor = "GhostFenixx",
        },
        new()
        {
            FromName = "The Blacklist",
            FromAuthor = "Platinum",
            ToName = "The Blacklist - flea market enhancements",
            ToAuthor = "Platinum",
        },
        new()
        {
            FromName = "TraderModding",
            FromAuthor = "ChooChoo",
            ToName = "Trader Modding And Improved Weapon Building",
            ToAuthor = "ChooChoo",
        },
        new()
        {
            FromName = "uifixes",
            FromAuthor = "Tyfon",
            ToName = "UI Fixes",
            ToAuthor = "Tyfon",
        },
        new()
        {
            FromName = "Weapons",
            FromAuthor = "EpicRangeTime",
            ToName = "Epic's All in One",
            ToAuthor = "EpicRangeTime",
        },
        new()
        {
            FromName = "WeightlessAmmo",
            FromAuthor = "MNSTR",
            ToName = "Weight-Less-Ammo",
            ToAuthor = "Turok",
        },
        new()
        {
            FromName = "Wisps-InfMeds",
            FromAuthor = "WispsFlame",
            ToName = "INFMEDS-Update BY WispsFlame",
            ToAuthor = "WispsFlame",
        },
        new()
        {
            FromName = "Wolfiks Heavy Troopers",
            FromAuthor = "SerWolfik, reuploaded by AMightyTank",
            ToName = "Wolfik's Heavy Trooper Masks - Reupload",
            ToAuthor = "AmightyTank",
        },
        new()
        {
            FromName = "WTT-PackNStrap",
            FromAuthor = "GrooveypenguinX, Tron, WTT Team",
            ToName = "WTT Pack N Strap",
            ToAuthor = "GrooveypenguinX",
        },
        // new()
        // {
        //     FromName = "xxxxxx",
        //     FromAuthor = "xxxxxx",
        //     ToName = "xxxxxx",
        //     ToAuthor = "xxxxxx",
        // },
    ];

    /// <summary>
    /// List of client mod name/author updates for mods that have changed names or authors over time. Used to improve
    /// API matching by mapping old mod identifiers to current ones.
    /// </summary>
    public static readonly List<ModUpdateInfo> ClientModUpdates =
    [
        new()
        {
            FromName = "ActuallyFoundInRaid",
            FromAuthor = "privateryan",
            ToName = "Actually Found In Raid (UPDATED)",
            ToAuthor = "RuKira",
        },
        new()
        {
            FromName = "Adds the Skeleton Key Item",
            FromAuthor = "",
            ToName = "Skeleton Key",
            ToAuthor = "Boogle",
        },
        new()
        {
            FromName = "AILimit",
            FromAuthor = "dvize",
            ToName = "Ai Limit",
            ToAuthor = "wizard83",
        },
        new()
        {
            FromName = "Choo² Trader Modding",
            FromAuthor = "",
            ToName = "Trader Modding And Improved Weapon Building",
            ToAuthor = "ChooChoo",
        },
        new()
        {
            FromName = "ContinuousLoadAmmo",
            FromAuthor = "",
            ToName = "Continuous Load Ammo",
            ToAuthor = "ozen",
        },
        new()
        {
            FromName = "DeHazardifier",
            FromAuthor = "Tetris",
            ToName = "DeHazardifier - Updated by Tetris",
            ToAuthor = "TetrisGG",
        },
        new()
        {
            FromName = "TYR_DeHazardifier",
            FromAuthor = "",
            ToName = "DeHazardifier - Updated by Tetris",
            ToAuthor = "TetrisGG",
        },
        new()
        {
            FromName = "Dynamic External Resolution",
            FromAuthor = "",
            ToName = "Dynamic External Resolution Patch (DERP) 3.11 port",
            ToAuthor = "Sh1ba",
        },
        new()
        {
            FromName = "FlareEventNotifier",
            FromAuthor = "Terkoiz",
            ToName = "Exfil Flare Notification",
            ToAuthor = "Terkoiz",
        },
        new()
        {
            FromName = "FOV",
            FromAuthor = "SamSWAT",
            ToName = "SamSWAT's IncreasedFOV",
            ToAuthor = "Devraccoon",
        },
        new()
        {
            FromName = "FOVFix",
            FromAuthor = "",
            ToName = "Fontaine's FOV Fix",
            ToAuthor = "Fontaine",
        },
        new()
        {
            FromName = "GildedKeyStorage",
            FromAuthor = "DrakiaXYZ",
            ToName = "Gilded Key Storage",
            ToAuthor = "Jehree",
        },
        new()
        {
            FromName = "Graphics",
            FromAuthor = "",
            ToName = "Amands's Graphics",
            ToAuthor = "Amands2Mello",
        },
        new()
        {
            FromName = "LootingBots",
            FromAuthor = "",
            ToName = "Looting Bots",
            ToAuthor = "Skwizzy",
        },
        new()
        {
            FromName = "LootValue",
            FromAuthor = "IhanaMies",
            ToName = "lootvalue",
            ToAuthor = "IhanaMies",
        },
        new()
        {
            FromName = "MOAR",
            FromAuthor = "MOAR",
            ToName = "MOAR + Bagels - Ultra lite spawn mod",
            ToAuthor = "DewardianDev",
        },
        new()
        {
            FromName = "RamCleanerInterval",
            FromAuthor = "",
            ToName = "Ram Cleaner Fix",
            ToAuthor = "Devraccoon",
        },
        new()
        {
            FromName = "RevivalMod",
            FromAuthor = "",
            ToName = "SPT Leaderboard",
            ToAuthor = "harmony",
        },
        new()
        {
            FromName = "SetSpeed",
            FromAuthor = "DrakiaXYZ",
            ToName = "Set Speed - Set Player Speed with Hotkeys",
            ToAuthor = "DrakiaXYZ",
        },
        new()
        {
            FromName = "SPTLeftStanceWallFix",
            FromAuthor = "",
            ToName = "Left Stance Wall Fix",
            ToAuthor = "pein",
        },
        new()
        {
            FromName = "TraderScrolling",
            FromAuthor = "Kaeno",
            ToName = "Kaeno-TraderScrolling",
            ToAuthor = "CWX",
        },
        new()
        {
            FromName = "UnderFire",
            FromAuthor = "rpmwpm",
            ToName = "UnderFire - An Adrenaline Effect",
            ToAuthor = "rpmwpm",
        },
        new()
        {
            FromName = "UseLooseLoot",
            FromAuthor = "SPT",
            ToName = "Use Loose Loot",
            ToAuthor = "gaylatea",
        },
        new()
        {
            FromName = "Zones",
            FromAuthor = "VCQL",
            ToName = "Virtual's Custom Quest Loader",
            ToAuthor = "Virtual",
        },
        // new()
        // {
        //     FromName = "xxxxxx",
        //     FromAuthor = "xxxxxx",
        //     ToName = "xxxxxx",
        //     ToAuthor = "xxxxxx",
        // },
    ];
}

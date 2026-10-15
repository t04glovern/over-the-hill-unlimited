using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using OverTheHillUnlimited.Patches;

namespace OverTheHillUnlimited;

[BepInPlugin(Id, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Id = "OverTheHillUnlimited";
    public const string Name = "Over the Hill Unlimited";
    public const string Version = PluginVersion.Value; // set by <Version> in the csproj

    // Vanilla sessions are capped at 4, and the game's per-player networked state is sized to match
    public const int VanillaMaxPlayers = 4;

    internal static ManualLogSource Logger;
    internal static ConfigEntry<int> ConfigMaxPlayers;
    internal static ConfigEntry<bool> ConfigExpandPlayerSlots;
    internal static ConfigEntry<bool> ConfigVerboseLogging;

    private Harmony _harmony;

    public override void Load()
    {
        Logger = Log;
        BindConfig();

        _harmony = new Harmony(Id);

        if (ConfigExpandPlayerSlots.Value)
        {
            // Let the host pick more than 4 players and make Fusion honour it
            _harmony.PatchAll(typeof(CreateServerMenuPatch));
            _harmony.PatchAll(typeof(SessionSizePatches));

            // Give the game's fixed-size networked player state room for more players
            _harmony.PatchAll(typeof(SessionPlayersCapacityPatches));
            _harmony.PatchAll(typeof(MapNetworkCapacityPatches));
            _harmony.PatchAll(typeof(CampCapacityPatches));

            // Keep UIs built for 4 players working against the larger player list
            _harmony.PatchAll(typeof(PlayerListUiPatches));
        }
        else
        {
            Logger.LogInfo("ExpandPlayerSlots is off, sessions stay at the vanilla 4 players");
        }

        if (ConfigVerboseLogging.Value)
            _harmony.PatchAll(typeof(SessionDiagnosticsPatches));

        Logger.LogInfo($"{Name} {Version} loaded (ExpandPlayerSlots={ConfigExpandPlayerSlots.Value}, " +
                       $"MaxPlayers={ConfigMaxPlayers.Value}, {ExpandedState.Capacity} player slots)");
    }

    private void BindConfig()
    {
        ConfigMaxPlayers = Config.Bind(
            "General",
            "MaxPlayers",
            8,
            new ConfigDescription(
                "Highest player count offered in the Create Server menu (including yourself).",
                new AcceptableValueRange<int>(1, ExpandedState.Capacity)));

        ConfigExpandPlayerSlots = Config.Bind(
            "General",
            "ExpandPlayerSlots",
            true,
            "Raise the player cap above 4 by giving the game's networked player state more slots. " +
            "Changes the network layout, so every player in the session needs the mod with this enabled. " +
            "Turn it off to play with people who don't have the mod (vanilla behaviour, 4 players max).");

        ConfigVerboseLogging = Config.Bind(
            "Debug",
            "VerboseLogging",
            false,
            "Log the session size and the expanded player list as players join and leave. Useful when testing.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        return true;
    }
}

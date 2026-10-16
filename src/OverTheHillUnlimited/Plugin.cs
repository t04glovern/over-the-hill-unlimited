using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using OverTheHillUnlimited.Patches;
using OverTheHillUnlimited.Testing;

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

        // Always applied: the patches check UnlimitedMode at runtime, so it can be switched in the server browser
        try
        {
            ApplyPlayerCapPatches();
        }
        catch (System.Exception e)
        {
            // All or nothing: a half-patched game is worse than a vanilla one
            _harmony.UnpatchSelf();
            Logger.LogError($"Couldn't apply the player cap patches, so the mod is disabled. " +
                            $"The game has probably been updated.\n{e}");
            return;
        }

        if (ConfigVerboseLogging.Value)
            _harmony.PatchAll(typeof(SessionDiagnosticsPatches));

        // Only with the -otu-selftest launch option: bot players and the scripted self-test
        if (SelfTest.Requested)
        {
            _harmony.PatchAll(typeof(BotActivePlayersPatch));
            _harmony.PatchAll(typeof(BotPlayerObjectPatch));
            AddComponent<SelfTestRunner>();
            Logger.LogWarning("Self-test mode: this run will host a session with bot players, then quit");
        }

        Logger.LogInfo($"{Name} {Version} loaded (Unlimited mode {(UnlimitedMode.Enabled ? "on" : "off")}, " +
                       $"MaxPlayers={ConfigMaxPlayers.Value}, {ExpandedState.Capacity} player slots)");
    }

    private void ApplyPlayerCapPatches()
    {
        // Switch Unlimited mode from a checkbox in the server browser
        _harmony.PatchAll(typeof(ServerBrowserTogglePatches));

        // Let the host pick more than 4 players and make Fusion honour it
        _harmony.PatchAll(typeof(CreateServerMenuPatch));
        _harmony.PatchAll(typeof(SessionSizePatches));

        // Give the game's fixed-size networked player state room for more players
        _harmony.PatchAll(typeof(SessionPlayersCapacityPatches));
        _harmony.PatchAll(typeof(MapNetworkCapacityPatches));
        _harmony.PatchAll(typeof(CampCapacityPatches));

        // Pad game data with one entry per player (meteorite setups, lobby spawn points)
        _harmony.PatchAll(typeof(PerPlayerTablePatches));

        // Pad UIs with one element per player (lobby labels, pause menu, map, repair/tool menus, HUD markers)
        _harmony.PatchAll(typeof(PerPlayerUiPatches));

        // Keep unmodded players out: they can't use the expanded objects
        _harmony.PatchAll(typeof(ModdedLobbyPatches));
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
            "Unlimited mode: raise the player cap above 4 by giving the game's networked player state more slots. " +
            "Every player in the session needs the mod with this on, and while it's on you only see and join " +
            "servers of players running the same mod version. Turn it off to play with people who don't have the " +
            "mod (vanilla behaviour, 4 players max). You can also switch it in game with the " +
            "\"Unlimited mod servers\" checkbox in the server browser.");

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

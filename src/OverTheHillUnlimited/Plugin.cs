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
    internal static ConfigEntry<int> ConfigPebbleShareMaxPlayers;
    internal static ConfigEntry<int> ConfigPlayersPerExtraMeteorite;
    internal static ConfigEntry<bool> ConfigScaleMeteoriteReach;

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

        // The appended layouts are built on the game's woven offsets; if an update moved them, stay vanilla everywhere
        try
        {
            UnlimitedMode.Init(SessionPlayersCapacityPatches.State, MapNetworkCapacityPatches.State, CampCapacityPatches.State,
                WinchCapacityPatches.State);
        }
        catch (System.Exception e)
        {
            Logger.LogError($"Couldn't check the game's networked layout, so Unlimited mode is off.\n{e}");
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

        string mode = !UnlimitedMode.Available ? "unavailable" : UnlimitedMode.Enabled ? "on" : "off";
        Logger.LogInfo($"{Name} {Version} loaded (Unlimited mode {mode}, MaxPlayers={ConfigMaxPlayers.Value}, " +
                       $"{ExpandedState.Capacity} player slots)");
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
        _harmony.PatchAll(typeof(WinchCapacityPatches));

        // Pad game data with one entry per player (meteorite setups and pull directions, lobby spawn points), make room
        // for every car where players spawn or arrive in the map and for every tent at camp, and frame every car in the
        // lobby
        _harmony.PatchAll(typeof(PerPlayerTablePatches));
        _harmony.PatchAll(typeof(CabinSpawnPointPatches));
        _harmony.PatchAll(typeof(LobbyCameraPatches));
        _harmony.PatchAll(typeof(CampTentPatches));

        // Let more than 4 cars winch an item recovery object, and give voice chat a slot for everyone
        _harmony.PatchAll(typeof(ItemRecoveryWinchPatches));
        _harmony.PatchAll(typeof(VoiceChatCapacityPatches));

        // Balance meteorites for more than 4 players: the 4-player share of pebbles, more meteorites, a wider reach
        _harmony.PatchAll(typeof(MeteoriteBalancePatches));

        // Pad UIs with one element per player (lobby labels, pause menu, map, repair/tool menus, HUD markers)
        _harmony.PatchAll(typeof(PerPlayerUiPatches));

        // Colours of their own for players 5 and up (Windows only, see PlayerColourPatches)
        if (PlayerColourPatches.Supported)
            _harmony.PatchAll(typeof(PlayerColourPatches));

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
                $"Highest player count offered in the Create Server menu (including yourself) in Unlimited mode. " +
                $"The vanilla 2 to {VanillaMaxPlayers} are always offered.",
                new AcceptableValueRange<int>(VanillaMaxPlayers, ExpandedState.Capacity)));

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

        // The host decides meteorite rewards and spawns, so for these only the host's settings count
        ConfigPebbleShareMaxPlayers = Config.Bind(
            "Meteorites",
            "PebbleShareMaxPlayers",
            VanillaMaxPlayers,
            new ConfigDescription(
                "A meteorite's pebbles are split between the players involved in pulling it out. With more players " +
                "involved than this, each gets the share they'd get with this many instead. " +
                $"{ExpandedState.Capacity} splits them between everyone, as the game does. Only the host's setting counts.",
                new AcceptableValueRange<int>(1, ExpandedState.Capacity)));

        ConfigPlayersPerExtraMeteorite = Config.Bind(
            "Meteorites",
            "PlayersPerExtraMeteorite",
            2,
            new ConfigDescription(
                $"The game keeps 2 or 3 meteorites in the map. With more than {VanillaMaxPlayers} players, add one for " +
                $"every this many players past {VanillaMaxPlayers}. 0 keeps the game's 2 or 3. Only the host's setting counts.",
                new AcceptableValueRange<int>(0, ExpandedState.Capacity)));

        ConfigScaleMeteoriteReach = Config.Bind(
            "Meteorites",
            "ScaleRewardReach",
            true,
            $"Only cars within 10 m of a meteorite when it comes out count as involved, unless they're winched to it. " +
            $"With more than {VanillaMaxPlayers} players, widen that so the area grows with the number of cars " +
            "(14 m for 8, 20 m for 16). Only the host's setting counts.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        return true;
    }
}

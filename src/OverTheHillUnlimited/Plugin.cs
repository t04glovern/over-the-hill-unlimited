using HarmonyLib;
using OverTheHillUnlimited.Networking;
using OverTheHillUnlimited.Patches;
using OverTheHillUnlimited.Testing;

namespace OverTheHillUnlimited;

/// <summary>
/// The mod itself, the same on either mod loader. The loader's half of this class starts it and gives it a log,
/// settings, a data folder and the self-test: Loader/BepInEx/Plugin.BepInEx.cs in the BepInEx build, and
/// Plugin.MelonLoader.cs in the MelonLoader build (src/OverTheHillUnlimited.MelonLoader).
/// </summary>
public partial class Plugin
{
    public const string Id = "OverTheHillUnlimited";
    public const string Name = "Over the Hill Unlimited";
    public const string Version = PluginVersion.Value; // set by <Version> in OverTheHillUnlimited.props
    public const string Author = "t04glovern";

    // Vanilla sessions are capped at 4, and the game's per-player networked state is sized to match
    public const int VanillaMaxPlayers = 4;

    internal static IModLog Logger;
    internal static Setting<int> ConfigMaxPlayers;
    internal static Setting<bool> ConfigExpandPlayerSlots;
    internal static Setting<bool> ConfigVerboseLogging;
    internal static Setting<int> ConfigPebbleShareMaxPlayers;
    internal static Setting<int> ConfigPlayersPerExtraMeteorite;
    internal static Setting<bool> ConfigScaleMeteoriteReach;
    internal static Setting<bool> ConfigGatherAsGroup;

    static HarmonyLib.Harmony _harmony; // qualified: MelonLoader also has a Harmony namespace

    // Called by the loader's half once it can apply patches
    static void Start(IModLog log)
    {
        Logger = log;
        BindConfig();

        _harmony = new HarmonyLib.Harmony(Id);

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

        // Room for the whole party where everyone has to gather (portals, cabin camping, the camp tool), and a meteorite
        // reach ring that shows the widened reach. Neither is needed to play, so if a game update breaks one, it's left
        // out and the rest of the mod carries on.
        ApplyOptionalPatches(typeof(PartyGatheringPatches), "gathering the party as one group");
        ApplyOptionalPatches(typeof(MeteoriteReachRingPatches), "the meteorite reach ring");

        if (ConfigVerboseLogging.Value)
            _harmony.PatchAll(typeof(SessionDiagnosticsPatches));

        // Only with the -otu-selftest launch option: bot players and the scripted self-test
        if (SelfTest.Requested)
        {
            _harmony.PatchAll(typeof(BotActivePlayersPatch));
            _harmony.PatchAll(typeof(BotPlayerObjectPatch));
            _harmony.PatchAll(typeof(SaveIsolationPatches));
            AddSelfTestRunner();
            Logger.LogWarning("Self-test mode: this run will host a session with bot players, then quit");
        }

        string mode = !UnlimitedMode.Available ? "unavailable" : UnlimitedMode.Enabled ? "on" : "off";
        Logger.LogInfo($"{Name} {Version} loaded on {Loader} (Unlimited mode {mode}, MaxPlayers={ConfigMaxPlayers.Value}, " +
                       $"{ExpandedState.Capacity} player slots)");
    }

    static void Stop()
    {
        _harmony?.UnpatchSelf();
        _optionalPatches.ForEach(patches => patches.UnpatchSelf());
    }

    static readonly System.Collections.Generic.List<HarmonyLib.Harmony> _optionalPatches = new();

    // Applies one class's patches, or none of them
    static void ApplyOptionalPatches(System.Type patches, string feature)
    {
        var applied = new HarmonyLib.Harmony($"{Id}.{patches.Name}");
        try
        {
            applied.PatchAll(patches);
            _optionalPatches.Add(applied);
        }
        catch (System.Exception e)
        {
            applied.UnpatchSelf();
            Logger.LogWarning($"Couldn't apply {feature}, so it's off. The game has probably been updated.\n{e}");
        }
    }

    static void ApplyPlayerCapPatches()
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

    // The same settings on either loader. BepInEx files them under the section; MelonLoader keeps them all in one
    // [OverTheHillUnlimited] category, so the section only groups them here.
    static void BindConfig()
    {
        ConfigMaxPlayers = BindRange(
            "General",
            "MaxPlayers",
            8,
            VanillaMaxPlayers,
            ExpandedState.Capacity,
            $"Highest player count offered in the Create Server menu (including yourself) in Unlimited mode. " +
            $"The vanilla 2 to {VanillaMaxPlayers} are always offered.");

        ConfigExpandPlayerSlots = Bind(
            "General",
            "ExpandPlayerSlots",
            true,
            "Unlimited mode: raise the player cap above 4 by giving the game's networked player state more slots. " +
            "Every player in the session needs the mod with this on, and while it's on you only see and join " +
            "servers of players running the same mod version. Turn it off to play with people who don't have the " +
            "mod (vanilla behaviour, 4 players max). You can also switch it in game with the " +
            "\"Unlimited mod servers\" checkbox in the server browser.");

        ConfigVerboseLogging = Bind(
            "Debug",
            "VerboseLogging",
            false,
            "Log the session size and the expanded player list as players join and leave. Useful when testing.");

        // The host decides meteorite rewards and spawns, so for these only the host's settings count
        ConfigPebbleShareMaxPlayers = BindRange(
            "Meteorites",
            "PebbleShareMaxPlayers",
            VanillaMaxPlayers,
            1,
            ExpandedState.Capacity,
            "A meteorite's pebbles are split between the players involved in pulling it out. With more players " +
            "involved than this, each gets the share they'd get with this many instead. " +
            $"{ExpandedState.Capacity} splits them between everyone, as the game does. Only the host's setting counts.");

        ConfigPlayersPerExtraMeteorite = BindRange(
            "Meteorites",
            "PlayersPerExtraMeteorite",
            2,
            0,
            ExpandedState.Capacity,
            $"The game keeps 2 or 3 meteorites in the map. With more than {VanillaMaxPlayers} players, add one for " +
            $"every this many players past {VanillaMaxPlayers}. 0 keeps the game's 2 or 3. Only the host's setting counts.");

        ConfigScaleMeteoriteReach = Bind(
            "Meteorites",
            "ScaleRewardReach",
            true,
            $"Only cars within 10 m of a meteorite when it comes out count as involved, unless they're winched to it. " +
            $"With more than {VanillaMaxPlayers} players, widen that so the area grows with the number of cars " +
            "(14 m for 8, 20 m for 16), and show it in the ring around the meteorite. Only the host's setting decides " +
            "the reward.");

        ConfigGatherAsGroup = Bind(
            "Areas",
            "GatherAsGroup",
            true,
            "To travel to another area every car has to be at the portal (the boat's holds 4), to camp at a cabin every " +
            "car has to be in its camping area, and to camp anywhere else every car has to be within 15 m of the " +
            $"camper. With more than {VanillaMaxPlayers} players, the party also counts as there once it's gathered as " +
            $"one group: every car in the area, or linked to it by a chain of cars each within " +
            $"{PartyGatheringPatches.LinkDistance:F0} m of the next. Whoever presses Interact still has to be in the area. Travel goes by this setting for both the " +
            "player who presses and the host; camping goes by the camper's.");
    }
}

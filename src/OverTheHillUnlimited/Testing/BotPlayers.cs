using System.Collections.Generic;
using HarmonyLib;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Fake players for the self-test. They join and leave through the game's own host-side paths, so every system that
/// reacts to players (the player registry, spawn points, lobby, HUD, menus) sees them like real ones. They have no
/// network connection and never drive. Only ever created during a self-test run.
/// </summary>
internal static class BotPlayers
{
    static readonly List<PlayerRef> Bots = new();

    public static IReadOnlyList<PlayerRef> All => Bots;

    /// <summary>
    /// Joins a bot the way OverlandNetworkRunner.OnRuntimePlayerReceived joins a real client: SpawnPlayerInLobby
    /// registers the player, picks their spawn point and spawns their car. The bot copies the host's car and upgrades.
    /// </summary>
    public static PlayerRef Join(int index, string name)
    {
        var overland = OverlandNetworkRunner.Instance;
        var runner = overland.NetworkRunner;
        if (!overland.spawnedCharactersInfo.TryGetValue(runner.LocalPlayer, out var host))
            throw new System.InvalidOperationException("The host has no RuntimePlayer to copy");

        var bot = PlayerRef.FromIndex(index);
        var runtimePlayer = new RuntimePlayer
        {
            PlayerName = name,
            CarItemId = host.CarItemId,
            EnabledUpgrades = host.EnabledUpgrades,
            EquippedModLiveryModId = host.EquippedModLiveryModId,
            EquippedModLiverySubId = host.EquippedModLiverySubId,
            Flags = host.Flags,
        };

        // Listed before spawning, so ActivePlayers includes the bot while the game sets it up
        Bots.Add(bot);
        overland.SpawnPlayerInLobby(runner, bot, runtimePlayer);
        return bot;
    }

    /// <summary>Removes a bot through the game's own handling of a player leaving.</summary>
    public static void Leave(PlayerRef bot)
    {
        var overland = OverlandNetworkRunner.Instance;
        overland.Fusion_INetworkRunnerCallbacks_OnPlayerLeft(overland.NetworkRunner, bot);
        Bots.Remove(bot);
    }

    public static bool Contains(PlayerRef player)
    {
        foreach (var bot in Bots)
        {
            if (bot.RawEncoded == player.RawEncoded)
                return true;
        }
        return false;
    }

    /// <summary>The bot's car, from the game's own record of the characters it spawned.</summary>
    public static bool TryGetCar(PlayerRef bot, out NetworkObject car)
    {
        car = null;
        var spawned = OverlandNetworkRunner.Instance?.SpawnedCharacters;
        return spawned != null && spawned.TryGetValue(bot, out car) && car != null;
    }
}

/// <summary>
/// Fusion only registers player objects for connected players, so a bot's car never becomes its player object and
/// every system that looks players up by TryGetPlayerObject (car info, repair and car health markers, the compass)
/// would skip the bots. This answers those lookups with the bot's car. Applied only in self-test runs.
/// </summary>
public class BotPlayerObjectPatch
{
    [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.TryGetPlayerObject))]
    [HarmonyPostfix]
    static void FindBotCar(PlayerRef player, ref NetworkObject networkObject, ref bool __result)
    {
        if (__result || !BotPlayers.Contains(player))
            return;
        if (BotPlayers.TryGetCar(player, out var car))
        {
            networkObject = car;
            __result = true;
        }
    }
}

/// <summary>
/// Adds the bots to NetworkRunner.ActivePlayers, which the car repair and tool selection menus, camp repairs and
/// meteorite spawning iterate. Applied only in self-test runs.
/// </summary>
public class BotActivePlayersPatch
{
    [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.ActivePlayers), MethodType.Getter)]
    [HarmonyPostfix]
    static void AddBots(ref Il2CppSystem.Collections.Generic.IEnumerable<PlayerRef> __result)
    {
        if (BotPlayers.All.Count == 0 || __result == null)
            return;

        var players = new Il2CppSystem.Collections.Generic.List<PlayerRef>();
        var enumerator = __result.GetEnumerator();
        var untyped = enumerator.Cast<Il2CppSystem.Collections.IEnumerator>();
        while (untyped.MoveNext())
            players.Add(enumerator.Current);
        foreach (var bot in BotPlayers.All)
            players.Add(bot);

        __result = players.Cast<Il2CppSystem.Collections.Generic.IEnumerable<PlayerRef>>();
    }
}

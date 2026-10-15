using Fusion;
using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Testing aid, applied only with VerboseLogging on: logs the Fusion session size as players come and go,
/// and the contents of the expanded player list.
/// </summary>
public class SessionDiagnosticsPatches
{
    [HarmonyPatch(typeof(OverlandNetworkRunner), nameof(OverlandNetworkRunner.Fusion_INetworkRunnerCallbacks_OnPlayerJoined))]
    [HarmonyPostfix]
    static void OnPlayerJoinedPostfix(NetworkRunner runner)
    {
        LogSession("Player joined", runner);
    }

    [HarmonyPatch(typeof(OverlandNetworkRunner), nameof(OverlandNetworkRunner.Fusion_INetworkRunnerCallbacks_OnPlayerLeft))]
    [HarmonyPostfix]
    static void OnPlayerLeftPostfix(NetworkRunner runner)
    {
        LogSession("Player left", runner);
    }

    // Session info isn't populated yet when the host joins, so also read it whenever the pause menu opens
    [HarmonyPatch(typeof(StatePauseMenu), nameof(StatePauseMenu.SetupMenu))]
    [HarmonyPostfix]
    static void PauseMenuPostfix()
    {
        LogSession("Pause menu opened", OverlandSessionPlayers.Instance?.Runner);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.AddPlayer))]
    [HarmonyPostfix]
    static void AddPlayerPostfix(OverlandSessionPlayers __instance, PlayerRef playerRef)
    {
        var ordered = __instance.OrderedPlayerRefs;
        var slots = new string[ordered.Length];
        for (int i = 0; i < ordered.Length; i++)
            slots[i] = ordered[i].IsNone ? "-" : $"{ordered[i].PlayerId}:{__instance.GetPlayerName(ordered[i])}";

        Plugin.Logger.LogInfo($"Added player {playerRef.PlayerId} " +
                              $"(expanded={SessionPlayersCapacityPatches.State.IsExpanded(__instance)}, " +
                              $"{__instance.GetPlayerCount()}/{ordered.Length} slots used): [{string.Join(", ", slots)}]");
    }

    static void LogSession(string what, NetworkRunner runner)
    {
        var session = runner?.SessionInfo;
        if (session == null || !session.IsValid)
        {
            Plugin.Logger.LogInfo($"{what}: no session info yet");
            return;
        }
        Plugin.Logger.LogInfo($"{what}: Fusion session has {session.PlayerCount}/{session.MaxPlayers} players");
    }
}

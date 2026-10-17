using System;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Makes Fusion size the session from the Create Server menu. The game starts hosts with
/// StartGameArgs.PlayerCount = Math.Min(GameSetupContext.MaxPlayers, 4), and ships Fusion's default,
/// NetworkProjectConfig.Simulation.PlayerCount, as 4 too.
///
/// The args are changed inside Fusion's StartGameModeCloud async state machine, which keeps them in a field and
/// creates the Photon room from it. Patching NetworkRunner.StartGame(StartGameArgs) directly would mean receiving
/// that large struct by value, which Il2CppInterop's trampolines get wrong on Linux (System V passes it on the
/// stack, Windows passes a pointer) and which crashed native Linux clients.
/// </summary>
public class SessionSizePatches
{
    static int _requestedMaxPlayers;
    static int? _vanillaSimulationPlayerCount;

    [HarmonyPatch(typeof(OverlandNetworkRunner), nameof(OverlandNetworkRunner.StartGame))]
    [HarmonyPrefix]
    static void RememberRequestedMaxPlayers(GameSetupContext gameSetupContext)
    {
        _requestedMaxPlayers = Math.Min(gameSetupContext?.MaxPlayers ?? 0, ExpandedState.Capacity);
    }

    [HarmonyPatch(typeof(NetworkRunner._StartGameModeCloud_d__481), nameof(NetworkRunner._StartGameModeCloud_d__481.MoveNext))]
    [HarmonyPrefix]
    static void StartGameModeCloudPrefix(NetworkRunner._StartGameModeCloud_d__481 __instance)
    {
        // Only the first step, before anything has read the args
        if (__instance.__1__state != -1)
            return;

        // A boxed copy: changes only reach Fusion once it's assigned back to the field
        var args = __instance.args;

        // Fusion's default player count is raised on every modded peer, host or client, so their configs match. In
        // vanilla mode it goes back to the game's own value, to match vanilla peers again.
        var simulation = (args.Config ?? NetworkProjectConfig.Global)?.Simulation;
        if (simulation != null)
        {
            _vanillaSimulationPlayerCount ??= simulation.PlayerCount;
            simulation.PlayerCount = UnlimitedMode.Enabled
                ? Math.Max(_vanillaSimulationPlayerCount.Value, ExpandedState.Capacity)
                : _vanillaSimulationPlayerCount.Value;
        }

        if (!UnlimitedMode.Enabled || args.GameMode != GameMode.Host || _requestedMaxPlayers <= Plugin.VanillaMaxPlayers)
            return;

        args.PlayerCount = new Il2CppSystem.Nullable<int>(_requestedMaxPlayers);
        __instance.args = args;
        Plugin.Logger.LogInfo($"Hosting a session for {_requestedMaxPlayers} players");
    }
}

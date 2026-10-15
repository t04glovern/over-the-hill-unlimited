using Fusion;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Makes Fusion size the session from the Create Server menu. The game always starts hosts with
/// StartGameArgs.PlayerCount = 4 (it enforces smaller menu choices itself), and ships Fusion's default,
/// NetworkProjectConfig.Simulation.PlayerCount, as 4 too.
///
/// The args are changed inside Fusion's StartGameModeCloud async state machine, which keeps them in a field and
/// creates the Photon room from it. Patching NetworkRunner.StartGame(StartGameArgs) directly would mean receiving
/// that large struct by value, which Il2CppInterop's trampolines get wrong on Linux (System V passes it on the
/// stack, Windows passes a pointer) and which crashed native Linux clients.
/// </summary>
public class SessionSizePatches
{
    // StartGameArgs.PlayerCount is a Nullable<int> at struct offset 0x80, after the 0x10 boxed-object header
    const int PlayerCountOffset = 0x10 + 0x80;

    static int _requestedMaxPlayers;

    [HarmonyPatch(typeof(OverlandNetworkRunner), nameof(OverlandNetworkRunner.StartGame))]
    [HarmonyPrefix]
    static void RememberRequestedMaxPlayers(GameSetupContext gameSetupContext)
    {
        _requestedMaxPlayers = gameSetupContext?.MaxPlayers ?? 0;
    }

    [HarmonyPatch(typeof(NetworkRunner._StartGameModeCloud_d__481), nameof(NetworkRunner._StartGameModeCloud_d__481.MoveNext))]
    [HarmonyPrefix]
    static void StartGameModeCloudPrefix(NetworkRunner._StartGameModeCloud_d__481 __instance)
    {
        // Only the first step, before anything has read the args
        if (!UnlimitedMode.Enabled || __instance.__1__state != -1)
            return;

        // A boxed copy: changes only reach Fusion once it's assigned back to the field
        var args = __instance.args;

        // Raised on every modded peer, host or client, so their Fusion configs match
        var config = args.Config ?? NetworkProjectConfig.Global;
        if (config?.Simulation != null && config.Simulation.PlayerCount < ExpandedState.Capacity)
            config.Simulation.PlayerCount = ExpandedState.Capacity;

        if (args.GameMode == GameMode.Host && _requestedMaxPlayers > Plugin.VanillaMaxPlayers)
        {
            args.PlayerCount = new Il2CppSystem.Nullable<int>(_requestedMaxPlayers);
            __instance.args = args;
        }

        Plugin.Logger.LogInfo($"Starting {args.GameMode} session: PlayerCount={ReadPlayerCount(args)}, " +
                              $"Fusion default PlayerCount={config?.Simulation?.PlayerCount}");
    }

    // Read straight from the struct: the interop wrapper's Nullable<int>.Value misreports it as 0
    static unsafe string ReadPlayerCount(StartGameArgs args)
    {
        byte* field = (byte*)args.Pointer + PlayerCountOffset;
        return field[0] != 0 ? (*(int*)(field + 4)).ToString() : "unset";
    }
}

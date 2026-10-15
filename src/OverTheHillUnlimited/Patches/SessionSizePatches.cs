using Fusion;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Makes Fusion size the session from the Create Server menu. The game always starts hosts with
/// StartGameArgs.PlayerCount = 4 (it enforces smaller menu choices itself), and ships Fusion's default,
/// NetworkProjectConfig.Simulation.PlayerCount, as 4 too.
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

    [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.StartGame))]
    [HarmonyPrefix]
    static void StartGamePrefix(StartGameArgs args)
    {
        if (args.GameMode == GameMode.Single)
            return;

        // Raised on every modded peer, host or client, so their Fusion configs match
        var config = args.Config ?? NetworkProjectConfig.Global;
        if (config?.Simulation != null && config.Simulation.PlayerCount < ExpandedState.Capacity)
            config.Simulation.PlayerCount = ExpandedState.Capacity;

        // Writing to args changes what the original StartGame receives (the session then reports x/MaxPlayers)
        if (args.GameMode == GameMode.Host && _requestedMaxPlayers > Plugin.VanillaMaxPlayers)
            args.PlayerCount = new Il2CppSystem.Nullable<int>(_requestedMaxPlayers);

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

using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
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

    /// <summary>
    /// The prefix on the StartGameModeCloud state machine's MoveNext. The compiler names the state machine with a
    /// number that changes whenever Fusion is rebuilt (&lt;StartGameModeCloud&gt;d__481 until the game's 13 November
    /// 2026 update, d__486 since), and naming it in code stopped the mod loading on the new Fusion. So it's found by
    /// name when the patch is applied, and its two fields are read by reflection.
    /// </summary>
    [HarmonyPatch]
    internal static class StartGameModeCloud
    {
        const string StateMachinePrefix = "_StartGameModeCloud_d__";

        static Type _stateMachine;
        static PropertyInfo _state;
        static PropertyInfo _args;

        static MethodBase TargetMethod()
        {
            foreach (var nested in typeof(NetworkRunner).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!nested.Name.StartsWith(StateMachinePrefix, StringComparison.Ordinal))
                    continue;
                if (_stateMachine != null)
                    throw new MissingMemberException($"More than one NetworkRunner.{StateMachinePrefix}* state machine");
                _stateMachine = nested;
            }
            if (_stateMachine == null)
                throw new MissingMemberException($"No NetworkRunner.{StateMachinePrefix}* state machine");

            _state = _stateMachine.GetProperty("__1__state") ?? throw new MissingMemberException(_stateMachine.Name, "__1__state");
            _args = _stateMachine.GetProperty("args") ?? throw new MissingMemberException(_stateMachine.Name, "args");
            return AccessTools.Method(_stateMachine, "MoveNext") ?? throw new MissingMemberException(_stateMachine.Name, "MoveNext");
        }

        static void Prefix(Il2CppObjectBase __instance)
        {
            object machine = _stateMachine.IsInstanceOfType(__instance)
                ? __instance
                : Activator.CreateInstance(_stateMachine, __instance.Pointer);

            // Only the first step, before anything has read the args
            if ((int)_state.GetValue(machine) != -1)
                return;

            // A boxed copy: changes only reach Fusion once it's assigned back to the field
            var args = (StartGameArgs)_args.GetValue(machine);

            // Fusion's default player count is raised on every modded peer, host or client, so their configs match.
            // In vanilla mode it goes back to the game's own value, to match vanilla peers again.
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
            _args.SetValue(machine, args);
            Plugin.Logger.LogInfo($"Hosting a session for {_requestedMaxPlayers} players");
        }
    }
}

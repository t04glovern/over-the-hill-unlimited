using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Gives MapNetworkManager's per-player map pinpoints and portable winch anchors room for more than 4 players.
/// </summary>
public class MapNetworkCapacityPatches
{
    // From [NetworkBehaviourWeaved(107)]; vanilla ranges from each property's [NetworkedWeaved(offset, words)]
    internal static readonly ExpandedState State = ExpandedState.For<MapNetworkManager>(107);

    static readonly int PinpointPositionOffset = // values are Vector3
        State.AddDictionary(nameof(MapNetworkManager.PlayerRefToPinpointPosition), new(0, 45), keyWords: 1, valueWords: 3);
    static readonly int PinpointObjectOffset = // values are NetworkObjects, stored as their NetworkId
        State.AddDictionary(nameof(MapNetworkManager.PlayerRefToPinpointNetworkObject), new(45, 31), keyWords: 1, valueWords: 1);
    static readonly int WinchAnchorOffset =
        State.AddDictionary(nameof(MapNetworkManager.PlayerRefToPortableWinchAnchorNetworkObject), new(76, 31), keyWords: 1, valueWords: 1);

    [HarmonyPatch(typeof(NetworkBehaviourUtils), nameof(NetworkBehaviourUtils.GetWordCount))]
    [HarmonyPostfix]
    static void GetWordCountPostfix(NetworkBehaviour behaviour, ref int __result)
    {
        State.ExpandIf<MapNetworkManager>(behaviour, ref __result);
    }

    // Runs before the game's Render checks its ChangeDetector
    [HarmonyPatch(typeof(MapNetworkManager), nameof(MapNetworkManager.Render))]
    [HarmonyPrefix]
    static void RenderPrefix(MapNetworkManager __instance)
    {
        State.SignalChanges(__instance);
    }

    [HarmonyPatch(typeof(MapNetworkManager), nameof(MapNetworkManager.PlayerRefToPinpointPosition), MethodType.Getter)]
    [HarmonyPostfix]
    static void PinpointPositionPostfix(MapNetworkManager __instance, ref NetworkDictionary<PlayerRef, Vector3> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, PinpointPositionOffset, __result);
    }

    [HarmonyPatch(typeof(MapNetworkManager), nameof(MapNetworkManager.PlayerRefToPinpointNetworkObject), MethodType.Getter)]
    [HarmonyPostfix]
    static void PinpointObjectPostfix(MapNetworkManager __instance, ref NetworkDictionary<PlayerRef, NetworkObject> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, PinpointObjectOffset, __result);
    }

    [HarmonyPatch(typeof(MapNetworkManager), nameof(MapNetworkManager.PlayerRefToPortableWinchAnchorNetworkObject), MethodType.Getter)]
    [HarmonyPostfix]
    static void WinchAnchorPostfix(MapNetworkManager __instance, ref NetworkDictionary<PlayerRef, NetworkObject> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, WinchAnchorOffset, __result);
    }
}

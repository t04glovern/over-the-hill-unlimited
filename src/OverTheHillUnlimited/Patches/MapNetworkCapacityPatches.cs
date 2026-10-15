using Fusion;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Gives MapNetworkManager's per-player map pinpoints and portable winch anchors room for more than 4 players.
/// </summary>
public class MapNetworkCapacityPatches
{
    // From [NetworkBehaviourWeaved(107)]
    internal static readonly ExpandedState State = new(nameof(MapNetworkManager), 107);

    static readonly int PinpointPositionOffset = State.AddDictionary(1, 3); // Vector3 is 3 words
    static readonly int PinpointObjectOffset = State.AddDictionary(1, 1); // NetworkObject is stored as its NetworkId
    static readonly int WinchAnchorOffset = State.AddDictionary(1, 1);

    [HarmonyPatch(typeof(NetworkBehaviourUtils), nameof(NetworkBehaviourUtils.GetWordCount))]
    [HarmonyPostfix]
    static void GetWordCountPostfix(NetworkBehaviour behaviour, ref int __result)
    {
        State.ExpandIf<MapNetworkManager>(behaviour, ref __result);
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

using Fusion;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Gives CampManager room for more than 4 tents, one per player at camp.
/// </summary>
public class CampCapacityPatches
{
    // From [NetworkBehaviourWeaved(10)] and SpawnedTents' [NetworkedWeaved(2, 4)]. CampManager doesn't use a
    // ChangeDetector, so unlike the other expanded behaviours it needs no change signalling.
    internal static readonly ExpandedState State = new(nameof(CampManager), 10);

    static readonly int SpawnedTentsOffset = State.AddArray(new(2, 4), elementWords: 1); // NetworkObject as its NetworkId

    [HarmonyPatch(typeof(NetworkBehaviourUtils), nameof(NetworkBehaviourUtils.GetWordCount))]
    [HarmonyPostfix]
    static void GetWordCountPostfix(NetworkBehaviour behaviour, ref int __result)
    {
        State.ExpandIf<CampManager>(behaviour, ref __result);
    }

    [HarmonyPatch(typeof(CampManager), nameof(CampManager.SpawnedTents), MethodType.Getter)]
    [HarmonyPostfix]
    static void SpawnedTentsPostfix(CampManager __instance, ref NetworkArray<NetworkObject> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Array(__instance, SpawnedTentsOffset, ExpandedState.Capacity, __result);
    }
}

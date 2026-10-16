using Fusion;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Gives each car's winch system room to track up to 16 winches attached to it. VehicleWinchSystem.AttachedWinchesOwners
/// holds the car's own winch plus every other player winched to it, 4 at most, and AddAttachedWinchOwner silently drops
/// any more. DetachAllWinches walks that list when the car teleports, respawns, leaves or opens the merchant, so a
/// dropped player's rope stayed attached through all of those.
/// </summary>
public class WinchCapacityPatches
{
    // From [NetworkBehaviourWeaved(25)] and AttachedWinchesOwners' [NetworkedWeaved(14, 4)]. Render's ChangeDetector
    // only watches WinchState and WinchPointsDisabled, so this needs no change signalling.
    internal static readonly ExpandedState State = ExpandedState.For<VehicleWinchSystem>(25);

    static readonly int AttachedWinchesOwnersOffset =
        State.AddArray(nameof(VehicleWinchSystem.AttachedWinchesOwners), new(14, 4), elementWords: 1);

    [HarmonyPatch(typeof(NetworkBehaviourUtils), nameof(NetworkBehaviourUtils.GetWordCount))]
    [HarmonyPostfix]
    static void GetWordCountPostfix(NetworkBehaviour behaviour, ref int __result)
    {
        State.ExpandIf<VehicleWinchSystem>(behaviour, ref __result);
    }

    [HarmonyPatch(typeof(VehicleWinchSystem), nameof(VehicleWinchSystem.AttachedWinchesOwners), MethodType.Getter)]
    [HarmonyPostfix]
    static void AttachedWinchesOwnersPostfix(VehicleWinchSystem __instance, ref NetworkArray<PlayerRef> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Array(__instance, AttachedWinchesOwnersOffset, ExpandedState.Capacity, __result);
    }
}

using Fusion;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Gives OverlandSessionPlayers, the game's networked player registry (order, colors, names, flags),
/// room for more than 4 players. Each collection getter returns a larger collection stored in the
/// expanded state, built the same way the woven getter builds the vanilla one.
/// </summary>
public class SessionPlayersCapacityPatches
{
    // From [NetworkBehaviourWeaved(390)]; vanilla ranges from each property's [NetworkedWeaved(offset, words)]
    internal static readonly ExpandedState State = ExpandedState.For<OverlandSessionPlayers>(390);

    static readonly int OrderedPlayerRefsOffset =
        State.AddArray(nameof(OverlandSessionPlayers.OrderedPlayerRefs), new(351, 4), elementWords: 1);
    static readonly int PlayerColorsOffset =
        State.AddArray(nameof(OverlandSessionPlayers.PlayerColors), new(355, 4), elementWords: 1);
    static readonly int BusyReasonOffset =
        State.AddDictionary(nameof(OverlandSessionPlayers.BusyPlayerRefToBusyReason), new(359, 31), keyWords: 1, valueWords: 1);
    static readonly int DevTeamOffset =
        State.AddDictionary(nameof(OverlandSessionPlayers.PlayerRefToDevTeamMembership), new(320, 31), keyWords: 1, valueWords: 1);
    static readonly int SupporterEditionOffset =
        State.AddDictionary(nameof(OverlandSessionPlayers.PlayerRefToSupporterEditionOwnership), new(289, 31), keyWords: 1, valueWords: 1);
    static readonly int NameOffset = // values are NetworkString<_32>
        State.AddDictionary(nameof(OverlandSessionPlayers.PlayerRefToNameDictionary), new(34, 255), keyWords: 1, valueWords: 33);

    [HarmonyPatch(typeof(NetworkBehaviourUtils), nameof(NetworkBehaviourUtils.GetWordCount))]
    [HarmonyPostfix]
    static void GetWordCountPostfix(NetworkBehaviour behaviour, ref int __result)
    {
        State.ExpandIf<OverlandSessionPlayers>(behaviour, ref __result);
    }

    // Runs before the game's Render checks its ChangeDetector
    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.Render))]
    [HarmonyPrefix]
    static void RenderPrefix(OverlandSessionPlayers __instance)
    {
        State.SignalChanges(__instance);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.OrderedPlayerRefs), MethodType.Getter)]
    [HarmonyPostfix]
    static void OrderedPlayerRefsPostfix(OverlandSessionPlayers __instance, ref NetworkArray<PlayerRef> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Array(__instance, OrderedPlayerRefsOffset, ExpandedState.Capacity, __result);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.PlayerColors), MethodType.Getter)]
    [HarmonyPostfix]
    static void PlayerColorsPostfix(OverlandSessionPlayers __instance, ref NetworkArray<PlayerRef> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Array(__instance, PlayerColorsOffset, ExpandedState.Capacity, __result);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.BusyPlayerRefToBusyReason), MethodType.Getter)]
    [HarmonyPostfix]
    static void BusyReasonPostfix(OverlandSessionPlayers __instance, ref NetworkDictionary<PlayerRef, int> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, BusyReasonOffset, __result);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.PlayerRefToDevTeamMembership), MethodType.Getter)]
    [HarmonyPostfix]
    static void DevTeamPostfix(OverlandSessionPlayers __instance, ref NetworkDictionary<PlayerRef, NetworkBool> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, DevTeamOffset, __result);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.PlayerRefToSupporterEditionOwnership), MethodType.Getter)]
    [HarmonyPostfix]
    static void SupporterEditionPostfix(OverlandSessionPlayers __instance, ref NetworkDictionary<PlayerRef, NetworkBool> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, SupporterEditionOffset, __result);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.PlayerRefToNameDictionary), MethodType.Getter)]
    [HarmonyPostfix]
    static void NamePostfix(OverlandSessionPlayers __instance, ref NetworkDictionary<int, NetworkString<_32>> __result)
    {
        if (State.IsExpanded(__instance))
            __result = State.Dictionary(__instance, NameOffset, __result);
    }
}

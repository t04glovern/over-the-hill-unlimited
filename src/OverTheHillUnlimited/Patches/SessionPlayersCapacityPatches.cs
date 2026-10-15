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
    // From [NetworkBehaviourWeaved(390)]
    internal static readonly ExpandedState State = new(nameof(OverlandSessionPlayers), 390);

    static readonly int OrderedPlayerRefsOffset = State.AddArray(1);
    static readonly int PlayerColorsOffset = State.AddArray(1);
    static readonly int BusyReasonOffset = State.AddDictionary(1, 1);
    static readonly int DevTeamOffset = State.AddDictionary(1, 1);
    static readonly int SupporterEditionOffset = State.AddDictionary(1, 1);
    static readonly int NameOffset = State.AddDictionary(1, 33); // NetworkString<_32> is 33 words

    [HarmonyPatch(typeof(NetworkBehaviourUtils), nameof(NetworkBehaviourUtils.GetWordCount))]
    [HarmonyPostfix]
    static void GetWordCountPostfix(NetworkBehaviour behaviour, ref int __result)
    {
        State.ExpandIf<OverlandSessionPlayers>(behaviour, ref __result);
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.OrderedPlayerRefs), MethodType.Getter)]
    [HarmonyPostfix]
    static void OrderedPlayerRefsPostfix(OverlandSessionPlayers __instance, ref NetworkArray<PlayerRef> __result)
    {
        if (!State.IsExpanded(__instance))
            return;
        int length = PlayerListUiPatches.ClampToVanilla ? Plugin.VanillaMaxPlayers : ExpandedState.Capacity;
        __result = State.Array(__instance, OrderedPlayerRefsOffset, length, __result);
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

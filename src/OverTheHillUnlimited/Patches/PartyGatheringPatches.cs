using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Room for the whole party where the game needs everyone together. It checks every car is in an area sized for about
/// 4 of them:
/// - Travelling to another area needs every car in the portal's trigger, a sphere 10 to 12 m in radius
///   (Portal.AllPlayersInArea, checked by your game to show the prompt and let you press it, and by the host's before it
///   sends everyone). The Valley's portal is a boat at the end of a one-car-wide dock: its deck and the dock's end hold
///   4 cars.
/// - Camping at a cabin needs every car in its 40 by 40 m camping area (CabinCampTrigger.CheckPartyIsInCampingArea).
/// - Camping anywhere else needs every car within 15 m of whoever places the campfire (CampManager.CheckPartyIsCloseBy).
/// With more than 4 players, the party also counts as there once it's gathered as one group: every car within
/// LinkDistance of another, and at least one of them in the game's area. A queue down the dock to the boat, or cars
/// parked around a cabin's camping area, then counts, however the ground around it is laid out. The areas themselves
/// stay the game's size.
/// </summary>
public class PartyGatheringPatches
{
    /// <summary>The furthest a car can be from the next one and still be with the group: about two car lengths apart.</summary>
    internal const float LinkDistance = 15f;

    /// <summary>For the self-test: leaves the game's own checks alone, to compare them.</summary>
    internal static bool GameRulesOnly;

    /// <summary>For the self-test: a player left out of the group, as the game's own checks leave them out then.</summary>
    internal static PlayerRef? SittingOut;

    static bool Active =>
        !GameRulesOnly && Plugin.ConfigGatherAsGroup.Value && UnlimitedMode.Enabled &&
        OverlandSessionPlayers.Instance != null && OverlandSessionPlayers.Instance.GetPlayerCount() > Plugin.VanillaMaxPlayers;

    [HarmonyPatch(typeof(Portal), nameof(Portal.AllPlayersInArea))]
    [HarmonyPostfix]
    static void PortalGroup(Portal __instance, ref bool __result)
    {
        if (__result || !Active)
            return;
        var inPortal = __instance.playersInPortal;
        __result = inPortal != null && Gathered((player, car) => inPortal.Contains(player));
    }

    [HarmonyPatch(typeof(CabinCampTrigger), nameof(CabinCampTrigger.CheckPartyIsInCampingArea))]
    [HarmonyPostfix]
    static void CabinGroup(CabinCampTrigger __instance, ref bool __result)
    {
        if (__result || !Active)
            return;
        __result = Gathered((player, car) => __instance.ContainsCar(car));
    }

    [HarmonyPatch(typeof(CampManager), nameof(CampManager.CheckPartyIsCloseBy))]
    [HarmonyPostfix]
    static void CampGroup(CampManager __instance, NetworkObject requestingPlayerNetworkObject, ref bool __result)
    {
        var config = __instance.campingConfig;
        if (__result || !Active || requestingPlayerNetworkObject == null || config == null)
            return;
        var camper = requestingPlayerNetworkObject.transform.position;
        float distance = config.maxPartyMembersDistance;
        __result = Gathered((player, car) => Vector3.Distance(car.transform.position, camper) <= distance);
    }

    /// <summary>
    /// Whether every active player's car is in one group with a car the game counts as in the area: each within
    /// LinkDistance of another.
    /// </summary>
    internal static bool Gathered(Func<PlayerRef, NetworkObject, bool> inArea)
    {
        // Anything going wrong leaves the game's own answer
        try
        {
            return GatheredAsGroup(inArea);
        }
        catch (Exception e)
        {
            WarnOnce(e);
            return false;
        }
    }

    static bool _warned;

    static void WarnOnce(Exception e)
    {
        if (_warned)
            return;
        _warned = true;
        Plugin.Logger.LogWarning($"Couldn't check whether the party is gathered, so the game's own check stands: {e}");
    }

    static bool GatheredAsGroup(Func<PlayerRef, NetworkObject, bool> inArea)
    {
        var runner = OverlandNetworkRunner.Instance != null ? OverlandNetworkRunner.Instance.NetworkRunner : null;
        if (runner == null)
            return false;

        // Everyone in the session, from the game's player registry
        var session = OverlandSessionPlayers.Instance;
        if (session == null)
            return false;
        var ordered = session.OrderedPlayerRefs;
        var positions = new List<Vector3>();
        var reached = new List<bool>();
        for (int slot = 0; slot < ordered.Length; slot++)
        {
            var player = ordered[slot];
            if (player.IsNone || (SittingOut.HasValue && player.RawEncoded == SittingOut.Value.RawEncoded))
                continue;
            if (!runner.TryGetPlayerObject(player, out var car) || car == null)
                return false;
            positions.Add(car.transform.position);
            reached.Add(inArea(player, car));
        }

        // Spread out from the cars in the area to every car close enough to one already reached
        var queue = new Queue<int>();
        for (int i = 0; i < reached.Count; i++)
        {
            if (reached[i])
                queue.Enqueue(i);
        }
        if (queue.Count == 0)
            return false;
        float linkSquared = LinkDistance * LinkDistance;
        while (queue.Count > 0)
        {
            int from = queue.Dequeue();
            for (int i = 0; i < reached.Count; i++)
            {
                if (!reached[i] && (positions[i] - positions[from]).sqrMagnitude <= linkSquared)
                {
                    reached[i] = true;
                    queue.Enqueue(i);
                }
            }
        }
        return !reached.Contains(false);
    }

    // The portal's prompt is refreshed when a car enters or leaves its trigger. A car joining the group outside it
    // doesn't, so while the prompt is up it's refreshed whenever the answer changes.
    static readonly Dictionary<IntPtr, bool> ShownAnswers = new();
    static float _nextPromptCheck;

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.Render))]
    [HarmonyPostfix]
    static void RefreshPortalPrompts()
    {
        if (Time.unscaledTime < _nextPromptCheck)
            return;
        _nextPromptCheck = Time.unscaledTime + 0.5f;
        if (!Active)
        {
            ShownAnswers.Clear();
            return;
        }
        try
        {
            var points = SceneReferences.Instance != null ? SceneReferences.Instance.pointsOfInterest : null;
            for (int i = 0; points != null && i < points.Count; i++)
            {
                var portal = points[i] != null ? points[i].TryCast<Portal>() : null;
                if (portal == null || !portal.isShown)
                    continue;
                bool answer = portal.AllPlayersInArea();
                if (ShownAnswers.TryGetValue(portal.Pointer, out bool shown) && shown != answer)
                    portal.ShowHint(true);
                ShownAnswers[portal.Pointer] = answer;
            }
        }
        catch (Exception e)
        {
            WarnOnce(e);
        }
    }
}

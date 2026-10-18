using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Room for the whole party where the game needs everyone together. It checks every car is in an area sized for about
/// 4 of them:
/// - Travelling to another area needs every car in the portal's trigger, a sphere 10 to 12 m in radius
///   (Portal.AllPlayersInArea). The game checks it on your side to show the prompt and let you press Interact
///   (Portal.ShowHint, Portal.InteractPerformed), and on the host's before sending everyone
///   (PortalNetworkManager.RPC_RequestActivate). The Valley's portal is a boat at the end of a one-car-wide dock: its
///   deck and the end of the dock hold 4 cars.
/// - Camping at a cabin needs every car in its 40 by 40 m camping area (CabinCampTrigger.CheckPartyIsInCampingArea,
///   checked on the camper's side).
/// - Camping anywhere else needs every car within 15 m, across the ground, of whoever places the campfire
///   (CampManager.CheckPartyIsCloseBy, checked on the camper's side).
/// With more than 4 players, the party also counts as there once it's gathered as one group: every car in the game's
/// area, or linked to it by a chain of cars, each within LinkDistance of the next across the ground. The cars in the
/// area count as linked wherever they are in it, as the game counts them. A queue down the dock to the boat, or cars
/// parked around a cabin's camping area, then counts, however the ground around it is laid out. The areas keep the
/// game's size, and the game's own answer still counts.
///
/// In the game, everyone being at the portal means whoever presses Interact is too. Interact is one input for the
/// whole game, so on your side the group only counts while your own car is in the portal, as it would have to be.
/// The host allows HostLinkSlack more between cars, so a group that's together on a player's screen is together on
/// the host's, where cars are a moment behind. Anything going wrong leaves the game's answer.
/// </summary>
public class PartyGatheringPatches
{
    /// <summary>The furthest a car can be from the next one, across the ground, and still be with the group.</summary>
    internal const float LinkDistance = 15f;

    /// <summary>How much further apart the host allows cars, for the moment its view of them lags a player's.</summary>
    internal const float HostLinkSlack = 2f;

    /// <summary>For the self-test: leaves the game's own checks alone, to compare them.</summary>
    internal static bool GameRulesOnly;

    /// <summary>How many times working out the group failed, leaving the game's answer (for the self-test).</summary>
    internal static int Failures { get; private set; }

    // Set while the host decides on a request to travel
    static bool _hostDeciding;

    /// <summary>For the self-test: runs a check the way the host decides on a request to travel.</summary>
    internal static bool AsHost(Func<bool> check)
    {
        _hostDeciding = true;
        try
        {
            return check();
        }
        finally
        {
            _hostDeciding = false;
        }
    }

    internal static bool Active =>
        !GameRulesOnly && Plugin.ConfigGatherAsGroup.Value && UnlimitedMode.Enabled &&
        OverlandSessionPlayers.Instance != null && OverlandSessionPlayers.Instance.GetPlayerCount() > Plugin.VanillaMaxPlayers;

    [HarmonyPatch(typeof(PortalNetworkManager), nameof(PortalNetworkManager.RPC_RequestActivate))]
    [HarmonyPrefix]
    static void HostDecides() => _hostDeciding = true;

    // Also cleared every frame (RefreshPortalPrompts), as RPCs run in the simulation, never during Render
    [HarmonyPatch(typeof(PortalNetworkManager), nameof(PortalNetworkManager.RPC_RequestActivate))]
    [HarmonyPostfix]
    static void HostDecided() => _hostDeciding = false;

    [HarmonyPatch(typeof(Portal), nameof(Portal.AllPlayersInArea))]
    [HarmonyPostfix]
    static void PortalGroup(Portal __instance, ref bool __result)
    {
        if (__result || !Active)
            return;
        var inPortal = __instance.playersInPortal;
        if (inPortal == null || (!_hostDeciding && !inPortal.Contains(OverlandNetworkRunner.LocalPlayerRef)))
            return;
        __result = Gathered((player, car) => inPortal.Contains(player), skipPlayersWithoutCars: false);
    }

    [HarmonyPatch(typeof(CabinCampTrigger), nameof(CabinCampTrigger.CheckPartyIsInCampingArea))]
    [HarmonyPostfix]
    static void CabinGroup(CabinCampTrigger __instance, ref bool __result)
    {
        if (__result || !Active)
            return;
        __result = Gathered((player, car) => __instance.ContainsCar(car), skipPlayersWithoutCars: false);
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
        // The game measures across the ground and leaves out anyone without a car
        __result = Gathered((player, car) => AcrossTheGround(car.transform.position, camper) <= distance,
            skipPlayersWithoutCars: true);
    }

    /// <summary>
    /// Whether every player's car is in the area by the game's own test, or linked to a car that is by a chain of cars,
    /// each within LinkDistance of the next across the ground.
    /// </summary>
    internal static bool Gathered(Func<PlayerRef, NetworkObject, bool> inArea, bool skipPlayersWithoutCars)
    {
        try
        {
            return GatheredAsGroup(inArea, skipPlayersWithoutCars);
        }
        catch (Exception e)
        {
            Failures++;
            if (Failures == 1)
                Plugin.Logger.LogWarning($"Couldn't check whether the party is gathered, so the game's own check stands: {e}");
            return false;
        }
    }

    internal static float AcrossTheGround(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x, z = a.z - b.z;
        return MathF.Sqrt(x * x + z * z);
    }

    // Reused, as the camp tool checks every frame while it's open
    static readonly List<Vector3> Positions = new();
    static readonly List<bool> Reached = new();
    static readonly Queue<int> ToVisit = new();

    static bool GatheredAsGroup(Func<PlayerRef, NetworkObject, bool> inArea, bool skipPlayersWithoutCars)
    {
        var runner = OverlandNetworkRunner.Instance != null ? OverlandNetworkRunner.Instance.NetworkRunner : null;
        var session = OverlandSessionPlayers.Instance;
        if (runner == null || session == null)
            return false;

        // Everyone in the session, from the game's player registry
        Positions.Clear();
        Reached.Clear();
        ToVisit.Clear();
        var ordered = session.OrderedPlayerRefs;
        for (int slot = 0; slot < ordered.Length; slot++)
        {
            var player = ordered[slot];
            if (player.IsNone)
                continue;
            if (!runner.TryGetPlayerObject(player, out var car) || car == null)
            {
                if (skipPlayersWithoutCars)
                    continue;
                return false;
            }
            bool reached = inArea(player, car);
            Positions.Add(car.transform.position);
            Reached.Add(reached);
            if (reached)
                ToVisit.Enqueue(Positions.Count - 1);
        }
        if (ToVisit.Count == 0)
            return false;

        // Spread out from the cars in the area to every car close enough to one already reached
        float link = LinkDistance + (_hostDeciding ? HostLinkSlack : 0);
        while (ToVisit.Count > 0)
        {
            int from = ToVisit.Dequeue();
            for (int i = 0; i < Reached.Count; i++)
            {
                if (!Reached[i] && AcrossTheGround(Positions[i], Positions[from]) <= link)
                {
                    Reached[i] = true;
                    ToVisit.Enqueue(i);
                }
            }
        }
        return !Reached.Contains(false);
    }

    // The game refreshes the portal's prompt when a car enters or leaves its trigger, but not when a car joins the
    // group outside it. So while the prompt is up, it's refreshed once the answer has differed from the one on screen
    // for a second, which also keeps a car parked right at the limit from flickering it.
    sealed class Prompt
    {
        public bool Shown;
        public int Differed;
    }

    static readonly Dictionary<IntPtr, Prompt> Prompts = new();
    static float _nextPromptCheck;
    static bool _refreshing;

    // The answer the prompt on screen was built from
    [HarmonyPatch(typeof(Portal), nameof(Portal.ShowHint))]
    [HarmonyPostfix]
    static void RememberPrompt(Portal __instance, bool show)
    {
        try
        {
            if (!show || !Active)
            {
                Prompts.Remove(__instance.Pointer);
                return;
            }
            if (!Prompts.TryGetValue(__instance.Pointer, out var prompt))
                Prompts[__instance.Pointer] = prompt = new Prompt();
            prompt.Shown = __instance.AllPlayersInArea();
            prompt.Differed = 0;
        }
        catch (Exception e)
        {
            Failures++;
            if (Failures == 1)
                Plugin.Logger.LogWarning($"Couldn't follow the portal's prompt: {e}");
        }
    }

    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.Render))]
    [HarmonyPostfix]
    static void RefreshPortalPrompts()
    {
        _hostDeciding = false;
        if (_refreshing || Time.unscaledTime < _nextPromptCheck || Prompts.Count == 0)
            return;
        _nextPromptCheck = Time.unscaledTime + 0.5f;
        _refreshing = true;
        try
        {
            var points = SceneReferences.Instance != null ? SceneReferences.Instance.pointsOfInterest : null;
            for (int i = 0; points != null && i < points.Count; i++)
            {
                var portal = points[i] != null ? points[i].TryCast<Portal>() : null;
                if (portal == null || !Prompts.TryGetValue(portal.Pointer, out var prompt))
                    continue;
                if (!portal.isShown || !Active)
                {
                    Prompts.Remove(portal.Pointer);
                    continue;
                }
                prompt.Differed = portal.AllPlayersInArea() != prompt.Shown ? prompt.Differed + 1 : 0;
                if (prompt.Differed >= 2)
                    portal.ShowHint(true);
            }
        }
        catch (Exception e)
        {
            Failures++;
            if (Failures == 1)
                Plugin.Logger.LogWarning($"Couldn't refresh the portal's prompt: {e}");
        }
        finally
        {
            _refreshing = false;
        }
    }
}

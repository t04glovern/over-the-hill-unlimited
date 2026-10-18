using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Wider zones for the places each player reaches on their own: scenic points, crates, cabins and merchants count a car
/// once it's in their trigger, sized for a few cars (most are 8 m in radius or 8 m across, on summits, at the ends of
/// tracks and in clearings). With more than 4 players, each grows by √(players / 4) across, so its area grows with the
/// number of cars: ×1.4 for 8 players and ×2 for 16, the same as the meteorite reward reach (MeteoriteBalancePatches).
/// The markers the game draws on the ground for these zones grow with them, and so does the ring a meteorite shows for
/// its reach. With 4 players or fewer, or with Unlimited mode off, everything is the game's size.
///
/// Every player's game checks its own car against these triggers, so each sizes them from the session's player count,
/// which every player in the session sees the same.
/// </summary>
public class ReachZonePatches
{
    // A zone's trigger with the size the game gave it, and the marker the game draws for it on the ground
    sealed class Zone
    {
        public Collider Collider;
        public Vector3 GameBox;
        public float GameRadius;
        public CylinderMarker Marker;
    }

    static readonly Dictionary<IntPtr, Zone> Zones = new();
    static IntPtr _scene;
    static int _players = -1;
    static float _factor = 1;

    /// <summary>How much wider the zones are for this many players.</summary>
    internal static float Factor(int players) =>
        Plugin.ConfigScaleReachZones.Value && UnlimitedMode.Enabled && players > Plugin.VanillaMaxPlayers
            ? MathF.Sqrt(players / (float)Plugin.VanillaMaxPlayers)
            : 1;

    /// <summary>The factor the zones in the current scene have now (for the self-test).</summary>
    internal static float CurrentFactor => _factor;

    /// <summary>The size the game gave a zone's trigger: a box's size, or a sphere's or capsule's radius.</summary>
    internal static bool TryGetGameSize(Collider collider, out Vector3 box, out float radius)
    {
        box = default;
        radius = 0;
        if (collider == null || !Zones.TryGetValue(collider.Pointer, out var zone))
            return false;
        box = zone.GameBox;
        radius = zone.GameRadius;
        return true;
    }

    /// <summary>Whether this point of interest has a zone the mod widens.</summary>
    internal static bool IsReachZone(PointOfInterest point) =>
        point.TryCast<ScenicPoint>() != null || point.TryCast<Crate>() != null || point.TryCast<Cabin>() != null ||
        point.TryCast<Merchant>() != null;

    // Runs every frame on every player's game, so the zones follow players joining and leaving
    [HarmonyPatch(typeof(OverlandSessionPlayers), nameof(OverlandSessionPlayers.Render))]
    [HarmonyPostfix]
    static void FollowPlayerCount(OverlandSessionPlayers __instance)
    {
        var scene = SceneReferences.Instance;
        IntPtr scenePointer = scene != null ? scene.Pointer : IntPtr.Zero;
        int players = __instance.GetPlayerCount();
        if (players == _players && scenePointer == _scene)
            return;

        if (scenePointer != _scene)
            Zones.Clear();
        _scene = scenePointer;
        _players = players;
        try
        {
            Apply(scene, Factor(players));
        }
        catch (Exception e)
        {
            WarnOnce(e);
        }
    }

    static bool _warned;

    static void WarnOnce(Exception e)
    {
        if (_warned)
            return;
        _warned = true;
        Plugin.Logger.LogWarning($"Couldn't resize the reach zones, so some may be the game's size: {e}");
    }

    /// <summary>Sizes every zone in the scene for this factor (the self-test calls it directly).</summary>
    internal static void Apply(SceneReferences scene, float factor)
    {
        _factor = factor;
        var points = scene != null ? scene.pointsOfInterest : null;
        for (int i = 0; points != null && i < points.Count; i++)
        {
            var point = points[i];
            if (point == null || !IsReachZone(point))
                continue;
            var marker = point.pointOfInterestWorld != null ? point.pointOfInterestWorld.DecalProjector : null;
            foreach (var collider in point.GetComponents<Collider>())
            {
                if (collider.isTrigger)
                    Resize(collider, marker, factor);
            }
        }

        foreach (var visuals in UnityEngine.Object.FindObjectsOfType<MeteoriteVisuals>())
            ShowMeteoriteReach(visuals, resizeShownRing: true);
    }

    static void Resize(Collider collider, CylinderMarker marker, float factor)
    {
        if (!Zones.TryGetValue(collider.Pointer, out var zone))
        {
            if (factor == 1)
                return;
            zone = new Zone { Collider = collider, Marker = marker };
            var box = collider.TryCast<BoxCollider>();
            if (box != null)
                zone.GameBox = box.size;
            var sphere = collider.TryCast<SphereCollider>();
            if (sphere != null)
                zone.GameRadius = sphere.radius;
            var capsule = collider.TryCast<CapsuleCollider>();
            if (capsule != null)
                zone.GameRadius = capsule.radius;
            Zones[collider.Pointer] = zone;
        }

        // The marker keeps the proportion the game drew it in: its across (x and y; z is how deep it projects) to the
        // trigger's across, measured before the change
        float before = Across(zone.Collider);
        SetSize(zone, factor);
        if (zone.Marker != null && before > 0)
        {
            float ratio = Across(zone.Collider) / before;
            var size = zone.Marker.m_Size;
            zone.Marker.m_Size = new Vector3(size.x * ratio, size.y * ratio, size.z);
            zone.Marker.geometryDirty = true;
        }
    }

    static void SetSize(Zone zone, float factor)
    {
        var box = zone.Collider.TryCast<BoxCollider>();
        if (box != null)
            box.size = new Vector3(zone.GameBox.x * factor, zone.GameBox.y, zone.GameBox.z * factor);
        var sphere = zone.Collider.TryCast<SphereCollider>();
        if (sphere != null)
            sphere.radius = zone.GameRadius * factor;
        var capsule = zone.Collider.TryCast<CapsuleCollider>();
        if (capsule != null)
            capsule.radius = zone.GameRadius * factor;
    }

    /// <summary>A zone trigger's size across the ground: a box's x, or a sphere's or capsule's radius.</summary>
    internal static float Across(Collider collider)
    {
        var box = collider.TryCast<BoxCollider>();
        if (box != null)
            return box.size.x;
        var sphere = collider.TryCast<SphereCollider>();
        if (sphere != null)
            return sphere.radius;
        var capsule = collider.TryCast<CapsuleCollider>();
        return capsule != null ? capsule.radius : 0;
    }

    // The meteorite draws its reward reach as a ring when it lands, from the distance its visuals hold
    [HarmonyPatch(typeof(MeteoriteVisuals), nameof(MeteoriteVisuals.SwitchMeteoriteState))]
    [HarmonyPrefix]
    static void ShowWidenedReach(MeteoriteVisuals __instance)
    {
        try
        {
            ShowMeteoriteReach(__instance, resizeShownRing: false);
        }
        catch (Exception e)
        {
            WarnOnce(e);
        }
    }

    static void ShowMeteoriteReach(MeteoriteVisuals visuals, bool resizeShownRing)
    {
        var meteorite = visuals.GetComponentInParent<Meteorite>();
        if (meteorite == null)
            return;
        float reach = MeteoriteBalancePatches.Reach(MeteoriteBalancePatches.GameReach(meteorite), SessionPlayers);
        visuals.prizeAwardMaxDistance = reach;
        var ring = visuals.areaOfEffectDecalProjector;
        if (resizeShownRing && ring != null)
        {
            // The game sizes the ring's x and y (across the ground) to the reach's diameter
            ring.m_Size = new Vector3(2 * reach, 2 * reach, ring.m_Size.z);
            ring.geometryDirty = true;
        }
    }

    static int SessionPlayers => OverlandSessionPlayers.Instance != null ? OverlandSessionPlayers.Instance.GetPlayerCount() : 0;
}

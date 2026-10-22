using System;
using System.Collections.Generic;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Room for more than 4 cars where players spawn in the map, such as cabins. Each spawnable point of interest has a few
/// spawn points (4 at the starting cabin), and once they're taken SpawnablePointOfInterest.GetNextFreeSpawnPoint puts
/// everyone else on the first one ("All spawn points ... are occupied"), on top of whoever is there. That's every
/// player from the 5th on when a session starts. The first time a point of interest is used, this adds spawn points
/// around its own, up to ExpandedState.Capacity, wherever there's open, fairly level, dry ground. Points of interest sit
/// among trees and rocks, so each spot is checked rather than laid out in a fixed pattern. Only the host picks spawn
/// points.
///
/// A spot stands on the terrain, or, where the game's own spawn point is on a dock, deck or bridge, on one at about its
/// height; never on a rock ledge or a roof. Never under water: the Valley's portal is a boat at the end of a
/// one-car-wide dock, its spawn points on the deck and the dock's end, and measuring from the lakebed put the rest of
/// the party in the lake. The search reaches far enough to find the shore from there. A car with nobody driving rolls a
/// few metres after landing on a slope, so a spot also needs room to roll: dry ground all around it that doesn't fall
/// away steeply, back from the water's edge and from the top of a drop, nothing solid in the way downhill, and no other
/// spot there. So the boat keeps only the game's own spawn points, and the rest of the party lands on the shore.
///
/// Where those run out, among rocks or on a small plateau, the search takes spots without room to roll (though still
/// not at the brink of a drop, where a car bouncing as it lands goes over the edge), and then any slots still empty
/// get rows in front of and behind the game's own points: on clear, dry ground where there is some, and as a last
/// resort anywhere. A car that might roll is better than one dropped on top of another.
///
/// Portals are spawnable points of interest too, with 4 spawn points each, but arriving through one doesn't go through
/// GetNextFreeSpawnPoint: Portal.AssignPlayersToSpawnPoints gives each player the spawn point at their position in
/// ActivePlayers and skips anyone past the end, who then has no car and can't respawn. So portals are padded before
/// that.
/// </summary>
public class CabinSpawnPointPatches
{
    // Candidate spots: rings around each of the game's spawn points, nearest rings first, out to 105 m, with candidates
    // about a car's width apart around each ring so a narrow way through rocks isn't missed
    const float SearchStep = 3.5f;
    const int SearchRings = 30;

    /// <summary>The furthest a spawn point is added from the game's own.</summary>
    internal const float MaxReach = SearchStep * SearchRings;

    // A spot needs this much room from the middle of every other spawn point's car, across and along it (a car's width
    // or length and a door's width), ground no steeper than about 15°, and nothing but terrain in a car-sized box above
    // it
    const float SpacingAcross = 4f;
    const float SpacingAlong = 6.5f;
    const float MinGroundUp = 0.966f;
    static readonly Vector3 CarHalfSize = new(1.3f, 0.9f, 2.6f);

    // The box that has to be clear: the car and a hand's width around it, from just above the ground, so a knee-high
    // rock under a wheel counts too, up to the top of a car dropped from as high as a spawn point can be, so a rock
    // overhanging the spot counts too
    static readonly Vector3 ClearHalfSize = new(CarHalfSize.x + 0.3f, 1.9f, CarHalfSize.z + 0.3f);
    const float BoxClearance = 0.2f;

    // Where the game's own spawn point is on a dock, deck or bridge, one counts as ground at about that height; much
    // higher, it's a roof or the top of a rock
    const float MaxDeckStep = 2.5f;

    // Water this far above what a car would stand on means it's under water
    const float MaxWaterDepth = 0.3f;

    // How far a spot stays back from the water and from a drop, checked at these distances all around, and how much the
    // ground there can fall away per metre (about 20°)
    static readonly float[] RollingRoom = { 5f, 10f };
    const float MaxFallPerMetre = 0.36f;

    // Even without room to roll, a spot keeps this far back from the brink of a drop: a car dropped onto its spot
    // bounces, and one right beside a steep bank goes over it
    static readonly float[] BrinkRoom = { 4f };

    // How far a car's corner can be above or below its middle: across a slope, more tips it onto its side as it lands
    const float MaxCornerStep = 0.5f;

    // A car on ground sloping more than this (the ground's normal leaning about 3°) rolls downhill after landing, up to
    // about RollDistance, so no other spot goes in that strip, and nothing solid can be in the way, checked every
    // RollCheckStep
    const float MinRollingSlope = 0.05f;
    const float RollDistance = 12f;
    const float RollCheckStep = 4f;

    // Rows in front of and behind the game's own spawn points, for slots the search couldn't fill: far enough out that
    // even the last pass, which takes any spot, always fills every slot
    const float FallbackRowSpacing = 7f;
    const int MaxFallbackRows = 20;

    static readonly HashSet<int> _padded = new();
    static int _waterLayer = -2;

    [HarmonyPatch(typeof(SpawnablePointOfInterest), nameof(SpawnablePointOfInterest.GetNextFreeSpawnPoint))]
    [HarmonyPrefix]
    static void PadOnSpawn(SpawnablePointOfInterest __instance)
    {
        if (!UnlimitedMode.Enabled)
            return;
        PadSpawnPoints(__instance);
        FillRemainingSlots(__instance);
    }

    [HarmonyPatch(typeof(Portal), nameof(Portal.AssignPlayersToSpawnPoints))]
    [HarmonyPrefix]
    static void PadOnPortalArrival(Portal __instance)
    {
        if (!UnlimitedMode.Enabled)
            return;
        PadSpawnPoints(__instance);
        FillRemainingSlots(__instance);
    }

    /// <summary>Adds spawn points on open, dry ground around the game's own, once per point of interest.</summary>
    internal static void PadSpawnPoints(SpawnablePointOfInterest pointOfInterest)
    {
        if (!_padded.Add(pointOfInterest.GetInstanceID()))
            return;
        var spawnPoints = pointOfInterest.spawnPoints;
        if (spawnPoints == null || spawnPoints.Count == 0 || spawnPoints.Count >= ExpandedState.Capacity)
            return;

        int vanillaCount = spawnPoints.Count;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var taken = Taken(spawnPoints);
        var sources = Sources(spawnPoints, vanillaCount);

        // Spots with room to roll first; then, for any slots left, spots without; then spots in another car's way
        // downhill, which only matters if a car rolls, and a player's idle car holds still on its automatic handbrake
        var found = new List<int>();
        foreach (var (roomToRoll, rollStrips) in new[] { (true, true), (false, true), (false, false) })
        {
            int before = spawnPoints.Count;
            for (int ring = 1; ring <= SearchRings && spawnPoints.Count < ExpandedState.Capacity; ring++)
            {
                float radius = SearchStep * ring;
                int steps = Math.Max(8, (int)Math.Ceiling(2 * Math.PI * radius / SpacingAcross));
                foreach (var source in sources)
                {
                    for (int step = 0; step < steps && spawnPoints.Count < ExpandedState.Capacity; step++)
                    {
                        var candidate = source.Transform.position +
                                        Quaternion.AngleAxis(360f * step / steps, Vector3.up) * source.Transform.forward * radius;
                        if (IsNear(new Spot(candidate, source.Heading, Vector3.zero), taken, rollStrips) ||
                            !TryFindSpot(candidate, source, roomToRoll, out var surface, out var normal) ||
                            IsBlocked(surface, source.Heading))
                            continue;
                        var spot = new Spot(surface + Vector3.up * source.Height, source.Heading, DownhillOf(normal));
                        if (IsNear(spot, taken, rollStrips))
                            continue;
                        Add(spawnPoints, source, spot);
                        taken.Add(spot);
                    }
                }
            }
            found.Add(spawnPoints.Count - before);
        }
        Plugin.Logger.LogInfo(spawnPoints.Count > vanillaCount
            ? $"Spawn points at {pointOfInterest.name} padded from {vanillaCount} to {spawnPoints.Count} in {timer.ElapsedMilliseconds} ms " +
              $"({found[0]} with room to roll, {found[1]} without, {found[2]} in another's way downhill)"
            : $"No open ground for more spawn points at {pointOfInterest.name} ({timer.ElapsedMilliseconds} ms)");
    }

    /// <summary>
    /// Fills any slots up to ExpandedState.Capacity that PadSpawnPoints couldn't, with rows in front of and behind the
    /// game's own spawn points: on clear, dry ground where there's some, then on whatever is there, so every player gets
    /// a spot of their own.
    /// </summary>
    internal static void FillRemainingSlots(SpawnablePointOfInterest pointOfInterest)
    {
        var spawnPoints = pointOfInterest.spawnPoints;
        if (spawnPoints == null || spawnPoints.Count == 0 || spawnPoints.Count >= ExpandedState.Capacity)
            return;

        int before = spawnPoints.Count;
        var taken = Taken(spawnPoints);
        var sources = Sources(spawnPoints, Math.Min(before, Plugin.VanillaMaxPlayers));

        // Rows behind and in front, each with a spot to either side, further out until every slot is filled. The game's
        // own spawn points can be close together, so their rows overlap; there are always more rows further out.
        foreach (bool dryOnly in new[] { true, false })
        {
            for (int row = 1; row <= MaxFallbackRows && spawnPoints.Count < ExpandedState.Capacity; row++)
            {
                foreach (float direction in new[] { -1f, 1f })
                {
                    foreach (float side in new[] { 0f, -1f, 1f })
                    {
                        foreach (var source in sources)
                        {
                            if (spawnPoints.Count >= ExpandedState.Capacity)
                                break;
                            var forward = source.Heading * Vector3.forward;
                            var right = source.Heading * Vector3.right;
                            var position = source.Transform.position + forward * (direction * FallbackRowSpacing * row) +
                                           right * (side * SpacingAcross);
                            var downhill = Vector3.zero;
                            if (TryFindSpot(position, source, false, out var surface, out var normal) &&
                                !IsBlocked(surface, source.Heading))
                            {
                                position = surface + Vector3.up * source.Height;
                                downhill = DownhillOf(normal);
                            }
                            else if (dryOnly)
                            {
                                continue;
                            }
                            else if (TryFindAnyGround(position, out var ground))
                            {
                                // The last resort: on whatever is there, wet or steep, rather than buried or in the air
                                position = ground + Vector3.up * source.Height;
                            }
                            var spot = new Spot(position, source.Heading, downhill);
                            if (IsNear(spot, taken, rollStrips: false))
                                continue;
                            Add(spawnPoints, source, spot);
                            taken.Add(spot);
                        }
                    }
                }
            }
        }
        Plugin.Logger.LogInfo($"Spawn points at {pointOfInterest.name} filled from {before} to {spawnPoints.Count} with rows");
    }

    // One of the game's own spawn points, as the search works from it: what it stands on, whether that's a dock, deck or
    // bridge rather than the terrain, how high above the ground its cars are placed, and which way they face, upright
    readonly struct Source
    {
        public readonly Transform Transform;
        public readonly Vector3 Ground;
        public readonly bool OnStructure;
        public readonly float Height;
        public readonly Quaternion Heading;

        public Source(Transform transform, Vector3 ground, bool onStructure)
        {
            Transform = transform;
            Ground = ground;
            OnStructure = onStructure;
            Height = Mathf.Clamp(transform.position.y - ground.y, MinSpawnHeight, MaxSpawnHeight);
            Heading = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }
    }

    // A spawn point's ground is looked for from just above it, so a roof, an overhang or a tunnel's ceiling over it
    // doesn't count; and its cars are placed between these heights above the ground, whatever the game's own is
    const float SourceCastHeight = 2f;
    const float MinSpawnHeight = 0.3f;
    const float MaxSpawnHeight = 1.5f;

    static List<Source> Sources(Il2CppSystem.Collections.Generic.List<SpawnPoint> spawnPoints, int count)
    {
        var sources = new List<Source>();
        for (int i = 0; i < count; i++)
        {
            var transform = spawnPoints[i].transform;
            if (TryFindStanding(transform.position, null, true, out var ground, out _, out bool onTerrain, SourceCastHeight))
                sources.Add(new Source(transform, ground, !onTerrain));
        }
        return sources;
    }

    static void Add(Il2CppSystem.Collections.Generic.List<SpawnPoint> spawnPoints, Source source, Spot spot)
    {
        var clone = Object.Instantiate(source.Transform.gameObject, spot.Position, spot.Rotation, source.Transform.parent);
        clone.name = $"{source.Transform.gameObject.name} (slot {spawnPoints.Count + 1})";
        spawnPoints.Add(clone.GetComponent<SpawnPoint>());
    }

    // Where a car can land, found from one of the game's spawn points: dry, fairly level, all four wheels down, and if
    // asked, with room to roll. On the terrain, unless the game's own spawn point stands on a dock, deck or bridge: then
    // one at about its height counts too. Never on a rock or a roof.
    static bool TryFindSpot(Vector3 around, Source source, bool roomToRoll, out Vector3 surface, out Vector3 normal) =>
        TryFindStanding(around, source.Ground.y, source.OnStructure, out surface, out normal, out _) &&
        normal.y >= MinGroundUp && FootprintStands(surface, source.Heading, source.OnStructure) &&
        NothingFallsAway(surface, BrinkRoom, source.OnStructure) &&
        (!roomToRoll || RoomToRoll(surface, normal, source.OnStructure));

    // What a car would stand on at a spot: the first solid, unmoving thing below that isn't water. Something other than
    // the terrain counts only with structures, and then only near nearHeight (a dock or deck at the level of the spot
    // it's found from), and nothing counts if water covers it.
    static bool TryFindStanding(Vector3 around, float? nearHeight, bool structures, out Vector3 surface, out Vector3 normal,
        out bool onTerrain, float castFrom = 20)
    {
        surface = normal = default;
        onTerrain = false;
        if (_waterLayer == -2)
            _waterLayer = LayerMask.NameToLayer("Water");
        var hits = Physics.RaycastAll(around + Vector3.up * castFrom, Vector3.down, castFrom + 40, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);
        float waterTop = float.NegativeInfinity;
        foreach (var hit in System.Linq.Enumerable.OrderBy(hits, h => h.distance))
        {
            var collider = hit.collider;
            if (collider == null)
                continue;
            if (collider.gameObject.layer == _waterLayer)
            {
                waterTop = Math.Max(waterTop, hit.point.y);
                continue;
            }
            if (collider.isTrigger || (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic))
                continue;
            bool terrain = collider.TryCast<TerrainCollider>() != null;
            if (!terrain && (!structures || (nearHeight.HasValue && Math.Abs(hit.point.y - nearHeight.Value) > MaxDeckStep)))
                continue;
            if (waterTop - hit.point.y > MaxWaterDepth ||
                (WaterBodyRegistry.TryGetWaterSurface(hit.point, out float water) && water - hit.point.y > MaxWaterDepth))
                return false;
            surface = hit.point;
            normal = hit.normal;
            onTerrain = terrain;
            return true;
        }
        return false;
    }

    // The first solid, unmoving thing below a spot, water included
    static bool TryFindAnyGround(Vector3 around, out Vector3 ground)
    {
        ground = default;
        var hits = Physics.RaycastAll(around + Vector3.up * 20, Vector3.down, 60, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        foreach (var hit in hits)
        {
            if (hit.collider == null || (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) ||
                hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            ground = hit.point;
        }
        return nearest < float.PositiveInfinity;
    }

    // The car's four corners stand on dry ground too, at about the middle's height, so it doesn't hang off a dock or a
    // bank and slide into the water
    static bool FootprintStands(Vector3 surface, Quaternion rotation, bool structures)
    {
        foreach (var corner in Corners)
        {
            if (!TryFindStanding(surface + rotation * corner, surface.y, structures, out var cornerSurface, out _, out _) ||
                Math.Abs(cornerSurface.y - surface.y) > MaxCornerStep)
                return false;
        }
        return true;
    }

    // Dry ground all around that doesn't fall away steeply, and on a slope a clear way downhill, so a car that rolls
    // after landing doesn't roll into a lake or river, off a ledge, or against a rock, and end up on its side
    static bool RoomToRoll(Vector3 surface, Vector3 normal, bool structures)
    {
        if (!NothingFallsAway(surface, RollingRoom, structures))
            return false;

        // On a slope, nothing solid downhill for the car to roll onto and tip over against
        var downhill = DownhillOf(normal);
        if (downhill == Vector3.zero)
            return true;
        var heading = Quaternion.LookRotation(downhill);
        for (float distance = RollCheckStep; distance <= RollDistance; distance += RollCheckStep)
        {
            if (!TryFindStanding(surface + downhill * distance, surface.y, structures, out var ground, out _, out _) ||
                IsBlocked(ground, heading))
                return false;
        }
        return true;
    }

    // Dry ground at these distances all around that doesn't fall away more steeply than MaxFallPerMetre
    static bool NothingFallsAway(Vector3 surface, float[] distances, bool structures)
    {
        foreach (float distance in distances)
        {
            for (int step = 0; step < 8; step++)
            {
                var offset = Quaternion.AngleAxis(45 * step, Vector3.up) * Vector3.forward * distance;
                if (!TryFindStanding(surface + offset, surface.y, structures, out var ground, out _, out _) ||
                    surface.y - ground.y > distance * MaxFallPerMetre)
                    return false;
            }
        }
        return true;
    }

    static readonly Vector3[] Corners =
    {
        new(-CarHalfSize.x, 0, -CarHalfSize.z), new(CarHalfSize.x, 0, -CarHalfSize.z),
        new(-CarHalfSize.x, 0, CarHalfSize.z), new(CarHalfSize.x, 0, CarHalfSize.z),
    };

    // A spawn point's car: where it lands, which way it faces, and which way it rolls (zero on level ground)
    readonly struct Spot
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Downhill;

        public Spot(Vector3 position, Quaternion rotation, Vector3 downhill)
        {
            Position = position;
            Rotation = rotation;
            Downhill = downhill;
        }
    }

    static List<Spot> Taken(Il2CppSystem.Collections.Generic.List<SpawnPoint> spawnPoints)
    {
        var taken = new List<Spot>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            var transform = spawnPoints[i].transform;
            var downhill = TryFindStanding(transform.position, null, true, out _, out var normal, out _, SourceCastHeight)
                ? DownhillOf(normal)
                : Vector3.zero;
            taken.Add(new Spot(transform.position, transform.rotation, downhill));
        }
        return taken;
    }

    static Vector3 DownhillOf(Vector3 normal)
    {
        var across = new Vector3(normal.x, 0, normal.z);
        return across.magnitude > MinRollingSlope ? across.normalized : Vector3.zero;
    }

    // Whether a car at a spot would be in another's way: within a car's width beside, or a car's length in front of or
    // behind, the car at any spot taken, or that car within the same of it, or either in the strip the other rolls down.
    // Across the ground, so it can be checked before knowing the spot's height: a spot right above or below another is
    // too close too.
    static bool IsNear(Spot spot, List<Spot> taken, bool rollStrips = true)
    {
        foreach (var other in taken)
        {
            if (Within(spot, other.Position) || Within(other, spot.Position) ||
                (rollStrips && (RollsInto(spot, other.Position) || RollsInto(other, spot.Position))))
                return true;
        }
        return false;
    }

    static bool Within(Spot car, Vector3 point)
    {
        var local = Quaternion.Inverse(Quaternion.Euler(0, car.Rotation.eulerAngles.y, 0)) * (point - car.Position);
        return Math.Abs(local.x) < SpacingAcross && Math.Abs(local.z) < SpacingAlong;
    }

    static bool RollsInto(Spot car, Vector3 point)
    {
        if (car.Downhill == Vector3.zero)
            return false;
        var offset = point - car.Position;
        float along = offset.x * car.Downhill.x + offset.z * car.Downhill.z;
        float across = Math.Abs(offset.x * car.Downhill.z - offset.z * car.Downhill.x);
        return along > 0 && along < RollDistance && across < SpacingAcross;
    }

    // Anything but terrain where the car would stand: trees, rocks, buildings, other cars. A rock's collider is usually
    // a mesh, which an overlap only finds where the box crosses its surface, so a box inside a big boulder looks clear;
    // so the spot also needs open sky, nothing over its middle or corners
    static bool IsBlocked(Vector3 ground, Quaternion rotation)
    {
        var centre = ground + Vector3.up * (BoxClearance + ClearHalfSize.y);
        var colliders = Physics.OverlapBox(centre, ClearHalfSize, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var collider in colliders)
        {
            if (IsObstacle(collider))
                return true;
        }
        foreach (var point in SkyPoints)
        {
            var top = ground + rotation * point + Vector3.up * OpenSky;
            foreach (var hit in Physics.RaycastAll(top, Vector3.down, OpenSky - BoxClearance, Physics.DefaultRaycastLayers,
                         QueryTriggerInteraction.Ignore))
            {
                if (IsObstacle(hit.collider))
                    return true;
            }
        }
        return false;
    }

    // How high above a spot there has to be nothing, over its middle and the corners of its clear box
    const float OpenSky = 15f;

    static readonly Vector3[] SkyPoints =
    {
        Vector3.zero,
        new(-ClearHalfSize.x, 0, -ClearHalfSize.z), new(ClearHalfSize.x, 0, -ClearHalfSize.z),
        new(-ClearHalfSize.x, 0, ClearHalfSize.z), new(ClearHalfSize.x, 0, ClearHalfSize.z),
    };

    static bool IsObstacle(Collider collider) =>
        collider != null && collider.TryCast<TerrainCollider>() == null && collider.gameObject.layer != _waterLayer;
}

using System;
using System.Collections.Generic;
using System.Linq;
using OverTheHillUnlimited.Patches;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Self-test sections that park cars where the whole party has to gather (portals, cabin camping areas, the camp tool)
/// and around the zones each player reaches on their own (scenic points, crates, cabins, merchants), then check the
/// game's own logic counts them. Cars park the way a group would: as close to the middle as they fit, a car's width and
/// length apart, on open, level ground clear of buildings, rocks, trees and water.
///
/// Only the bots park. The host's car (the real profile) stays where it spawned, so it never reaches a point of interest,
/// and sits out of the party checks (SitOut): the party is the bots.
/// With -otu-selftest-screenshots, each is rendered from above.
/// </summary>
internal sealed partial class SelfTest
{
    const string AllZonesFlag = "-otu-selftest-all-zones";

    // Spacing between parked cars: a car's width or length, plus room to open a door or pull out
    const float ParkingPitchAcross = 4f;
    const float ParkingPitchAlong = 6.5f;

    // Half the box a parked car needs clear of anything that isn't the ground
    static readonly Vector3 CarClearance = new(1.2f, 0.8f, 2.4f);

    IEnumerable<object> TestGatheringAreas(NetworkRunner runner, Vector3 origin)
    {
        var points = SceneReferences.Instance.pointsOfInterest;
        var all = Enumerable.Range(0, points.Count).Select(i => points[i]).Where(p => p != null).ToList();
        bool everyZone = HasArgument(AllZonesFlag);
        if (HasArgument(ScreenshotsFlag) && TimeManager.Instance != null)
            TimeManager.Instance.Cheat_SetTime(12, 0);

        foreach (var portal in all.Select(p => p.TryCast<Portal>()).Where(p => p != null))
        {
            var sphere = portal.GetComponent<SphereCollider>();
            foreach (var step in TestGathering(runner, $"Gathering at portal '{portal.name}'", sphere, () => portal.AllPlayersInArea()))
                yield return step;
        }

        foreach (var cabin in all.Select(p => p.TryCast<Cabin>()).Where(c => c != null).Take(everyZone ? int.MaxValue : 3))
        {
            var camping = cabin.GetComponentInChildren<CabinCampTrigger>(true);
            if (camping != null && camping.campingArea != null)
            {
                foreach (var step in TestGathering(runner, $"Camping at cabin '{cabin.name}'", camping.campingArea,
                             () => camping.CheckPartyIsInCampingArea()))
                    yield return step;
            }
        }

        foreach (var step in TestCampAnywhere(runner, origin))
            yield return step;

        var reached = new List<(string Kind, PointOfInterest Point)>();
        reached.AddRange(all.Where(p => p.TryCast<ScenicPoint>() != null).Take(everyZone ? int.MaxValue : 2).Select(p => ("scenic point", p)));
        reached.AddRange(all.Where(p => p.TryCast<Crate>() != null).Take(everyZone ? int.MaxValue : 2).Select(p => ("crate", p)));
        reached.AddRange(all.Where(p => p.TryCast<Cabin>() != null).Take(everyZone ? int.MaxValue : 2).Select(p => ("cabin", p)));
        reached.AddRange(all.Where(p => p.TryCast<Merchant>() != null).Select(p => ("merchant", p)));
        foreach (var (kind, point) in reached)
        {
            var trigger = point.GetComponents<Collider>().FirstOrDefault(c => c.isTrigger);
            if (trigger != null)
            {
                foreach (var step in TestReachZone(runner, $"Reaching {kind} '{point.name}'", point, trigger))
                    yield return step;
            }
        }
    }

    // Parks the party as close to the middle of the area as it fits, checks the game's own check counts it with the
    // group rule, and that it stops counting once a car leaves the group
    IEnumerable<object> TestGathering(NetworkRunner runner, string name, Collider area, Func<bool> partyIsThere)
    {
        Section(name);
        var bots = Bots(runner);
        var center = area.bounds.center;
        float across = Math.Max(area.bounds.extents.x, area.bounds.extents.z);
        var spots = ParkingSpots(runner, center, across * 3 + 40, bots.Count);
        if (!Check($"{bots.Count} places to park", () => spots.Count >= bots.Count, () => $"{spots.Count} found"))
        {
            EndSection();
            yield break;
        }
        SitOut(runner, true);
        Do($"the party of {bots.Count} parks as close as it fits", () => Park(bots, spots));
        yield return new Wait(4);

        int inArea = CarsInside(runner, bots, area);
        bool gameRule = WithGameRules(partyIsThere);
        Record($"INFO {_section}: {Describe(area)}; {inArea} of {bots.Count} parked cars are in it, the furthest " +
               $"{FurthestFrom(runner, bots, center):F0} m from its middle; the game's own check says " +
               $"{(gameRule ? "everyone's there" : "they're not all there")}");
        Check("the party counts as there, gathered as one group", partyIsThere,
            () => $"{inArea} of {bots.Count} in the area; widest gap in the group {WidestGap(runner, bots):F1} m");
        foreach (var step in TopDownScreenshot(Slug(name), center, Math.Max(across, FurthestFrom(runner, bots, center))))
            yield return step;

        // One car wandering off breaks the group
        var straggler = bots[^1];
        var away = ParkingSpots(runner, center + Vector3.back * (FurthestFrom(runner, bots, center) + 40), 40, 1);
        if (away.Count > 0)
        {
            Do($"{Players.GetPlayerName(straggler)} drives off", () => Park(new List<PlayerRef> { straggler }, away));
            yield return new Wait(3);
            Check("the party no longer counts as there", () => !partyIsThere(),
                () => $"{Players.GetPlayerName(straggler)} is {Vector3.Distance(CarPosition(runner, straggler), center):F0} m from the middle");
        }
        SitOut(runner, false);
        EndSection();
    }

    // The camp tool needs every car within maxPartyMembersDistance of whoever places the campfire: a bot here, near
    // where everyone spawned
    IEnumerable<object> TestCampAnywhere(NetworkRunner runner, Vector3 origin)
    {
        Section("Camping away from a cabin");
        var manager = CampManager.Instance;
        var config = manager != null ? manager.campingConfig : null;
        if (config == null)
        {
            Skip("no camp manager");
            yield break;
        }
        var bots = Bots(runner);
        float distance = config.maxPartyMembersDistance;
        var spots = ParkingSpots(runner, origin, distance * 3 + 40, bots.Count);
        if (!Check($"{bots.Count} places to park", () => spots.Count >= bots.Count, () => $"{spots.Count} found"))
        {
            EndSection();
            yield break;
        }
        SitOut(runner, true);
        Do($"the party of {bots.Count} parks around the first", () => Park(bots, spots));
        yield return new Wait(4);
        BotPlayers.TryGetCar(bots[0], out var camper);
        var camperPosition = camper.transform.position;
        int within = bots.Count(p => Vector3.Distance(CarPosition(runner, p), camperPosition) <= distance);
        bool gameRule = WithGameRules(() => manager.CheckPartyIsCloseBy(camper));
        Record($"INFO {_section}: {within} of {bots.Count} cars are within the game's {distance:F0} m of the camper; " +
               $"the game's own check says {(gameRule ? "everyone's there" : "they're not all there")}");
        Check("the camp tool counts the party, gathered as one group", () => manager.CheckPartyIsCloseBy(camper),
            () => $"{within} within {distance:F0} m; widest gap in the group {WidestGap(runner, bots):F1} m");
        foreach (var step in TopDownScreenshot("camp-anywhere", camperPosition, FurthestFrom(runner, bots, camperPosition)))
            yield return step;
        SitOut(runner, false);
        EndSection();
    }

    // Each player reaches these on their own, and the trigger counts a car once it overlaps it
    IEnumerable<object> TestReachZone(NetworkRunner runner, string name, PointOfInterest point, Collider zone)
    {
        Section(name);
        var bots = Bots(runner);
        var center = zone.bounds.center;
        float across = Math.Max(zone.bounds.extents.x, zone.bounds.extents.z);
        var spots = ParkingSpots(runner, center, across * 3 + 40, bots.Count);
        if (!Check($"{bots.Count} places to park", () => spots.Count >= bots.Count, () => $"{spots.Count} found"))
        {
            EndSection();
            yield break;
        }
        Do($"{bots.Count} cars park as close as they fit", () => Park(bots, spots));
        yield return new Wait(4);

        float factor = ReachZonePatches.CurrentFactor;
        var marker = point.pointOfInterestWorld != null ? point.pointOfInterestWorld.DecalProjector : null;
        float modMarker = marker != null ? marker.m_Size.x / ReachZonePatches.Across(zone) : 0;
        int inMod = CarsInside(runner, bots, zone);
        ReachZonePatches.Apply(SceneReferences.Instance, 1);
        int inGame = CarsInside(runner, bots, zone);
        float gameMarker = marker != null ? marker.m_Size.x / ReachZonePatches.Across(zone) : 0;
        foreach (var step in TopDownScreenshot($"{Slug(name)}-game", center, across))
            yield return step;
        ReachZonePatches.Apply(SceneReferences.Instance, factor);
        foreach (var step in TopDownScreenshot($"{Slug(name)}-mod", center, across))
            yield return step;

        Record($"INFO {_section}: {Describe(zone)}; of {bots.Count} cars parked as close as they fit, {inGame} are in it at " +
               $"the game's size and {inMod} at the mod's (×{factor:F2})");
        if (factor > 1)
            Check("more of them are in it than at the game's size, or all already were", () => inMod > inGame || inGame == bots.Count,
                () => $"{inMod} now, {inGame} at the game's size");
        if (marker != null)
            Check("its marker on the ground grew with it", () => Math.Abs(modMarker - gameMarker) < 0.01f * Math.Max(1, gameMarker),
                () => $"marker to zone {modMarker:F2} now, {gameMarker:F2} at the game's size");
        EndSection();
    }

    static List<PlayerRef> Bots(NetworkRunner runner) => AllPlayers(runner).Where(p => p != runner.LocalPlayer).ToList();

    // Leaves the host out of the game's party checks and the mod's group alike
    static void SitOut(NetworkRunner runner, bool sitOut)
    {
        BotPlayers.HostSitsOut = sitOut;
        PartyGatheringPatches.SittingOut = sitOut ? runner.LocalPlayer : null;
    }

    // How finely the ground is searched for places to park
    const float ParkingSearchStep = 1.5f;

    // Places for cars around a point, nearest first: a group parks as close to the middle as it can, each car a width
    // and a length clear of the next. Cars face the same way: Fusion's compressed rotations aren't quite unit length
    // for most headings.
    static List<Vector3> ParkingSpots(NetworkRunner runner, Vector3 center, float reach, int count)
    {
        var host = OverlandNetworkRunner.Instance.LocalPlayerNetworkObject;
        var hostPosition = host != null ? host.transform.position : Vector3.positiveInfinity;
        var candidates = new List<(float Distance, Vector3 Point)>();
        int steps = (int)Math.Ceiling(reach / ParkingSearchStep);
        for (int x = -steps; x <= steps; x++)
        {
            for (int z = -steps; z <= steps; z++)
            {
                var offset = new Vector3(x * ParkingSearchStep, 0, z * ParkingSearchStep);
                if (offset.magnitude <= reach)
                    candidates.Add((offset.magnitude, center + offset));
            }
        }

        // Rays starting inside a rock or a building only see it from the inside
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            var spots = new List<Vector3>();
            foreach (var (_, point) in candidates.OrderBy(c => c.Distance))
            {
                if (spots.Count >= count)
                    break;
                // Clear of the cars already placed, and of the host's car, which stays put
                if (spots.Any(spot => TooClose(spot, point)) || TooClose(hostPosition, point))
                    continue;
                if (TryGround(point, out var ground))
                    spots.Add(ground + Vector3.up * 0.6f);
            }
            return spots;
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
        }
    }

    static bool TooClose(Vector3 car, Vector3 point) =>
        Math.Abs(car.x - point.x) < ParkingPitchAcross && Math.Abs(car.z - point.z) < ParkingPitchAlong;

    // Level, open ground for a car under this point: not on a roof, under cover, inside a rock, in water or against
    // anything
    static bool TryGround(Vector3 point, out Vector3 ground)
    {
        ground = default;
        if (!TryFindGround(point + Vector3.up * 8, 30, out var hit, out bool water) || water)
            return false;
        if (hit.normal.y < Mathf.Cos(20 * Mathf.Deg2Rad))
            return false;
        ground = hit.point;
        if (WaterBodyRegistry.TryGetWaterSurface(ground, out float surface) && surface - ground.y > 0.3f)
            return false;
        if (Blocked(ground + Vector3.up * 0.3f, Vector3.up, 15))
            return false;
        // Each end of the car on the ground too, within a step of the middle
        foreach (float end in new[] { -CarClearance.z, CarClearance.z })
        {
            if (!TryFindGround(ground + new Vector3(0, 3, end), 6, out var endHit, out bool endWater) ||
                endWater || Math.Abs(endHit.point.y - ground.y) > 1.2f)
                return false;
        }
        var blockers = Physics.OverlapBox(ground + Vector3.up * (CarClearance.y + 0.5f), CarClearance, Quaternion.identity, ~0,
            QueryTriggerInteraction.Ignore);
        return blockers.All(c => c.TryCast<TerrainCollider>() != null || IsMoving(c));
    }

    // The first solid thing below that isn't water or anything that moves, and whether water lies above it. Lakes are
    // solid colliders on the Water layer, or triggers.
    static bool TryFindGround(Vector3 start, float distance, out RaycastHit ground, out bool water)
    {
        ground = default;
        water = false;
        var hits = Physics.RaycastAll(start, Vector3.down, distance, ~0, QueryTriggerInteraction.Collide)
            .OrderBy(h => h.distance).ToList();
        foreach (var hit in hits)
        {
            if (IsMoving(hit.collider))
                continue;
            if (IsWater(hit.collider))
            {
                water = true;
                continue;
            }
            if (hit.collider.isTrigger)
                continue;
            ground = hit;
            return true;
        }
        return false;
    }

    static bool Blocked(Vector3 start, Vector3 direction, float distance) =>
        Physics.RaycastAll(start, direction, distance, ~0, QueryTriggerInteraction.Ignore).Any(h => !IsMoving(h.collider) && !IsWater(h.collider));

    static bool IsMoving(Collider collider) =>
        collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic;

    static bool IsWater(Collider collider) =>
        string.Equals(LayerMask.LayerToName(collider.gameObject.layer), "Water", StringComparison.OrdinalIgnoreCase);

    // Through the game's own teleport, the way the map does it
    static void Park(List<PlayerRef> players, List<Vector3> spots)
    {
        for (int i = 0; i < players.Count && i < spots.Count; i++)
            SpawnManager.Instance.RPC_TeleportPlayerToPosition(players[i], spots[i], Quaternion.identity);
    }

    // Cars in the zone the way its trigger sees them: any of a car's colliders overlapping it
    static int CarsInside(NetworkRunner runner, List<PlayerRef> players, Collider zone) =>
        players.Count(player => CarOf(runner, player) is { } car && car.GetComponentsInChildren<Collider>()
            .Any(c => c.enabled && !c.isTrigger && Physics.ComputePenetration(zone, zone.transform.position, zone.transform.rotation,
                c, c.transform.position, c.transform.rotation, out _, out _)));

    static float FurthestFrom(NetworkRunner runner, List<PlayerRef> players, Vector3 point) =>
        players.Max(p => Vector3.Distance(CarPosition(runner, p), point));

    // The longest hop needed to link every car into one group
    static float WidestGap(NetworkRunner runner, List<PlayerRef> players)
    {
        var positions = players.Select(p => CarPosition(runner, p)).ToList();
        var linked = new List<Vector3> { positions[0] };
        var rest = positions.Skip(1).ToList();
        float widest = 0;
        while (rest.Count > 0)
        {
            var (next, gap) = rest.Select(r => (r, linked.Min(l => Vector3.Distance(l, r)))).OrderBy(x => x.Item2).First();
            widest = Math.Max(widest, gap);
            linked.Add(next);
            rest.Remove(next);
        }
        return widest;
    }

    static bool WithGameRules(Func<bool> check)
    {
        PartyGatheringPatches.GameRulesOnly = true;
        try
        {
            return check();
        }
        finally
        {
            PartyGatheringPatches.GameRulesOnly = false;
        }
    }

    static string Describe(Collider zone)
    {
        var scale = zone.transform.lossyScale;
        var box = zone.TryCast<BoxCollider>();
        if (box != null)
        {
            string now = $"{box.size.x * scale.x:F0} by {box.size.z * scale.z:F0} m";
            return ReachZonePatches.TryGetGameSize(zone, out var game, out _)
                ? $"{game.x * scale.x:F0} by {game.z * scale.z:F0} m at the game's size, {now} now"
                : now;
        }
        float worldScale = Math.Max(scale.x, scale.z);
        float radius = (zone.TryCast<SphereCollider>()?.radius ?? zone.TryCast<CapsuleCollider>()?.radius ?? 0) * worldScale;
        return ReachZonePatches.TryGetGameSize(zone, out _, out float gameRadius)
            ? $"{gameRadius * worldScale:F0} m radius at the game's size, {radius:F0} m now"
            : $"{radius:F0} m radius";
    }

    static NetworkObject CarOf(NetworkRunner runner, PlayerRef player) =>
        player == runner.LocalPlayer ? OverlandNetworkRunner.Instance.LocalPlayerNetworkObject
            : BotPlayers.TryGetCar(player, out var car) ? car : null;

    // Looks down on the area from above and to the south, through a camera of the test's own drawn over the game's,
    // which follows the host's car
    IEnumerable<object> TopDownScreenshot(string name, Vector3 center, float across)
    {
        var main = Camera.main;
        if (!HasArgument(ScreenshotsFlag) || main == null)
            yield break;
        var holder = new GameObject("OTU self-test camera");
        var camera = holder.AddComponent<Camera>();
        camera.CopyFrom(main);
        camera.depth = main.depth + 10;
        camera.farClipPlane = Math.Max(main.farClipPlane, 3000);
        float height = across * 1.4f + 12;
        holder.transform.position = center + Vector3.up * height + Vector3.back * height * 0.55f;
        holder.transform.LookAt(center);
        yield return new Wait(0.5f);
        yield return Screenshot(name);
        Object.Destroy(holder);
    }

    static string Slug(string text) =>
        new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Replace("--", "-").Trim('-');
}

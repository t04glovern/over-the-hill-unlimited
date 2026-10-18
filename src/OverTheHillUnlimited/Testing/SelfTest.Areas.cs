using System;
using System.Collections.Generic;
using System.Linq;
using OverTheHillUnlimited.Patches;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Self-test sections for room for the whole party (PartyGatheringPatches). Where everyone has to gather (each portal,
/// cabin camping areas, the camp tool), every car parks, the host's included, and the game's own check is asked, with
/// and without the mod's group rule:
/// - where the game puts the party: arriving through a portal, or respawning at a cabin, on spawn points padded by
///   CabinSpawnPointPatches, where every car has to land on dry ground and on its wheels,
/// - as close as they fit,
/// - gathered at the edge of the area, where the game's own check turns them down and only the group rule counts them,
/// - at a portal, with the host waiting at the back of the group, where the host can't start travel but its decision
///   on a request from whoever is at the portal still counts the party,
/// - with one car driven off, out of reach of the rest and out of the area, so the party doesn't count.
/// Each answer is checked against the rule worked out independently from where the cars ended up: every car in the area
/// by the game's own test, or linked to one that is by a chain of cars, each within LinkDistance of the next across the
/// ground.
///
/// Cars park the way a group would: as close as they fit, a car's width and length apart, on open, level ground clear of
/// roofs, rocks, trees and water. The run doesn't save (SaveIsolationPatches), so cabins this profile hasn't unlocked
/// are unlocked for the run, and whatever the host's car reaches isn't kept. -otu-selftest-all-zones tests every cabin,
/// not just the first 3. With -otu-selftest-screenshots each layout is rendered from above.
/// </summary>
internal sealed partial class SelfTest
{
    const string AllZonesFlag = "-otu-selftest-all-zones";

    // Spacing between parked cars: a car's width or length, plus room to open a door or pull out
    const float ParkingPitchAcross = 4f;
    const float ParkingPitchAlong = 6.5f;

    // Half the box a parked car needs clear of anything that isn't the ground
    static readonly Vector3 CarClearance = new(1.2f, 0.8f, 2.4f);

    int _groupDecided;

    IEnumerable<object> TestGatheringAreas(NetworkRunner runner, Vector3 origin)
    {
        Section("Room for everyone");
        bool gather = Check("the group rule is on", () => Plugin.ConfigGatherAsGroup.Value);
        var points = SceneReferences.Instance.pointsOfInterest;
        var all = Enumerable.Range(0, points.Count).Select(i => points[i]).Where(p => p != null).ToList();
        bool everyZone = HasArgument(AllZonesFlag);
        if (HasArgument(ScreenshotsFlag) && TimeManager.Instance != null)
            Do("midday, for the screenshots", () => TimeManager.Instance.Cheat_SetTime(12, 0));
        EndSection();

        if (gather)
        {
            _groupDecided = 0;
            foreach (var portal in all.Select(p => p.TryCast<Portal>()).Where(p => p != null))
            {
                var trigger = portal.GetComponents<Collider>().FirstOrDefault(c => c.isTrigger);
                if (trigger == null)
                {
                    Record($"INFO Room for everyone: portal '{portal.name}' has no trigger of its own, so it's skipped");
                    continue;
                }
                foreach (var step in TestGathering(runner, $"Gathering at portal '{portal.name}'", trigger,
                             () => portal.AllPlayersInArea(), car => portal.playersInPortal.Contains(PlayerOf(runner, car)), portal))
                    yield return step;
            }

            var cabins = all.Select(p => p.TryCast<Cabin>()).Where(c => c != null)
                .Select(c => (Cabin: c, Camping: c.GetComponentInChildren<CabinCampTrigger>(true)))
                .Where(c => c.Camping != null && c.Camping.campingArea != null).Take(everyZone ? int.MaxValue : 3).ToList();
            foreach (var (cabin, camping) in cabins)
            {
                // The game only counts cars at cabins this profile has unlocked; this run doesn't save
                if (!camping.cabinIsUnlocked)
                    Do($"'{cabin.name}' is unlocked for this run", () => cabin.Cheat_UnlockCabin());
                foreach (var step in TestGathering(runner, $"Camping at cabin '{cabin.name}'", camping.campingArea,
                             () => camping.CheckPartyIsInCampingArea(), car => camping.ContainsCar(car), null, cabin))
                    yield return step;
            }

            foreach (var step in TestCampAnywhere(runner, origin))
                yield return step;

            Section("Room for everyone");
            Check("the group rule counted a party the game's own check turned down", () => _groupDecided > 0,
                () => "every layout was one the game already counted");
            Record($"INFO {_section}: the group rule decided {_groupDecided} layouts the game turned down");
            EndSection();
        }
    }

    // A place the whole party has to gather: inArea is the game's own test for one car
    IEnumerable<object> TestGathering(NetworkRunner runner, string name, Collider area, Func<bool> partyIsThere,
        Func<NetworkObject, bool> inArea, Portal portal, Cabin cabin = null)
    {
        Section(name);
        int failures = PartyGatheringPatches.Failures;
        var players = AllPlayers(runner);
        var center = area.bounds.center;
        float across = Math.Max(area.bounds.extents.x, area.bounds.extents.z);

        // At a portal, first where the game puts everyone arriving through it: the portal's own spawn points, padded
        // for the rest (CabinSpawnPointPatches). That's the party gathered there, wherever it is (a dock, a tunnel).
        if (portal != null)
        {
            Do("everyone leaves", () => OverlandNetworkRunner.Instance.DespawnAllPlayers());
            yield return new Wait(2);
            Do("everyone arrives through it", () => portal.AssignPlayersToSpawnPoints());
            yield return new Wait(LandingTime);
            if (!Check("every player has a car", () => players.All(p => CarOf(runner, p) != null),
                    () => string.Join(", ", players.Where(p => CarOf(runner, p) == null).Select(p => Players.GetPlayerName(p)))))
            {
                EndSection();
                yield break;
            }
            foreach (var step in CheckLanding(runner, players, portal))
                yield return step;
            foreach (var step in CheckGathering(runner, "where everyone arrives through it", area, partyIsThere, inArea, center, across, true))
                yield return step;
        }

        // At a cabin, first where the game puts everyone respawning there: its own spawn points, padded for the rest
        if (cabin != null)
        {
            // One after another, as players do: the game finds the next free spawn point by whether a car is on it,
            // which only shows a car moved there after the next physics step, and the party's cars can still be
            // parked on some of them
            int index = SceneReferences.Instance.pointsOfInterest.IndexOf(cabin);
            StopBots(runner, players);
            foreach (var player in players)
            {
                Do($"{Players.GetPlayerName(player)} respawns there", () => SpawnManager.Instance.RPC_RespawnPlayer(player, index));
                yield return new Wait(RespawnGap);
            }
            yield return new Wait(LandingTime);
            if (!Check("every car is there", () => players.All(p => Vector3.Distance(CarPosition(runner, p), cabin.transform.position) < NearPlace),
                    () => string.Join(", ", players.Select(p => $"{Players.GetPlayerName(p)} {Vector3.Distance(CarPosition(runner, p), cabin.transform.position):F0} m"))))
            {
                EndSection();
                yield break;
            }
            foreach (var step in CheckLanding(runner, players, cabin))
                yield return step;
            Check("it has a spawn point for every player, a car's length apart", () => SpawnSpacing(cabin, players.Count) >= MinSpawnSpacing,
                () => $"{cabin.spawnPoints.Count} spawn points, closest {SpawnSpacing(cabin, players.Count):F1} m apart");
            foreach (var step in CheckGathering(runner, "where everyone respawns there", area, partyIsThere, inArea, center, across, false))
                yield return step;
        }

        // As close as they fit, the host nearest the middle, where whoever presses Interact would be
        var spots = ParkingSpots(center, across * 3 + 80, players.Count);
        if (spots.Count >= players.Count)
        {
            foreach (var step in ParkAndCheck(runner, "as close as they fit", players, spots))
                yield return step;
            foreach (var step in CheckGathering(runner, "as close as they fit", area, partyIsThere, inArea, center, across, portal != null))
                yield return step;
        }
        else
        {
            Check("somewhere to park the party", () => portal != null, () => $"only {spots.Count} places to park");
            Record($"INFO {_section}: only {spots.Count} places to park around it by the test's rules, so that layout is skipped");
        }

        // Gathered at the edge: centred past the side of the area with the most room, the host still nearest the middle
        var edge = EdgeSpots(area, players.Count);
        if (edge != null)
        {
            foreach (var step in ParkAndCheck(runner, "gathered at its edge", players, edge))
                yield return step;
            foreach (var step in CheckGathering(runner, "gathered at its edge", area, partyIsThere, inArea, center, across, portal != null))
                yield return step;
        }
        else
        {
            Record($"INFO {_section}: no room to gather the party at the area's edge, so that layout is skipped");
        }

        // At a portal, the host waits at the back of the group: whoever is at the portal can start travel, the host can't
        if (portal != null && partyIsThere())
        {
            var host = runner.LocalPlayer;
            var current = players.ToDictionary(p => p.RawEncoded, p => CarPosition(runner, p));
            var back = players.OrderByDescending(p => Vector3.Distance(current[p.RawEncoded], center)).First();
            if (back != host)
            {
                Do($"the host swaps places with {Players.GetPlayerName(back)}, at the back", () => Park(
                    new List<PlayerRef> { host, back }, new List<Vector3> { current[back.RawEncoded], current[host.RawEncoded] }));
                yield return new Wait(3);
                Check("the host, away from the portal, can't start travel", () => !portal.AllPlayersInArea(),
                    () => "it would send everyone from the back of the group");
                // Cars can roll after the swap, so whether the party is still gathered is worked out again
                float gap = GapToArea(runner, players, p => CarOf(runner, p) is { } car && inArea(car));
                bool gathered = WithGameRules(partyIsThere) ||
                                gap <= PartyGatheringPatches.LinkDistance + PartyGatheringPatches.HostLinkSlack;
                Check($"the host's own decision counts the party exactly when it's still gathered ({gap:F1} m widest gap to the area)",
                    () => PartyGatheringPatches.AsHost(partyIsThere) == gathered);
            }
        }

        // One car drives off, out of reach of every other
        var straggler = players[^1];
        var others = players.Where(p => p != straggler).Select(p => CarPosition(runner, p)).ToList();
        // Out of the area too: a car in the area counts on its own
        var away = ParkingSpots(center, across * 3 + 120, 1, others, PartyGatheringPatches.LinkDistance + 10,
            spot => (area.ClosestPoint(spot) - spot).magnitude > 10);
        if (Check($"{Players.GetPlayerName(straggler)} has somewhere to drive off to", () => away.Count == 1))
        {
            Do($"{Players.GetPlayerName(straggler)} drives off", () => Park(new List<PlayerRef> { straggler }, away));
            yield return new Wait(3);
            float gap = others.Min(o => PartyGatheringPatches.AcrossTheGround(o, CarPosition(runner, straggler)));
            Check($"the party no longer counts, {gap:F0} m from the nearest car",
                () => !PartyGatheringPatches.AsHost(partyIsThere) && !partyIsThere());
        }
        Check("the group was worked out without errors", () => PartyGatheringPatches.Failures == failures,
            () => $"{PartyGatheringPatches.Failures - failures} failures");
        EndSection();
    }

    IEnumerable<object> CheckGathering(NetworkRunner runner, string layout, Collider area, Func<bool> partyIsThere,
        Func<NetworkObject, bool> inArea, Vector3 center, float across, bool isPortal)
    {
        var players = AllPlayers(runner);
        int inGameArea = 0;
        bool gameRule = false, groupRule = false, hostIn = false;
        float gap = 0;
        Do($"{layout}: the game's own checks", () =>
        {
            inGameArea = players.Count(p => CarOf(runner, p) is { } car && inArea(car));
            hostIn = inArea(CarOf(runner, runner.LocalPlayer));
            gap = GapToArea(runner, players, p => CarOf(runner, p) is { } car && inArea(car));
            gameRule = WithGameRules(partyIsThere);
            groupRule = partyIsThere();
        });
        float furthest = FurthestFrom(runner, players, center);
        Record($"INFO {_section}: {layout}, {inGameArea} of {players.Count} cars are in the area by the game's own test, " +
               $"the furthest {furthest:F0} m from its middle, the widest gap linking the rest to it {gap:F1} m; the game " +
               $"alone says {(gameRule ? "everyone's there" : "they're not all there")}");
        // The group rule's answer, worked out independently: every car in the area or linked to it (at a portal, the
        // player asking has to be in it)
        bool oneGroup = gap <= PartyGatheringPatches.LinkDistance && (!isPortal || hostIn);
        Check($"{layout}: the party counts as there exactly when it's gathered as one group", () => groupRule == (gameRule || oneGroup),
            () => $"says {(groupRule ? "yes" : "no")}; {inGameArea} in the area, widest gap to it {gap:F1} m, host in it: {hostIn}");
        if (groupRule && !gameRule)
            _groupDecided++;
        foreach (var step in TopDownScreenshot($"{Slug(_section)}-{Slug(layout)}", center, Math.Max(across, furthest)))
            yield return step;
    }

    // The camp tool needs every car within maxPartyMembersDistance of the camper, across the ground: the host here
    IEnumerable<object> TestCampAnywhere(NetworkRunner runner, Vector3 origin)
    {
        Section("Camping away from a cabin");
        int failures = PartyGatheringPatches.Failures;
        var manager = CampManager.Instance;
        var config = manager != null ? manager.campingConfig : null;
        var host = OverlandNetworkRunner.Instance.LocalPlayerNetworkObject;
        if (config == null || host == null)
        {
            Skip("no camp manager");
            yield break;
        }
        var players = AllPlayers(runner);
        float distance = config.maxPartyMembersDistance;

        // The host camps where the run started; the rest park as close as they fit, then gather off to one side
        List<Vector3> spots = null;
        if (!Check($"{players.Count} places to park", () => (spots = ParkingSpots(origin, distance * 3 + 40, players.Count)).Count >= players.Count,
                () => $"{spots?.Count} found"))
        {
            EndSection();
            yield break;
        }
        foreach (var step in ParkAndCheck(runner, "as close as they fit", players, spots))
            yield return step;
        foreach (var step in CheckCamp(runner, "as close as they fit", manager, host, distance))
            yield return step;

        var camper = host.transform.position;
        var side = ParkingSpots(camper + Vector3.right * (distance + 6), distance * 2 + 30, players.Count - 1,
            new List<Vector3> { camper });
        if (side.Count == players.Count - 1)
        {
            foreach (var step in ParkAndCheck(runner, "gathered off to one side", players.Skip(1).ToList(), side))
                yield return step;
            foreach (var step in CheckCamp(runner, "gathered off to one side", manager, host, distance))
                yield return step;
        }
        else
        {
            Record($"INFO {_section}: no room to gather off to one side, so that layout is skipped");
        }

        var straggler = players[^1];
        var others = players.Where(p => p != straggler).Select(p => CarPosition(runner, p)).ToList();
        // Out of the camper's reach too: a car within it counts on its own
        var away = ParkingSpots(camper, distance * 3 + 120, 1, others, PartyGatheringPatches.LinkDistance + 10,
            spot => PartyGatheringPatches.AcrossTheGround(spot, camper) > distance + 10);
        if (Check($"{Players.GetPlayerName(straggler)} has somewhere to drive off to", () => away.Count == 1))
        {
            Do($"{Players.GetPlayerName(straggler)} drives off", () => Park(new List<PlayerRef> { straggler }, away));
            yield return new Wait(3);
            Check("the camp tool no longer counts the party", () => !manager.CheckPartyIsCloseBy(host));
        }
        Check("the group was worked out without errors", () => PartyGatheringPatches.Failures == failures,
            () => $"{PartyGatheringPatches.Failures - failures} failures");
        EndSection();
    }

    IEnumerable<object> CheckCamp(NetworkRunner runner, string layout, CampManager manager, NetworkObject host, float distance)
    {
        var players = AllPlayers(runner);
        var camper = host.transform.position;
        int within = players.Count(p => PartyGatheringPatches.AcrossTheGround(CarPosition(runner, p), camper) <= distance);
        float gap = GapToArea(runner, players, p => PartyGatheringPatches.AcrossTheGround(CarPosition(runner, p), camper) <= distance);
        bool gameRule = WithGameRules(() => manager.CheckPartyIsCloseBy(host));
        bool groupRule = manager.CheckPartyIsCloseBy(host);
        Record($"INFO {_section}: {layout}, {within} of {players.Count} cars are within the game's {distance:F0} m of the camper, " +
               $"the widest gap linking the rest to them {gap:F1} m; the game alone says {(gameRule ? "everyone's there" : "they're not all there")}");
        // The camper's own car is always within reach, so the group rule says yes exactly when every car is linked to it
        bool oneGroup = gap <= PartyGatheringPatches.LinkDistance;
        Check($"{layout}: the camp tool counts the party exactly when it's gathered as one group", () => groupRule == (gameRule || oneGroup),
            () => $"says {(groupRule ? "yes" : "no")}; {within} within {distance:F0} m, widest gap to them {gap:F1} m");
        if (groupRule && !gameRule)
            _groupDecided++;
        foreach (var step in TopDownScreenshot($"camp-anywhere-{Slug(layout)}", camper, FurthestFrom(runner, players, camper)))
            yield return step;
    }

    static bool Inside(Collider zone, Vector3 point) => (zone.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;

    // Spots for a party gathered just past the side of the area with the most room, with the first one (the host's)
    // well inside the area: a car settling there stays in it
    static List<Vector3> EdgeSpots(Collider area, int count)
    {
        var center = area.bounds.center;
        var extents = area.bounds.extents;
        var probes = new[] { Vector3.down * 0.8f, Vector3.up * 0.8f, Vector3.left * 2, Vector3.right * 2, Vector3.forward * 2, Vector3.back * 2 };
        List<Vector3> best = null;
        foreach (var side in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            float half = Math.Abs(Vector3.Dot(side, extents));
            var spots = ParkingSpots(center + side * (half + 10), 60, count);
            var inside = spots.Where(s => probes.All(p => Inside(area, s + p))).ToList();
            if (spots.Count < count || inside.Count == 0)
                continue;
            var host = inside.OrderBy(s => Vector3.Distance(s, center)).First();
            var layout = new[] { host }.Concat(spots.Where(s => s != host)).ToList();
            if (best == null || layout.Max(s => Vector3.Distance(s, center)) < best.Max(s => Vector3.Distance(s, center)))
                best = layout;
        }
        return best;
    }

    // Parks the players on the spots in order, then checks they got there. The checks that follow use where the cars
    // ended up, so a car that rolled a little after landing (bots don't hold the brake) is only logged.
    IEnumerable<object> ParkAndCheck(NetworkRunner runner, string layout, List<PlayerRef> players, List<Vector3> spots)
    {
        Do($"{layout}: {players.Count} cars park", () => Park(players, spots));
        yield return new Wait(4);
        var cars = players.Select((p, i) => (Name: Players.GetPlayerName(p), Spot: spots[i], Car: CarOf(runner, p))).ToList();
        float Off((string Name, Vector3 Spot, NetworkObject Car) c) => PartyGatheringPatches.AcrossTheGround(c.Car.transform.position, c.Spot);
        Check($"{layout}: every car got there", () => cars.All(c => c.Car != null && Off(c) < 25),
            () => string.Join(", ", cars.Where(c => c.Car == null || Off(c) >= 25).Select(c => c.Name)));
        var settled = cars.Where(c => c.Car != null && (Off(c) > 3 || c.Car.transform.up.y < 0.7f))
            .Select(c => $"{c.Name} {Off(c):F1} m, tilted {Vector3.Angle(c.Car.transform.up, Vector3.up):F0}°").ToList();
        if (settled.Count > 0)
            Record($"INFO {_section}: {layout}, after landing these rolled or tipped: {string.Join(", ", settled)}");
    }

    // Through the game's own teleport, the way the map does it. Cars face the same way: Fusion's compressed rotations
    // aren't quite unit length for most headings.
    static void Park(List<PlayerRef> players, List<Vector3> spots)
    {
        for (int i = 0; i < players.Count && i < spots.Count; i++)
            SpawnManager.Instance.RPC_TeleportPlayerToPosition(players[i], spots[i], Quaternion.identity);
    }

    // How finely the ground is searched for places to park
    const float ParkingSearchStep = 1.5f;

    // Places for cars around a point, nearest first: a group parks as close to the middle as it can, each car a width
    // and a length clear of the next and of the cars listed to avoid. With minDistance, only places at least that far
    // (across the ground) from every car to avoid.
    static List<Vector3> ParkingSpots(Vector3 center, float reach, int count, List<Vector3> avoid = null, float minDistance = 0,
        Func<Vector3, bool> allowed = null)
    {
        avoid ??= new List<Vector3>();
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
                if (spots.Any(spot => TooClose(spot, point)) || avoid.Any(car => TooClose(car, point)) ||
                    (minDistance > 0 && avoid.Any(car => PartyGatheringPatches.AcrossTheGround(car, point) < minDistance)) ||
                    (allowed != null && !allowed(point)))
                    continue;
                if (TryGround(point, out var spot))
                    spots.Add(spot);
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

    // Where a car can be parked under this point, if it can: open, level ground, not a roof, not under cover, not in a
    // rock, a tree or water, and clear of anything else
    static bool TryGround(Vector3 point, out Vector3 spot)
    {
        spot = default;
        if (!TryFindGround(point + Vector3.up * 8, 30, out var hit, out bool water) || water)
            return false;
        if (hit.normal.y < Mathf.Cos(15 * Mathf.Deg2Rad))
            return false;
        var ground = hit.point;
        if (WaterBodyRegistry.TryGetWaterSurface(ground, out float surface) && surface - ground.y > 0.3f)
            return false;
        if (OnAStructure(ground))
            return false;
        if (Blocked(ground + Vector3.up * 0.3f, Vector3.up, 15))
            return false;
        // Each end and side of the car on the ground too, within a small step of the middle
        float highest = ground.y;
        foreach (var end in new[] { new Vector3(0, 0, -CarClearance.z), new Vector3(0, 0, CarClearance.z),
                     new Vector3(-CarClearance.x, 0, 0), new Vector3(CarClearance.x, 0, 0) })
        {
            if (!TryFindGround(ground + end + Vector3.up * 3, 6, out var endHit, out bool endWater) ||
                endWater || Math.Abs(endHit.point.y - ground.y) > 0.5f)
                return false;
            highest = Math.Max(highest, endHit.point.y);
        }
        var blockers = Physics.OverlapBox(ground + Vector3.up * (CarClearance.y + 0.5f), CarClearance, Quaternion.identity, ~0,
            QueryTriggerInteraction.Ignore);
        if (!blockers.All(c => c.TryCast<TerrainCollider>() != null || IsMoving(c)))
            return false;
        // Trees on the terrain are part of its collider, so look along each side of the car at bonnet height
        var at = ground + Vector3.up * 1.4f;
        foreach (var (from, to) in new[]
                 {
                     (new Vector3(-CarClearance.x, 0, -CarClearance.z), new Vector3(-CarClearance.x, 0, CarClearance.z)),
                     (new Vector3(CarClearance.x, 0, -CarClearance.z), new Vector3(CarClearance.x, 0, CarClearance.z)),
                     (new Vector3(-CarClearance.x, 0, -CarClearance.z), new Vector3(CarClearance.x, 0, -CarClearance.z)),
                     (new Vector3(-CarClearance.x, 0, CarClearance.z), new Vector3(CarClearance.x, 0, CarClearance.z)),
                 })
        {
            if (Blocked(at + from, to - from, (to - from).magnitude))
                return false;
        }
        spot = new Vector3(ground.x, highest + 0.6f, ground.z);
        return true;
    }

    // Ground well above the terrain, with no water under it, is a roof or the top of a rock: docks over water count
    static bool OnAStructure(Vector3 ground)
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            if (ground.x < origin.x || ground.z < origin.z || ground.x > origin.x + size.x || ground.z > origin.z + size.z)
                continue;
            float terrainHeight = origin.y + terrain.SampleHeight(ground);
            if (ground.y - terrainHeight <= 1.5f)
                return false;
            return !Physics.RaycastAll(ground + Vector3.down * 0.1f, Vector3.down, ground.y - terrainHeight + 1, ~0,
                QueryTriggerInteraction.Collide).Any(h => IsWater(h.collider));
        }
        return false;
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

    // A car sunk in a lake or river: its middle more than half a metre under the water's surface
    static bool Underwater(Vector3 car)
    {
        if (WaterBodyRegistry.TryGetWaterSurface(car, out float surface) && surface - car.y > 0.5f)
            return true;
        return Physics.RaycastAll(car, Vector3.up, 30, ~0, QueryTriggerInteraction.Collide)
            .Any(h => IsWater(h.collider) && h.point.y - car.y > 0.5f);
    }

    // Where the game put the party at a place it spawns players (the start, a portal's arrivals, a cabin's respawns),
    // once every car has dropped onto its spawn point (LandingTime after spawning): every car on dry ground and on its
    // wheels, each one that isn't listed with how far it is from the nearest spawn point. A real player's idle car then
    // holds still on its automatic handbrake (VehicleInput's AutoHandbrake), but a bot sends no input, so its car rolls
    // freely downhill; how far the bots' cars roll over the next few seconds is only reported.
    IEnumerable<object> CheckLanding(NetworkRunner runner, List<PlayerRef> players, SpawnablePointOfInterest place)
    {
        Check("every car landed on dry ground", () => players.All(p => !Underwater(CarPosition(runner, p))),
            () => string.Join(", ", players.Where(p => Underwater(CarPosition(runner, p))).Select(p =>
                $"{Players.GetPlayerName(p)} {FromSpawnPoints(place, CarPosition(runner, p)):F1} m from the nearest spawn point")));
        Check("every car landed on its wheels", () => players.All(p => Tilt(runner, p) < MaxLandedTilt),
            () => string.Join(", ", players.Where(p => Tilt(runner, p) >= MaxLandedTilt).Select(p =>
                $"{Players.GetPlayerName(p)} tilted {Tilt(runner, p):F0}°, {FromSpawnPoints(place, CarPosition(runner, p)):F1} m from the nearest spawn point, " +
                SpotDetails(place, NearestSpawnPoint(place, CarPosition(runner, p))))));
        Check("no two cars landed on top of each other", () => ClosestCars(runner) >= MinLandedApart,
            () => $"the closest two are {ClosestCars(runner):F1} m apart");
        var landed = players.ToDictionary(p => p.RawEncoded, p => CarPosition(runner, p));
        yield return new Wait(RollingTime);
        var rolled = players.Where(p => Vector3.Distance(CarPosition(runner, p), landed[p.RawEncoded]) > 3 || Tilt(runner, p) > 30)
            .Select(p => $"{Players.GetPlayerName(p)} {Vector3.Distance(CarPosition(runner, p), landed[p.RawEncoded]):F1} m, " +
                         $"tilted {Tilt(runner, p):F0}°").ToList();
        if (rolled.Count > 0)
            Record($"INFO {_section}: without a handbrake, these bots' cars rolled or tipped after landing: {string.Join(", ", rolled)}");
    }

    // Spawning at a place moves a player's car from where it is now, and a bot's car, with no handbrake, is often still
    // rolling, in the lobby or from the last layout. A player's idle car would be standing still, so the bots' cars are
    // stopped first; how fast they were going is reported.
    void StopBots(NetworkRunner runner, List<PlayerRef> players)
    {
        var moving = new List<string>();
        foreach (var player in players)
        {
            if (player == runner.LocalPlayer)
                continue;
            var car = CarOf(runner, player);
            var body = car != null ? car.GetComponent<Rigidbody>() : null;
            if (body == null || body.isKinematic)
                continue;
            if (body.linearVelocity.magnitude > 1 || body.angularVelocity.magnitude > 1)
                moving.Add($"{Players.GetPlayerName(player)} {body.linearVelocity.magnitude:F1} m/s, turning {body.angularVelocity.magnitude:F1} rad/s");
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        if (moving.Count > 0)
            Record($"INFO {_section}: stopped these bots' cars, still moving from before: {string.Join(", ", moving)}");
    }

    // The gap between players respawning, how long a car takes to drop onto its spawn point and settle, and how long the
    // bots' cars are then left to roll
    const float RespawnGap = 0.1f;
    const float LandingTime = 1.5f;
    const float RollingTime = 4.5f;

    // Past this, a car is on its side or its roof
    const float MaxLandedTilt = 60;

    // Spawn points are a car's width apart or more, so two cars closer than this were given the same one
    const float MinLandedApart = 2.5f;

    static float Tilt(NetworkRunner runner, PlayerRef player) =>
        CarOf(runner, player) is { } car ? Vector3.Angle(car.transform.up, Vector3.up) : 180;

    static float FromSpawnPoints(SpawnablePointOfInterest place, Vector3 car) =>
        Vector3.Distance(car, NearestSpawnPoint(place, car));

    static Vector3 NearestSpawnPoint(SpawnablePointOfInterest place, Vector3 car)
    {
        var nearest = Vector3.positiveInfinity;
        for (int i = 0; i < place.spawnPoints.Count; i++)
        {
            var point = place.spawnPoints[i].transform.position;
            if (Vector3.Distance(car, point) < Vector3.Distance(car, nearest))
                nearest = point;
        }
        return nearest;
    }

    // A spawn point, for a failure's details: how far above what's below it, how steep that is, how far the next spawn
    // point is, and what's solid around it
    static string SpotDetails(SpawnablePointOfInterest place, Vector3 point)
    {
        var below = Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !IsMoving(h.collider)).OrderBy(h => h.distance).ToList();
        string ground = below.Count > 0
            ? $"{point.y - below[0].point.y:F1} m above {below[0].collider.name} sloping {Vector3.Angle(below[0].normal, Vector3.up):F0}°"
            : "nothing below it";
        float next = float.PositiveInfinity;
        for (int i = 0; i < place.spawnPoints.Count; i++)
        {
            float distance = Vector3.Distance(point, place.spawnPoints[i].transform.position);
            if (distance > 0.01f)
                next = Math.Min(next, distance);
        }
        return $"its spawn point {ground}, {next:F1} m from the next, near {Around(point)}";
    }

    // What's solid within a few metres of a spot, other than the terrain, water and cars, for a failure's details
    static string Around(Vector3 point)
    {
        var names = Physics.OverlapSphere(point, 4, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c.TryCast<TerrainCollider>() == null && !IsMoving(c) && !IsWater(c))
            .Select(c => $"{c.name} ({c.ClosestPoint(point).y - point.y:+0.0;-0.0} m)").Distinct().Take(8).ToList();
        return names.Count > 0 ? string.Join(", ", names) : "nothing but the ground";
    }

    static bool IsMoving(Collider collider) =>
        collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic;

    static int _waterLayer = -2;

    static bool IsWater(Collider collider)
    {
        if (_waterLayer == -2)
            _waterLayer = LayerMask.NameToLayer("Water");
        return collider.gameObject.layer == _waterLayer;
    }

    static float FurthestFrom(NetworkRunner runner, List<PlayerRef> players, Vector3 point) =>
        players.Max(p => Vector3.Distance(CarPosition(runner, p), point));

    // The longest hop across the ground needed to link every car to the area, by a chain of cars: the cars in the area
    // are linked already, wherever they are in it
    static float GapToArea(NetworkRunner runner, List<PlayerRef> players, Func<PlayerRef, bool> inArea)
    {
        var linked = players.Where(p => inArea(p)).Select(p => CarPosition(runner, p)).ToList();
        var rest = players.Where(p => !inArea(p)).Select(p => CarPosition(runner, p)).ToList();
        if (linked.Count == 0)
            return float.PositiveInfinity;
        float widest = 0;
        while (rest.Count > 0)
        {
            var (next, gap) = rest.Select(r => (r, linked.Min(l => PartyGatheringPatches.AcrossTheGround(l, r))))
                .OrderBy(x => x.Item2).First();
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

    static NetworkObject CarOf(NetworkRunner runner, PlayerRef player) =>
        player == runner.LocalPlayer ? OverlandNetworkRunner.Instance.LocalPlayerNetworkObject
            : BotPlayers.TryGetCar(player, out var car) ? car : null;

    static PlayerRef PlayerOf(NetworkRunner runner, NetworkObject car) =>
        AllPlayers(runner).FirstOrDefault(p => CarOf(runner, p) is { } c && c.Pointer == car.Pointer);

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

    static string Slug(string text)
    {
        var slug = new System.Text.StringBuilder();
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }
        return slug.ToString().Trim('-');
    }
}

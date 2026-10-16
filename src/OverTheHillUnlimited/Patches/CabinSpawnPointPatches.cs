using System.Collections.Generic;
using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Room for more than 4 cars where players spawn in the map, such as cabins. Each spawnable point of interest has a few
/// spawn points (4 at the starting cabin), and once they're taken SpawnablePointOfInterest.GetNextFreeSpawnPoint puts
/// everyone else on the first one ("All spawn points ... are occupied"), on top of whoever is there. That's every
/// player from the 5th on when a session starts. The first time a point of interest is used, this adds spawn points
/// around its own, up to ExpandedState.Capacity, wherever there's open, fairly level ground. Points of interest sit
/// among trees and rocks, so each spot is checked rather than laid out in a fixed pattern; where there isn't room, the
/// game's fallback applies as before. Only the host picks spawn points.
/// </summary>
public class CabinSpawnPointPatches
{
    // Candidate spots: rings of 8 around each of the game's spawn points, nearest rings first
    const float SearchStep = 3.5f;
    const int SearchRings = 6;

    // A spot needs this much room from every other spawn point, ground no steeper than about 25°, and nothing but
    // terrain in a car-sized box above it
    const float CarSpacing = 4.5f;
    const float MinGroundUp = 0.9f;
    static readonly Vector3 CarHalfSize = new(1.3f, 0.9f, 2.6f);
    const float BoxClearance = 0.5f;

    static readonly HashSet<int> _padded = new();

    [HarmonyPatch(typeof(SpawnablePointOfInterest), nameof(SpawnablePointOfInterest.GetNextFreeSpawnPoint))]
    [HarmonyPrefix]
    static void PadSpawnPoints(SpawnablePointOfInterest __instance)
    {
        if (!UnlimitedMode.Enabled || !_padded.Add(__instance.GetInstanceID()))
            return;
        var spawnPoints = __instance.spawnPoints;
        if (spawnPoints == null || spawnPoints.Count == 0 || spawnPoints.Count >= ExpandedState.Capacity)
            return;

        int vanillaCount = spawnPoints.Count;
        var taken = new List<Vector3>();
        for (int i = 0; i < vanillaCount; i++)
            taken.Add(spawnPoints[i].transform.position);

        for (int ring = 1; ring <= SearchRings && spawnPoints.Count < ExpandedState.Capacity; ring++)
        {
            for (int i = 0; i < vanillaCount && spawnPoints.Count < ExpandedState.Capacity; i++)
            {
                var source = spawnPoints[i].transform;
                if (!TryFindGround(source.position, out var sourceGround, out _))
                    continue;
                float heightAboveGround = source.position.y - sourceGround.y;

                for (int step = 0; step < 8 && spawnPoints.Count < ExpandedState.Capacity; step++)
                {
                    var offset = Quaternion.AngleAxis(45 * step, Vector3.up) * source.forward * (SearchStep * ring);
                    if (!TryFindGround(source.position + offset, out var ground, out var normal) || normal.y < MinGroundUp)
                        continue;
                    var position = ground + Vector3.up * heightAboveGround;
                    if (IsNear(position, taken) || IsBlocked(ground, source.rotation))
                        continue;

                    var clone = Object.Instantiate(source.gameObject, position, source.rotation, source.parent);
                    clone.name = $"{source.gameObject.name} (slot {spawnPoints.Count + 1})";
                    spawnPoints.Add(clone.GetComponent<SpawnPoint>());
                    taken.Add(position);
                }
            }
        }
        Plugin.Logger.LogInfo($"Spawn points at {__instance.name} padded from {vanillaCount} to {spawnPoints.Count}");
    }

    // The terrain under a spot, ignoring anything standing on it
    static bool TryFindGround(Vector3 around, out Vector3 ground, out Vector3 normal)
    {
        var hits = Physics.RaycastAll(around + Vector3.up * 20, Vector3.down, 60, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.TryCast<TerrainCollider>() != null)
            {
                ground = hit.point;
                normal = hit.normal;
                return true;
            }
        }
        ground = normal = default;
        return false;
    }

    static bool IsNear(Vector3 position, List<Vector3> taken)
    {
        foreach (var other in taken)
        {
            if (Vector3.Distance(position, other) < CarSpacing)
                return true;
        }
        return false;
    }

    // Anything but terrain where the car would stand: trees, rocks, buildings, other cars
    static bool IsBlocked(Vector3 ground, Quaternion rotation)
    {
        var centre = ground + Vector3.up * (BoxClearance + CarHalfSize.y);
        var colliders = Physics.OverlapBox(centre, CarHalfSize, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var collider in colliders)
        {
            if (collider != null && collider.TryCast<TerrainCollider>() == null)
                return true;
        }
        return false;
    }
}

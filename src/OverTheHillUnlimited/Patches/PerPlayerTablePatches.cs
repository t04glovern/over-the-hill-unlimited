using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Game data with one entry per player, sized for 4. Each is padded up to ExpandedState.Capacity the first
/// time the game reads it, so players 5 and up reuse the 4-player data instead of indexing past the end.
/// </summary>
public class PerPlayerTablePatches
{
    // Distance between rows of extra lobby spawn points, in front of the vanilla row
    const float SpawnRowSpacing = 8f;

    // MeteoritesSpawnConfig.SpawnableMeteoritesByPlayersCount is indexed by ActivePlayers.Count() - 1
    [HarmonyPatch(typeof(MeteoriteSpawnController), nameof(MeteoriteSpawnController.UpdateSpawnableMeteoritesConfig))]
    [HarmonyPrefix]
    static void PadMeteoriteSetups(MeteoriteSpawnController __instance)
    {
        if (!UnlimitedMode.Enabled)
            return;
        var setups = __instance.meteoriteSpawnConfig?.SpawnableMeteoritesByPlayersCount;
        if (setups == null || setups.Count == 0 || setups.Count >= ExpandedState.Capacity)
            return;

        int vanillaCount = setups.Count;
        while (setups.Count < ExpandedState.Capacity)
            setups.Add(setups[vanillaCount - 1]);
        Plugin.Logger.LogInfo($"Meteorite setups padded from {vanillaCount} to {setups.Count} player counts");
    }

    // Lobby.spawnPoints is indexed by the player's slot in OrderedPlayerRefs
    [HarmonyPatch(typeof(Lobby), nameof(Lobby.GetSpawnPoint))]
    [HarmonyPrefix]
    static void PadLobbySpawnPoints(Lobby __instance)
    {
        if (!UnlimitedMode.Enabled)
            return;
        var spawnPoints = __instance.spawnPoints;
        if (spawnPoints == null || spawnPoints.Count == 0 || spawnPoints.Count >= ExpandedState.Capacity)
            return;

        // Clone the vanilla spawn points into extra rows in front of them. SpawnPoint.Awake has already destroyed each
        // one's placeholder car preview, so the clones don't get one.
        int vanillaCount = spawnPoints.Count;
        for (int slot = vanillaCount; slot < ExpandedState.Capacity; slot++)
        {
            var source = spawnPoints[slot % vanillaCount];
            var sourceTransform = source.transform;
            int row = slot / vanillaCount;
            var position = sourceTransform.position + sourceTransform.forward * (SpawnRowSpacing * row);

            var clone = Object.Instantiate(source.gameObject, position, sourceTransform.rotation, sourceTransform.parent);
            clone.name = $"{source.gameObject.name} (slot {slot + 1})";
            spawnPoints.Add(clone.GetComponent<SpawnPoint>());
        }
        Plugin.Logger.LogInfo($"Lobby spawn points padded from {vanillaCount} to {spawnPoints.Count}");
    }
}

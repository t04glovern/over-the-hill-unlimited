using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using OverTheHillUnlimited.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Game data with one entry per player, sized for 4. Each is padded up to ExpandedState.Capacity the first
/// time the game reads it, so players 5 and up reuse the 4-player data instead of indexing past the end.
/// </summary>
public class PerPlayerTablePatches
{
    // Distance between rows of lobby spawn points: a car length and a gap
    const float SpawnRowSpacing = 7f;

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

    // Meteorite.winchedPlayersPullDirections is created as a Vector2[4], but FixedUpdateNetwork writes one entry per car
    // winched to the meteorite (winchedPlayers, a list that grows), so a 5th winch throws on the host every tick and
    // the meteorite can never be extracted. Grow it before each winch is added.
    [HarmonyPatch(typeof(Meteorite), nameof(Meteorite.OnWinchAttached))]
    [HarmonyPrefix]
    static void PadMeteoritePullDirections(Meteorite __instance)
    {
        if (!UnlimitedMode.Enabled)
            return;
        var directions = __instance.winchedPlayersPullDirections;
        int needed = Math.Max(ExpandedState.Capacity, (__instance.winchedPlayers?.Count ?? 0) + 1);
        if (directions != null && directions.Length >= needed)
            return;

        var padded = new Il2CppStructArray<Vector2>(needed);
        for (int i = 0; directions != null && i < directions.Length; i++)
            padded[i] = directions[i];
        __instance.winchedPlayersPullDirections = padded;
    }

    // Lobby.spawnPoints is indexed by the player's slot in OrderedPlayerRefs
    [HarmonyPatch(typeof(Lobby), nameof(Lobby.GetSpawnPoint))]
    [HarmonyPrefix]
    static void PadLobbySpawnPointsOnUse(Lobby __instance)
    {
        PadLobbySpawnPoints(__instance);
    }

    /// <summary>
    /// Clones the vanilla lobby spawn points into extra rows behind them, away from the lobby camera, which
    /// LobbyCameraPatches raises to see over the front row. Safe to call more than once.
    /// </summary>
    public static void PadLobbySpawnPoints(Lobby lobby)
    {
        if (!UnlimitedMode.Enabled)
            return;
        var spawnPoints = lobby.spawnPoints;
        if (spawnPoints == null || spawnPoints.Count == 0 || spawnPoints.Count >= ExpandedState.Capacity)
            return;

        // SpawnPoint.Awake has already destroyed each one's placeholder car preview, so the clones don't get one
        int vanillaCount = spawnPoints.Count;
        for (int slot = vanillaCount; slot < ExpandedState.Capacity; slot++)
        {
            var source = spawnPoints[slot % vanillaCount];
            var sourceTransform = source.transform;
            int row = slot / vanillaCount;
            var position = sourceTransform.position - sourceTransform.forward * (SpawnRowSpacing * row);

            var clone = Object.Instantiate(source.gameObject, position, sourceTransform.rotation, sourceTransform.parent);
            clone.name = $"{source.gameObject.name} (slot {slot + 1})";
            spawnPoints.Add(clone.GetComponent<SpawnPoint>());
        }
        Plugin.Logger.LogInfo($"Lobby spawn points padded from {vanillaCount} to {spawnPoints.Count}");
    }
}

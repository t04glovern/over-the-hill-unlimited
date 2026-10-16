using System;
using HarmonyLib;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Room around the campfire for every tent. Tents go on one circle around the campfire, CampingConfig.tentsPerCar per
/// car, spread evenly by angle at a fixed distance from the campfire. That distance fits 7 tents: from 8 their inner
/// corners touch, and at 16 they're 1.8 m apart while 2.4 m wide, so they overlap and the camp tool's placement check
/// fails more often. StateCampTool.ShowTentDecalProjectors(partyCount) lays the preview out for the party when the camp
/// tool opens and whenever someone joins or leaves, so this widens the circle there for big parties and puts the
/// game's distance back for small ones. Placement (CampUtils.GetTentPlacementForIndex) reads the same config, so the
/// preview and the tents agree.
/// </summary>
public class CampTentPatches
{
    // Gap between neighbouring tents' inner corners
    const float TentGap = 0.3f;

    static float? _gameDistance;

    [HarmonyPatch(typeof(StateCampTool), nameof(StateCampTool.ShowTentDecalProjectors))]
    [HarmonyPrefix]
    static void MakeRoomForTents(int partyCount)
    {
        FitTents(partyCount);
    }

    /// <summary>Sets the tents' distance from the campfire for a party of this size.</summary>
    internal static void FitTents(int partyCount)
    {
        var manager = CampManager.Instance;
        var config = manager != null ? manager.campingConfig : null;
        if (config == null)
            return;

        _gameDistance ??= config.tentDistanceFromCampfire;
        int tents = partyCount * Math.Max(1, config.tentsPerCar);
        config.tentDistanceFromCampfire = UnlimitedMode.Enabled
            ? DistanceFor(config, tents, _gameDistance.Value)
            : _gameDistance.Value;
    }

    // A tent's centre is (tentLength + campfireSize) / 2 + distance from the campfire's centre, so its inner edge is
    // campfireSize / 2 + distance out. Neighbouring inner corners clear each other once that radius times
    // tan(180° / tents) is at least half a tent's width plus half the gap.
    static float DistanceFor(CampingConfig config, int tents, float gameDistance)
    {
        if (tents <= 1)
            return gameDistance;
        float innerRadius = (config.tentWidth + TentGap) / 2 / Mathf.Tan(Mathf.PI / tents);
        return Math.Max(gameDistance, innerRadius - config.campfireSize / 2);
    }
}

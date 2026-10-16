using System;
using System.Collections.Generic;
using Fusion;
using HarmonyLib;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Meteorites balanced for more than 4 players. The game's own balance stops at 4: past that, every extra player shrinks
/// everyone's share of the pebbles, there are still only 2 or 3 meteorites in the map, and only cars within 10 m of
/// one when it comes out count as involved. All three are decided by the host, so only the host needs this (and only
/// the host's config counts), and with the default settings none of it changes anything with 4 players or fewer.
/// - Pebbles: Meteorite.CompleteObjectiveForInvolvedPlayers sends every player in the session
///   RPC_CompleteObjectiveForLocalPlayer, with CeilToInt(PebbleValue / involved players) for each involved player
///   (every car winched to it, and every car within prizeAwardMaxDistance) and 0 for everyone else. With more involved
///   than PebbleShareMaxPlayers (4), each involved player gets the share for that many instead.
/// - Count: MeteoriteSpawnController keeps 2 or 3 meteorites in the map (meteoritesToSpawn, rolled once by the host).
///   One more is added for every PlayersPerExtraMeteorite (2) players past 4, so by default there are as many
///   meteorites per player as in a 4-player game.
/// - Reach: with ScaleRewardReach, prizeAwardMaxDistance (10 m) grows with the square root of the player count past 4,
///   so the area around the meteorite grows with the number of cars.
/// </summary>
public class MeteoriteBalancePatches
{
    /// <summary>The award the last extraction gave (for the self-test).</summary>
    internal static (int Involved, int Share, int VanillaShare, float Reach)? LastAward;

    /// <summary>The prize sent to each player for the last extraction, by PlayerRef.RawEncoded (for the self-test).</summary>
    internal static readonly Dictionary<int, int> LastPrizes = new();

    /// <summary>The self-test extracts meteorites itself, and keeps the host's own player (the real profile) out of it.</summary>
    internal static bool SkipLocalPlayerAward;

    // The awards CompleteObjectiveForInvolvedPlayers sends, held back until it's done and the number of players is known
    readonly record struct Award(PlayerRef Player, int Prize, bool InAchievementCollectionWindow);

    static readonly List<Award> _awards = new();
    static Meteorite _collecting;
    static float _vanillaReach;

    // The meteorite count the host rolled, before adding any for more players
    static IntPtr _controller;
    static int _baseCount;

    /// <summary>The number of meteorites the host rolled (for the self-test).</summary>
    internal static int BaseMeteoriteCount => _baseCount;

    static int SessionPlayers => OverlandSessionPlayers.Instance != null ? OverlandSessionPlayers.Instance.GetPlayerCount() : 0;

    [HarmonyPatch(typeof(Meteorite), nameof(Meteorite.CompleteObjectiveForInvolvedPlayers))]
    [HarmonyPrefix]
    static void BeforeAwards(Meteorite __instance)
    {
        // If the last call threw before the postfix, put that meteorite's reach back so it isn't widened twice
        if (_collecting != null)
            _collecting.prizeAwardMaxDistance = _vanillaReach;
        _awards.Clear();
        _collecting = null;
        if (!UnlimitedMode.Enabled)
            return;
        _collecting = __instance;

        // Who counts as involved is worked out with this reach, so it's widened for the call and put back after
        _vanillaReach = __instance.prizeAwardMaxDistance;
        __instance.prizeAwardMaxDistance = Reach(_vanillaReach, SessionPlayers);
    }

    /// <summary>The distance within which cars count as involved, for a number of players.</summary>
    internal static float Reach(float vanillaReach, int players) =>
        Plugin.ConfigScaleMeteoriteReach.Value && players > Plugin.VanillaMaxPlayers
            ? vanillaReach * MathF.Sqrt(players / (float)Plugin.VanillaMaxPlayers)
            : vanillaReach;

    /// <summary>Each involved player's pebbles, given the game's own share for that many players.</summary>
    internal static int Share(int pebbles, int involved, int vanillaShare)
    {
        int maxSharing = Plugin.ConfigPebbleShareMaxPlayers.Value;
        return involved > maxSharing && pebbles > 0 ? (int)MathF.Ceiling(pebbles / (float)maxSharing) : vanillaShare;
    }

    [HarmonyPatch(typeof(Meteorite), nameof(Meteorite.RPC_CompleteObjectiveForLocalPlayer))]
    [HarmonyPrefix]
    static bool HoldBackAward(Meteorite __instance, PlayerRef playerRef, int playerPrize, bool inAchievementCollectionWindow)
    {
        if (_collecting == null || __instance.Pointer != _collecting.Pointer)
            return true;
        _awards.Add(new Award(playerRef, playerPrize, inAchievementCollectionWindow));
        return false;
    }

    [HarmonyPatch(typeof(Meteorite), nameof(Meteorite.CompleteObjectiveForInvolvedPlayers))]
    [HarmonyPostfix]
    static void SendAwards(Meteorite __instance)
    {
        if (_collecting == null)
            return;
        _collecting = null;
        float reach = __instance.prizeAwardMaxDistance;
        __instance.prizeAwardMaxDistance = _vanillaReach;

        // Everyone in the session is told; the players who weren't involved get 0
        int involved = 0, vanillaShare = 0;
        foreach (var award in _awards)
        {
            if (award.Prize <= 0)
                continue;
            involved++;
            vanillaShare = award.Prize;
        }
        int pebbles = __instance.meteoriteConfig != null ? __instance.meteoriteConfig.PebbleValue : 0;
        int share = Share(pebbles, involved, vanillaShare);
        LastAward = (involved, share, vanillaShare, reach);
        if (share != vanillaShare)
            Plugin.Logger.LogInfo($"Meteorite extracted with {involved} players: {share} pebbles each " +
                                  $"(the {Plugin.ConfigPebbleShareMaxPlayers.Value}-player share) instead of {vanillaShare}");

        LastPrizes.Clear();
        int local = __instance.Runner != null ? __instance.Runner.LocalPlayer.RawEncoded : int.MinValue;
        foreach (var award in _awards)
        {
            int prize = award.Prize > 0 ? share : award.Prize;
            LastPrizes[award.Player.RawEncoded] = prize;
            if (SkipLocalPlayerAward && award.Player.RawEncoded == local)
                continue;
            __instance.RPC_CompleteObjectiveForLocalPlayer(award.Player, prize, award.InAchievementCollectionWindow);
        }
        _awards.Clear();
    }

    // The host rolls 2 or 3 meteorites here; add the ones for more players, and have them already landed like the rest
    [HarmonyPatch(typeof(MeteoriteSpawnController), nameof(MeteoriteSpawnController.Spawned))]
    [HarmonyPostfix]
    static void ScaleMeteoriteCountOnSpawn(MeteoriteSpawnController __instance)
    {
        if (!__instance.HasStateAuthority)
            return;
        _controller = __instance.Pointer;
        _baseCount = __instance.NumberOfMeteoritesToSpawn;
        if (UnlimitedMode.Enabled && ApplyMeteoriteCount(__instance))
            __instance.CheckAndRespawnMeteorites(true);
    }

    // Called when the number of players changes. Spawned calls it too, before rolling the count, so wait for that.
    [HarmonyPatch(typeof(MeteoriteSpawnController), nameof(MeteoriteSpawnController.UpdateSpawnableMeteoritesConfig))]
    [HarmonyPostfix]
    static void ScaleMeteoriteCountForPlayers(MeteoriteSpawnController __instance)
    {
        if (UnlimitedMode.Enabled && __instance.HasStateAuthority && __instance.Pointer == _controller)
            ApplyMeteoriteCount(__instance);
    }

    /// <summary>The number of meteorites to keep in the map for a number of players, given the host's roll.</summary>
    internal static int MeteoriteCount(int baseCount, int players)
    {
        int playersPerExtra = Plugin.ConfigPlayersPerExtraMeteorite.Value;
        if (playersPerExtra <= 0 || players <= Plugin.VanillaMaxPlayers)
            return baseCount;
        return baseCount + (players - Plugin.VanillaMaxPlayers + playersPerExtra - 1) / playersPerExtra;
    }

    // New ones fall from the sky at the controller's next check; with fewer players, it stops replacing the extra ones
    static bool ApplyMeteoriteCount(MeteoriteSpawnController controller)
    {
        int target = MeteoriteCount(_baseCount, controller.currentNumberOfPlayers);
        if (controller.NumberOfMeteoritesToSpawn == target)
            return false;
        controller.NumberOfMeteoritesToSpawn = target;
        Plugin.Logger.LogInfo($"{target} meteorites in the map for {controller.currentNumberOfPlayers} players");
        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OverTheHillUnlimited.Networking;
using OverTheHillUnlimited.Patches;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Self-test sections for things in the world that hold one entry per player: winches on a car, on a meteorite and on
/// a recovery vehicle, tents around a campfire, voice chat slots, and arriving through a portal. Each section skips
/// itself (with an INFO line) if the area doesn't have what it needs.
/// </summary>
internal sealed partial class SelfTest
{
    // A car's winch ropes are tracked by the car they're attached to, and DetachAllWinches (on teleport, respawn or
    // leaving) only lets go of the ones it tracked
    IEnumerable<object> TestCarWinches(NetworkRunner runner, List<PlayerRef> bots)
    {
        Section("Winches on one car");
        var hostWinch = WinchOf(runner, runner.LocalPlayer);
        var winchers = bots.Take(6).Select(bot => WinchOf(runner, bot)).Where(winch => winch != null).ToList();
        Check("the car's winch state is expanded", () => hostWinch != null && WinchCapacityPatches.State.IsExpanded(hostWinch));
        Do($"{winchers.Count} bots winch to the host's car", () =>
            winchers.ForEach(winch => winch.AttachToPlayerCarAnchorPoint(Vector3.zero, runner.LocalPlayer, Vector3.zero)));
        yield return new Wait(2);
        Check($"the host's car tracks all {winchers.Count}", () => OwnerCount(hostWinch) >= winchers.Count,
            () => $"{OwnerCount(hostWinch)} tracked");
        Do("the host's car lets go of every winch", () => hostWinch.DetachAllWinches());
        yield return new Wait(2);
        Check("every bot's winch is released", () => winchers.All(IsReleased),
            () => $"{winchers.Count(winch => !IsReleased(winch))} still attached");
        EndSection();
    }

    // Meteorite.FixedUpdateNetwork writes a pull direction per winched car, into an array the game makes for 4
    IEnumerable<object> TestMeteoriteWinches(NetworkRunner runner)
    {
        Section("Meteorite winched by six cars");
        var controller = MeteoriteSpawnController.Instance;
        if (controller != null)
        {
            int players = controller.currentNumberOfPlayers;
            int expected = MeteoriteBalancePatches.MeteoriteCount(MeteoriteBalancePatches.BaseMeteoriteCount, players);
            Check($"{expected} meteorites to keep in the map for {players} players", () => controller.NumberOfMeteoritesToSpawn == expected,
                () => $"{controller.NumberOfMeteoritesToSpawn}, from a roll of {MeteoriteBalancePatches.BaseMeteoriteCount}");
            for (int wait = 0; wait < 30 && controller.spawnedMeteorites.Count < expected; wait++)
                yield return new Wait(1);
            Check("they're all spawned", () => controller.spawnedMeteorites.Count >= expected,
                () => $"{controller.spawnedMeteorites.Count} spawned");
        }
        var meteorite = Object.FindObjectsOfType<Meteorite>()
            .FirstOrDefault(m => m.Object != null && m.Object.IsValid && m.HasStateAuthority);
        if (meteorite == null)
        {
            Skip("no spawned meteorite in this area");
            yield break;
        }
        // Bots only, so the host's own profile earns nothing from the test
        var pullerPlayers = AllPlayers(runner).Where(player => player.RawEncoded != runner.LocalPlayer.RawEncoded)
            .Where(player => WinchOf(runner, player) != null).Take(6).ToList();
        var pullers = pullerPlayers.Select(player => WinchOf(runner, player)).ToList();
        Do("any pull direction counts", () => meteorite.maxPullSpreadAngle = 180);
        Do($"{pullers.Count} cars winch it", () => pullers.ForEach(winch => meteorite.OnWinchAttached(winch, meteorite.Object)));
        Check("room for every car's pull direction", () => meteorite.winchedPlayersPullDirections.Length >= ExpandedState.Capacity,
            () => $"{meteorite.winchedPlayersPullDirections.Length} slots");
        Do("every rope pulls tight while it's being extracted", () =>
        {
            pullers.ForEach(winch => winch.WinchRopeState = VehicleWinchSystem.WinchRopeStates.Tense);
            meteorite.Status = Meteorite.MeteoriteStatus.Extracting;
            meteorite.FixedUpdateNetwork();
        });
        Check("it starts coming out", () => meteorite.Status == Meteorite.MeteoriteStatus.ExtractingPulling,
            () => $"status {meteorite.Status}");

        // The game's own award, with the host's own player left out of it
        MeteoriteBalancePatches.LastAward = null;
        Do("it comes out and the involved players are rewarded", () =>
        {
            MeteoriteBalancePatches.SkipLocalPlayerAward = true;
            try
            {
                meteorite.CompleteObjectiveForInvolvedPlayers();
            }
            finally
            {
                MeteoriteBalancePatches.SkipLocalPlayerAward = false;
            }
        });
        var award = MeteoriteBalancePatches.LastAward;
        int pebbles = meteorite.meteoriteConfig.PebbleValue;
        int sessionPlayers = Players.GetPlayerCount();
        var prizes = MeteoriteBalancePatches.LastPrizes;
        Check("every winched car counts as involved", () => award != null && award.Value.Involved >= pullers.Count,
            () => award == null ? "no award" : $"{award.Value.Involved} involved");
        Record($"INFO {_section}: {award?.Involved} of {sessionPlayers} players involved, {prizes.Count} told");
        Check("the game splits them between everyone involved, rounding up", () =>
                award != null && award.Value.VanillaShare == (int)Math.Ceiling(pebbles / (float)award.Value.Involved),
            () => award == null ? "no award" : $"{award.Value.VanillaShare} each for {award.Value.Involved}");
        int maxSharing = Plugin.ConfigPebbleShareMaxPlayers.Value;
        int expectedShare = award != null && award.Value.Involved > maxSharing
            ? (int)Math.Ceiling(pebbles / (float)maxSharing)
            : award?.VanillaShare ?? -1;
        Check($"each winched car gets the {maxSharing}-player share of its {pebbles} pebbles", () =>
                award != null && award.Value.Share == expectedShare &&
                pullerPlayers.All(player => prizes.TryGetValue(player.RawEncoded, out int prize) && prize == expectedShare),
            () => string.Join(", ", pullerPlayers.Select(player => prizes.TryGetValue(player.RawEncoded, out int prize) ? $"{prize}" : "none")) +
                  $" (the game's split: {award?.VanillaShare})");
        Check("players who weren't involved get nothing", () =>
                award != null && prizes.Values.Count(prize => prize > 0) == award.Value.Involved &&
                prizes.Values.All(prize => prize == 0 || prize == expectedShare),
            () => $"{prizes.Values.Count(prize => prize > 0)} rewarded: {string.Join(", ", prizes.Values)}");
        float expectedReach = Plugin.ConfigScaleMeteoriteReach.Value && sessionPlayers > Plugin.VanillaMaxPlayers
            ? 10f * MathF.Sqrt(sessionPlayers / (float)Plugin.VanillaMaxPlayers)
            : 10f;
        Check($"cars within {expectedReach:F1} m count for {sessionPlayers} players", () =>
                award != null && Math.Abs(award.Value.Reach - expectedReach) < 0.1f,
            () => award == null ? "no award" : $"{award.Value.Reach:F1} m");
        Check("the meteorite's own reach is put back", () => Math.Abs(meteorite.prizeAwardMaxDistance - 10f) < 0.01f,
            () => $"{meteorite.prizeAwardMaxDistance:F1} m");
        Check($"its ring on the ground shows the same {expectedReach:F1} m",
            () => Math.Abs(MeteoriteReachRingPatches.ShownReach(meteorite) - expectedReach) < 0.1f,
            () => $"{MeteoriteReachRingPatches.ShownReach(meteorite):F1} m");
        Do("the cars let go", () =>
        {
            pullers.ForEach(winch => winch.WinchRopeState = VehicleWinchSystem.WinchRopeStates.Relaxed);
            pullers.ForEach(winch => meteorite.OnWinchDetached(winch, meteorite.Object));
        });
        yield return new Wait(1);
        EndSection();
    }

    // The recovery mission's networked status has 4 slots for the cars winched to the vehicle
    IEnumerable<object> TestItemRecoveryWinches(NetworkRunner runner, List<PlayerRef> bots)
    {
        Section("Recovery vehicle winched by six cars");
        var manager = ItemRecoveryNetworkManager.Instance;
        var points = manager != null ? manager.itemRecoveryPoints : null;
        ItemRecovery point = null;
        for (int i = 0; points != null && i < points.Count && point == null; i++)
        {
            if (points[i] != null)
                point = points[i];
        }
        if (point == null)
        {
            Skip("no item recovery mission in this area");
            yield break;
        }
        if (point.SpawnedObject == null)
        {
            Do("the recovery vehicle is spawned", () => manager.Spawn(point, 0));
            for (int wait = 0; wait < 10 && point.SpawnedObject == null; wait++)
                yield return new Wait(1);
        }

        // The game only spawns recovery vehicles this save has unlocked, so there may be none. A bot's car can stand in:
        // the manager recognises the vehicle by the mission's SpawnedObjectId, so the mission is pointed at the car (and
        // put back afterwards), and the attach and detach events then reach the same bookkeeping. Completing the
        // mission would destroy the stand-in, so that part only runs with a real vehicle.
        var vehicle = point.SpawnedObject;
        var missions = manager.Missions;
        uint missionId = point.info.ItemId;
        ItemRecoveryMissionStatus savedMission = default;
        bool standIn = false, hadMission = false;
        if (vehicle == null && bots.Count > 6 && BotPlayers.TryGetCar(bots[^1], out var botCar))
        {
            Record($"INFO {_section}: this save can't spawn a recovery vehicle, so {BotName(bots[^1])}'s car stands in for it");
            Do("a bot's car stands in for the recovery vehicle", () =>
            {
                hadMission = missions.TryGet(missionId, out savedMission);
                missions.Set(missionId, new ItemRecoveryMissionStatus { Status = savedMission.Status, SpawnedObjectId = botCar.Id });
                point.SetSpawnedObject(botCar);
            });
            vehicle = botCar;
            standIn = true;
        }
        if (vehicle == null)
        {
            Skip("no recovery vehicle, and too few bots for a stand-in");
            yield break;
        }

        // A rope reaching the vehicle raises VehicleWinchSystem.ObjectAttached, which the host's recovery manager listens
        // to, and letting go raises ObjectDetached. Raising them directly drives the same handlers, without waiting for
        // each rope to reach the vehicle. Without the fix, the 5th attach throws.
        var cars = AllPlayers(runner).Where(player => player != bots[^1]).Take(6)
            .Select(player => WinchOf(runner, player)).Where(winch => winch != null).ToList();
        Do($"{cars.Count} cars winch the recovery vehicle", () => cars.ForEach(winch => VehicleWinchSystem.ObjectAttached.Invoke(winch, vehicle)));
        Check($"the {cars.Count - 4} past the 4th wait for a slot", () => ItemRecoveryWinchPatches.WaitingCount(point) == cars.Count - 4,
            () => $"{ItemRecoveryWinchPatches.WaitingCount(point)} waiting");
        Do("a waiting car lets go", () => VehicleWinchSystem.ObjectDetached.Invoke(cars[^1], vehicle));
        Check("it no longer waits", () => ItemRecoveryWinchPatches.WaitingCount(point) == cars.Count - 5,
            () => $"{ItemRecoveryWinchPatches.WaitingCount(point)} waiting");
        Do("one of the first 4 lets go", () => VehicleWinchSystem.ObjectDetached.Invoke(cars[0], vehicle));
        Check("the other waiting car takes its slot", () => ItemRecoveryWinchPatches.WaitingCount(point) == 0,
            () => $"{ItemRecoveryWinchPatches.WaitingCount(point)} waiting");
        if (standIn)
        {
            Do("the rest let go", () => cars.Skip(1).Take(cars.Count - 2).ToList()
                .ForEach(winch => VehicleWinchSystem.ObjectDetached.Invoke(winch, vehicle)));
            Do("the stand-in goes back to being a car", () =>
            {
                point.SetSpawnedObject(null);
                if (hadMission)
                    missions.Set(missionId, savedMission);
                else
                    missions.Remove(missionId);
            });
        }
        else
        {
            Do("the vehicle is recovered", () => manager.ItemRecoveryCompleted(point));
        }
        yield return new Wait(2);
        EndSection();
    }

    // Tents go on one circle around the campfire, which the game sizes for 7
    IEnumerable<object> TestCampTents(int players, Vector3 campfire)
    {
        Section("Tents around the campfire");
        var config = CampManager.Instance != null ? CampManager.Instance.campingConfig : null;
        if (config == null)
        {
            Skip("no camping config");
            yield break;
        }
        float gameDistance = config.tentDistanceFromCampfire;
        Record($"INFO {_section}: at the game's distance, {players} tents are {TentSpacing(campfire, players):F1} m apart and {config.tentWidth:F1} m wide");
        Do($"the camp tool lays out {players} tents", () => CampTentPatches.FitTents(players));
        Check($"{players} tents don't overlap", () => TentSpacing(campfire, players) >= config.tentWidth,
            () => $"{TentSpacing(campfire, players):F1} m apart");
        Do("a party of 4 gets the game's layout back", () => CampTentPatches.FitTents(Plugin.VanillaMaxPlayers));
        Check("the game's distance is back", () => Mathf.Approximately(config.tentDistanceFromCampfire, gameDistance),
            () => $"{config.tentDistanceFromCampfire} instead of {gameDistance}");
        EndSection();
    }

    // DiscordManager plays each other player's voice through an emitter of its own. Without a Discord client the test
    // can't start a real call, so it drives the participant bookkeeping and audio callback directly.
    IEnumerable<object> TestVoiceChatSlots()
    {
        Section("Voice chat slots");
        var discord = DiscordManager.Instance;
        if (discord == null)
        {
            Skip("no Discord manager");
            yield break;
        }
        Do("voice chat makes room before a call", () => VoiceChatCapacityPatches.PadVoiceEmitters(discord));
        var emitters = discord.voiceEmitters;
        var slots = discord.voiceEmittersOccupationStatus;
        Check("an emitter and a slot for every player", () => emitters.Count >= ExpandedState.Capacity && slots.Count == emitters.Count,
            () => $"{emitters.Count} emitters, {slots.Count} slots");
        Check("every emitter is set up like the game's own", () => Enumerable.Range(0, emitters.Count)
            .All(i => emitters[i]._initialized == emitters[0]._initialized && emitters[i]._inputFormat == emitters[0]._inputFormat));

        var others = Enumerable.Range(1, ExpandedState.Capacity - 1)
            .Select(i => (Id: 900000UL + (ulong)i, Player: PlayerRef.FromIndex(i))).ToList();
        Do($"{others.Count} other players join the call", () => others.ForEach(p => WithoutClient(() => discord.UpdateParticipatingUser(p.Id, p.Player, true))));
        Check("each has an emitter of their own", () =>
        {
            var indices = others.Select(p => discord.callParticipants[p.Id].VoiceEmitterIndex).ToList();
            return indices.Distinct().Count() == indices.Count && indices.All(i => i >= 0 && i < emitters.Count);
        });
        // Emitters set themselves up once Discord voice is active; without it even the game's own reject audio
        if (emitters[0]._initialized)
            Do("every one of them is heard", () => PlaySilence(discord, others.Select(p => p.Id)));
        else
            Record($"INFO {_section}: voice emitters aren't set up without a Discord login, so audio playback isn't tested");
        Do("they all leave the call", () => others.ForEach(p => WithoutClient(() => discord.UpdateParticipatingUser(p.Id, p.Player, false))));
        Check("every slot is free again", () => others.All(p => !discord.callParticipants.ContainsKey(p.Id)) &&
                                                Enumerable.Range(0, slots.Count).All(i => !slots[i]));
        EndSection();
    }

    // Arriving through a portal gives each player the spawn point at their position in ActivePlayers, and portals
    // have 4. This replaces every car, the host's included, so it runs last.
    IEnumerable<object> TestPortalArrival(NetworkRunner runner)
    {
        Section("Arriving through a portal");
        var portal = Object.FindObjectsOfType<Portal>().FirstOrDefault();
        if (portal == null)
        {
            Skip("no portal in this area");
            yield break;
        }
        Do("everyone leaves through a portal", () => OverlandNetworkRunner.Instance.DespawnAllPlayers());
        yield return new Wait(2);
        Do($"everyone arrives at '{portal.name}'", () => portal.AssignPlayersToSpawnPoints());
        yield return new Wait(LandingTime);
        Check("every player has a car", () => AllPlayers(runner).All(p => HasCar(runner, p)),
            () => $"no car: {string.Join(", ", AllPlayers(runner).Where(p => !HasCar(runner, p)).Select(p => Players.GetPlayerName(p)))}");
        foreach (var step in CheckLanding(runner, AllPlayers(runner), portal))
            yield return step;
        Check($"'{portal.name}' has a spawn point for every player, a car's length apart", () => SpawnSpacing(portal, AllPlayers(runner).Count) >= MinSpawnSpacing,
            () => $"{portal.spawnPoints.Count} spawn points, closest {SpawnSpacing(portal, AllPlayers(runner).Count):F1} m apart");
        Record($"INFO {_section}: after landing, the closest two cars are {ClosestCars(runner):F1} m apart");
        EndSection();
    }

    void Skip(string why)
    {
        Record($"INFO {_section}: skipped, {why}");
        EndSection();
    }

    static VehicleWinchSystem WinchOf(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject car = player == runner.LocalPlayer
            ? OverlandNetworkRunner.Instance.LocalPlayerNetworkObject
            : BotPlayers.TryGetCar(player, out var botCar) ? botCar : null;
        return car != null ? car.GetComponentInChildren<VehicleWinchSystem>() : null;
    }

    static bool HasCar(NetworkRunner runner, PlayerRef player) =>
        player == runner.LocalPlayer ? OverlandNetworkRunner.Instance.LocalPlayerNetworkObject != null : BotPlayers.TryGetCar(player, out _);

    static bool IsReleased(VehicleWinchSystem winch) =>
        winch.WinchState == VehicleWinchSystem.WinchStates.Disabled;

    static int OwnerCount(VehicleWinchSystem winch)
    {
        var owners = winch.AttachedWinchesOwners;
        int count = 0;
        for (int i = 0; i < owners.Length; i++)
        {
            if (!owners[i].IsNone)
                count++;
        }
        return count;
    }

    static float TentSpacing(Vector3 campfire, int tents)
    {
        var positions = Enumerable.Range(0, tents).Select(i => CampUtils.GetTentPlacementForIndex(campfire, i, tents, 0)).ToList();
        float closest = float.PositiveInfinity;
        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
                closest = Math.Min(closest, Vector3.Distance(positions[i], positions[j]));
        }
        return closest;
    }

    // Feeds 10 ms of silence from each participant through the game's audio callback
    static void PlaySilence(DiscordManager discord, IEnumerable<ulong> participants)
    {
        const int samples = 480;
        var silence = Marshal.AllocHGlobal(samples * sizeof(short));
        try
        {
            for (int i = 0; i < samples; i++)
                Marshal.WriteInt16(silence, i * sizeof(short), 0);
            foreach (ulong id in participants)
            {
                bool mute = false;
                discord.CallReceivedCb(id, silence, samples, 48000, 1, ref mute);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(silence);
        }
    }

    // Without a Discord client, UpdateParticipatingUser throws a NullReferenceException once its own bookkeeping is
    // done (it goes on to ask the call for the participant's volume). Anything else is a real failure.
    static void WithoutClient(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e.Message.Contains("NullReferenceException"))
        {
        }
    }
}

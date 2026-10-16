using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Fusion;
using FunCore.Managers;
using Il2CppInterop.Runtime;
using OverTheHillUnlimited.Networking;
using OverTheHillUnlimited.Patches;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OverTheHillUnlimited.Testing;

/// <summary>Pauses the self-test script for a number of seconds.</summary>
internal sealed class Wait
{
    public Wait(float seconds) => Seconds = seconds;
    public float Seconds { get; }
}

/// <summary>Pauses the self-test script until a condition holds, failing the run if it times out.</summary>
internal sealed class Until
{
    public Until(string what, Func<bool> condition, float timeoutSeconds)
    {
        What = what;
        Condition = condition;
        TimeoutSeconds = timeoutSeconds;
    }

    public string What { get; }
    public Func<bool> Condition { get; }
    public float TimeoutSeconds { get; }
}

/// <summary>
/// The self-test, run by launching the game with -otu-selftest. Without any other players, it hosts a session in
/// Unlimited mode and joins bot players through the game's own join path, until the session holds MaxPlayers. It then
/// drives everything that indexes per-player data: the player registry, spawn points, lobby labels, busy states, map
/// pins, portable winch anchors and their HUD markers, meteorite setups, the compass, the car health HUD, and the map,
/// pause, car repair and tool selection menus. Each section also fails if the game or BepInEx logged an error during
/// it. Results go to the BepInEx log and to BepInEx/OverTheHillUnlimited-selftest.txt, and the game quits afterwards
/// unless -otu-selftest-stay is also given. -otu-selftest-area &lt;name&gt; picks the area (default: the first one).
/// </summary>
internal sealed class SelfTest
{
    public const string Flag = "-otu-selftest";
    const string StayFlag = "-otu-selftest-stay";
    const string AreaFlag = "-otu-selftest-area";

    readonly ManualLogSource _log;
    readonly LogWatcher _watcher;
    readonly List<string> _results = new();
    string _section = "";
    int _passed;
    int _failed;
    bool? _savedMode;

    public SelfTest(ManualLogSource log)
    {
        _log = log;
        _watcher = new LogWatcher(log.SourceName);
    }

    public static bool Requested => HasArgument(Flag);

    public IEnumerator<object> Run()
    {
        _log.LogInfo("Self-test starting: hosting a session and filling it with bot players");
        _savedMode = Plugin.ConfigExpandPlayerSlots.Value;

        Section("Setup");
        if (!Check("the game's networked layout matches the mod", () => UnlimitedMode.Available))
        {
            Finish();
            yield break;
        }
        if (!UnlimitedMode.Enabled)
            Check("Unlimited mode switched on", () => UnlimitedMode.TrySetEnabled(true));
        yield return new Until("main menu", () => GameStateManager.Instance?.PeekState()?.TryCast<StateMainMenu>() != null, 300);
        yield return new Wait(3);

        AreaInfo area = null;
        Check("area found", () => (area = FindArea(ArgumentValue(AreaFlag))) != null);
        if (area == null)
        {
            Finish();
            yield break;
        }
        int maxPlayers = Plugin.ConfigMaxPlayers.Value;
        var context = new GameSetupContext
        {
            GameMode = GameMode.Host,
            MaxPlayers = maxPlayers,
            RoomName = "OTU self-test",
            ServerRegion = "auto",
            AreaInfo = area,
        };
        Do($"host '{area.name}' for {maxPlayers} players", () => GameStateManager.Instance.PushState(new StateStartGame(context)));
        yield return new Until("session started", () => Players != null && Players.GetPlayerCount() >= 1, 300);
        yield return new Until("lobby open", () => GameStateManager.Instance.PeekState()?.TryCast<StateLobbyMenu>() != null, 120);
        yield return new Wait(3);
        EndSection();

        Section("Session");
        var runner = OverlandNetworkRunner.Instance.NetworkRunner;
        Check("Fusion session size matches Max Players", () => runner.SessionInfo.MaxPlayers == maxPlayers,
            () => $"session {runner.SessionInfo.MaxPlayers}, wanted {maxPlayers}");
        Check("player list expanded", () => SessionPlayersCapacityPatches.State.IsExpanded(Players));
        Check("map state expanded", () => MapNetworkManager.Instance != null && MapNetworkCapacityPatches.State.IsExpanded(MapNetworkManager.Instance));
        var camp = Object.FindObjectOfType<CampManager>();
        Check("camp state expanded", () => camp == null || CampCapacityPatches.State.IsExpanded(camp),
            () => "no CampManager in this area");
        Check("Unlimited mode can't be switched mid-session", () => !UnlimitedMode.TrySetEnabled(false) && UnlimitedMode.Enabled);
        Do("saved setting changed from outside the mod", () => Plugin.ConfigExpandPlayerSlots.Value = false);
        Check("the change is undone", () => Plugin.ConfigExpandPlayerSlots.Value && UnlimitedMode.Enabled);
        Record($"INFO {_section}: per call, an unpatched networked getter takes {MicrosecondsPerCall(() => _ = Players.HostPlayerRef):F1} µs, " +
               $"the expanded player order {MicrosecondsPerCall(() => _ = Players.OrderedPlayerRefs):F1} µs, " +
               $"the expanded busy states {MicrosecondsPerCall(() => _ = Players.BusyPlayerRefToBusyReason):F1} µs");
        EndSection();

        Section("Bots join the lobby");
        var bots = new List<PlayerRef>();
        int hostIndex = runner.LocalPlayer.AsIndex;
        for (int index = maxPlayers - 1, n = 1; bots.Count < maxPlayers - 1 && index >= 0; index--)
        {
            if (index == hostIndex)
                continue;
            string name = $"Bot {n++}";
            if (Do($"{name} joins", () => bots.Add(BotPlayers.Join(index, name))))
                yield return new Wait(0.5f);
        }
        yield return new Wait(3);
        Check($"{maxPlayers} players registered", () => Players.GetPlayerCount() == maxPlayers,
            () => $"{Players.GetPlayerCount()} registered");
        foreach (var bot in bots)
        {
            string name = BotName(bot);
            Check($"{name} has its name", () => Players.GetPlayerName(bot) == name, () => $"'{Players.GetPlayerName(bot)}'");
            Check($"{name} has a car", () => runner.TryGetPlayerObject(bot, out var car) && car != null);
            Check($"{name} has a colour slot", () => SlotOf(Players.PlayerColors, bot) >= 0);
        }
        if (PlayerColourPatches.Supported)
        {
            Check("every player has a distinct colour", () =>
            {
                var colours = AllPlayers(runner).Select(p => UIManager.GetPlayerColor(p)).ToList();
                return colours.Distinct().Count() == colours.Count;
            }, () => string.Join(", ", AllPlayers(runner).Select(p => $"{Players.GetPlayerName(p)}={UIManager.GetPlayerColor(p)}")));
        }
        var lobby = Object.FindObjectOfType<Lobby>();
        Check("every player has their own lobby spawn point", () =>
        {
            var indices = AllPlayers(runner).Select(p => { lobby.GetSpawnPoint(p, out int i); return i; }).ToList();
            return indices.Distinct().Count() == indices.Count;
        });
        var lobbyMenu = GameStateManager.Instance.PeekState()?.TryCast<StateLobbyMenu>();
        Do("lobby name labels update", () => lobbyMenu.UpdateCarsStatus());
        Check("a lobby name label for every player", () => CountActive(lobbyMenu.lobbyMenuContextUI.LobbyPlayersStatus) == maxPlayers,
            () => $"{CountActive(lobbyMenu.lobbyMenuContextUI.LobbyPlayersStatus)} shown");
        Do("bots show as busy", () => bots.ForEach(bot => Players.RPC_SetBusyState(bot, 1, true)));
        yield return new Wait(1);
        Do("bots stop being busy", () => bots.ForEach(bot => Players.RPC_SetBusyState(bot, 1, false)));
        yield return new Wait(1);
        EndSection();

        Section("Free roam");
        Check("lobby closes", () => BenchmarkBootRunner.TryCloseLobby());
        yield return new Wait(5);
        var origin = OverlandNetworkRunner.Instance.LocalPlayerNetworkObject.transform.position;
        Do("every player places a map pin", () =>
        {
            foreach (var (player, i) in AllPlayers(runner).Select((p, i) => (p, i)))
                MapNetworkManager.Instance.RPC_InstantiatePinpoint(player, origin + new Vector3(50 + 10 * i, 0, 50));
        });
        Do("every bot places a portable winch anchor", () =>
        {
            foreach (var (bot, i) in bots.Select((b, i) => (b, i)))
                MapNetworkManager.Instance.RPC_PlacePortableWinchAnchor(bot, origin + new Vector3(-10 - 3 * i, 0, 10));
        });
        yield return new Wait(3);
        Check("a map pin for every player", () => MapNetworkManager.Instance.PlayerRefToPinpointPosition.Count == maxPlayers,
            () => $"{MapNetworkManager.Instance.PlayerRefToPinpointPosition.Count} pins");
        Check("a winch anchor for every bot", () => MapNetworkManager.Instance.PlayerRefToPortableWinchAnchorNetworkObject.Count == bots.Count,
            () => $"{MapNetworkManager.Instance.PlayerRefToPortableWinchAnchorNetworkObject.Count} anchors");
        Do("HUD winch anchor markers update", () => HUDManager.Instance.OnPortableWinchAnchorsChanged());
        Check("a HUD marker for every anchor", () => HUDManager.Instance.portableWinchAnchorIndicators.Length >= bots.Count);
        Do("meteorite setups update", () => MeteoriteSpawnController.Instance?.UpdateMeteoritesSizes());
        var compass = Object.FindObjectOfType<CompassController>();
        Do("compass updates", () => compass?.UpdateOtherCarsCompassIcon());
        var carHealth = Object.FindObjectOfType<CarHealthUI>();
        Do("car health HUD updates", () => carHealth?.SetupPlayerCarHealthStatus());
        Check("car health HUD has a marker for every player", () => carHealth == null || carHealth.carHealthStatusButtons.Count >= maxPlayers,
            () => $"{carHealth.carHealthStatusButtons.Count} markers");
        EndSection();

        var carDamage = OverlandNetworkRunner.Instance.LocalPlayerNetworkObject.GetComponentInChildren<CarDamageSystem>();
        foreach (var step in OpenMenu("Map", () => new StateMapScreen(false), _ =>
                 {
                     var map = Object.FindObjectOfType<MapView>();
                     Check("map is open", () => map != null);
                     // The host skipped the normal lobby exit (TryCloseLobby), so the game doesn't count it as spawned in
                     // the map and hides its marker; every other player should have one
                     int spawned = AllPlayers(runner).Count(p => Players.SpawnedInMap(p));
                     Check($"a map marker for each of the {spawned} players spawned in the map",
                         () => spawned >= maxPlayers - 1 && CountActive(map.playerIndicators) >= spawned, () => MissingMapMarkers(map));
                     Check("pin markers and their rects stay in step", () =>
                             map.pinpointIndicators.Count >= maxPlayers && map.pinpointIndicatorsRectTransforms.Count == map.pinpointIndicators.Count,
                         () => $"{map.pinpointIndicators.Count} pins, {map.pinpointIndicatorsRectTransforms.Count} rects");
                 }))
            yield return step;
        foreach (var step in OpenMenu("Pause menu", () => new StatePauseMenu(), PauseMenuSteps))
            yield return step;
        foreach (var step in OpenMenu("Car repair menu", () => new StateCarRepair(carDamage, 0), state =>
                 {
                     var repair = state.TryCast<StateCarRepair>();
                     var buttons = repair.contextUICarRepair.CarRepairButtons;
                     int inRange = bots.Count(bot => BotPlayers.TryGetCar(bot, out var car) &&
                         Vector3.Distance(car.transform.position, origin) <= repair.maxRepairRange);
                     Check($"a repair button for each of the {inRange} bots in range", () => CountActive(buttons) >= inRange,
                         () => $"{CountActive(buttons)} shown of {buttons.Count}");
                 }))
            yield return step;
        foreach (var step in OpenMenu("Tool selection menu", () => new StateSelectTool(carDamage), state =>
                 {
                     var tools = state.TryCast<StateSelectTool>();
                     Check("a car health button for every player", () => tools.contextUISelectTool.CarHealthButtons.Count >= maxPlayers);
                     Check("a tools panel for every player", () => tools.contextUISelectTool.AvailableToolsPanels.Count >= maxPlayers);
                 }))
            yield return step;

        Section("Bots leave");
        foreach (var bot in bots)
        {
            string name = BotName(bot);
            Do($"{name} leaves", () => BotPlayers.Leave(bot));
            yield return new Wait(0.5f);
        }
        yield return new Wait(3);
        Check("only the host is left", () => Players.GetPlayerCount() == 1, () => $"{Players.GetPlayerCount()} registered");
        EndSection();

        Section("Back to the main menu");
        Do("quit to the main menu", () => OverlandNetworkRunner.Instance.QuitToMenuAsync(default, ""));
        yield return new Until("main menu", () => Players == null && GameStateManager.Instance.PeekState()?.TryCast<StateMainMenu>() != null, 120);
        yield return new Wait(3);
        EndSection();

        foreach (var step in TestServerBrowserCheckbox())
            yield return step;
        foreach (var step in TestVanillaMode(area))
            yield return step;

        Finish();

        // Rows for players 5 and up are padded clones, so check they open the player options like the game's own rows
        IEnumerable<object> PauseMenuSteps(FunCore.StateMachine.State state)
        {
            var rows = state.TryCast<StatePauseMenu>().contextUIPauseMenu.PlayerInfos;
            Check("a row for every player", () => CountActive(rows) == maxPlayers, () => $"{CountActive(rows)} rows shown");
            Check("every row has one click handler", () => Enumerable.Range(0, rows.Count).All(i => HandlerCount(rows[i].ButtonSelector) == 1),
                () => string.Join(", ", Enumerable.Range(0, rows.Count).Select(i => HandlerCount(rows[i].ButtonSelector))));

            var ordered = Players.OrderedPlayerRefs;
            int row = Enumerable.Range(Plugin.VanillaMaxPlayers, Math.Max(0, rows.Count - Plugin.VanillaMaxPlayers))
                .Where(i => i < ordered.Length && rows[i].gameObject.activeSelf && bots.Any(b => b.RawEncoded == ordered[i].RawEncoded))
                .DefaultIfEmpty(-1).Last();
            if (!Check("a bot has a padded row", () => row >= 0))
                yield break;
            string name = BotName(ordered[row]);
            Do($"click {name}'s row (row {row + 1})", () => rows[row].ButtonSelector.OnPointerClickOrSubmit.Invoke(rows[row].ButtonSelector));
            yield return new Wait(1);
            Check($"{name}'s player options open", () => GameStateManager.Instance.PeekState()?.TryCast<StatePauseMenuPlayerOptions>() != null,
                () => $"top state is {GameStateManager.Instance.PeekState()?.GetIl2CppType().Name}");
            Do("player options close", () => GameStateManager.Instance.PopState());
            yield return new Wait(1);
        }
    }

    // Clicks the server browser's Unlimited mod servers checkbox through its real click handler, twice
    IEnumerable<object> TestServerBrowserCheckbox()
    {
        Section("Server browser checkbox");
        if (!Do("server browser opens", () => GameStateManager.Instance.PushState(new StateServerBrowser(new GameSetupContext()))))
        {
            EndSection();
            yield break;
        }
        yield return new Until("server browser checkbox", () => ServerBrowserTogglePatches.Toggle != null, 60);
        var toggle = ServerBrowserTogglePatches.Toggle;

        // Sampled every frame until the lobby is connected: a click before then would leave the first connection open
        int framesConnecting = 0, framesClickable = 0;
        yield return new Until("lobby connection", () =>
        {
            if (OverlandNetworkRunner.Instance?.IsConnectedToLobby == true)
                return true;
            framesConnecting++;
            if (toggle.buttonSelector.interactable)
                framesClickable++;
            return false;
        }, 60);
        Check($"checkbox disabled while the lobby connects ({framesConnecting} frames)", () => framesClickable == 0,
            () => $"clickable for {framesClickable} frames");
        yield return new Wait(2);

        Check("checkbox is labelled", () => toggle.label.text == "Unlimited mod servers", () => $"'{toggle.label.text}'");
        foreach (bool expected in new[] { false, true })
        {
            Do($"click switches Unlimited mode {(expected ? "on" : "off")}", () => toggle.buttonSelector.OnPointerClickOrSubmit.Invoke(toggle.buttonSelector));
            Check($"Unlimited mode is {(expected ? "on" : "off")}", () => UnlimitedMode.Enabled == expected);
            Check("checkbox waits for the lobby to reconnect", () => ServerBrowserTogglePatches.IsWaitingForLobby && !toggle.buttonSelector.interactable);
            yield return new Until("lobby reconnects", () => !ServerBrowserTogglePatches.IsWaitingForLobby, 30);
            yield return new Wait(0.5f);
            Check("checkbox usable again", () => toggle.buttonSelector.interactable);
        }
        Do("server browser closes", () => GameStateManager.Instance.PopState());
        yield return new Wait(2);
        EndSection();
    }

    // Hosts once more with Unlimited mode off and checks the game stays vanilla
    IEnumerable<object> TestVanillaMode(AreaInfo area)
    {
        Section("Vanilla mode");
        Check("Unlimited mode switches off", () => UnlimitedMode.TrySetEnabled(false));
        var context = new GameSetupContext
        {
            GameMode = GameMode.Host,
            MaxPlayers = Plugin.VanillaMaxPlayers,
            RoomName = "OTU self-test (vanilla)",
            ServerRegion = "auto",
            AreaInfo = area,
        };
        Do("host a vanilla session", () => GameStateManager.Instance.PushState(new StateStartGame(context)));
        yield return new Until("vanilla session started", () => Players != null && Players.GetPlayerCount() >= 1, 300);
        yield return new Wait(3);

        var runner = OverlandNetworkRunner.Instance.NetworkRunner;
        Check("Fusion session size is the vanilla 4", () => runner.SessionInfo.MaxPlayers == Plugin.VanillaMaxPlayers,
            () => $"{runner.SessionInfo.MaxPlayers}");
        Check("player list not expanded", () => !SessionPlayersCapacityPatches.State.IsExpanded(Players));
        Check("map state not expanded", () => !MapNetworkCapacityPatches.State.IsExpanded(MapNetworkManager.Instance));
        Check("vanilla player list has 4 slots", () => Players.OrderedPlayerRefs.Length == Plugin.VanillaMaxPlayers,
            () => $"{Players.OrderedPlayerRefs.Length}");
        Check("Fusion's default player count is back to the vanilla 4",
            () => NetworkProjectConfig.Global.Simulation.PlayerCount == Plugin.VanillaMaxPlayers,
            () => $"{NetworkProjectConfig.Global.Simulation.PlayerCount}");
        EndSection();
    }

    /// <summary>Fails the run when a wait times out.</summary>
    public void TimedOut(string what)
    {
        Fail($"timed out waiting for {what}");
        Finish();
    }

    /// <summary>Fails the run when the script itself throws.</summary>
    public void Crashed(Exception exception)
    {
        Fail($"self-test threw {exception.GetType().Name}: {FirstLine(exception.Message)}");
        Finish();
    }

    static OverlandSessionPlayers Players => OverlandSessionPlayers.Instance;

    IEnumerable<object> OpenMenu(string name, Func<FunCore.StateMachine.State> create, Action<FunCore.StateMachine.State> check) =>
        OpenMenu(name, create, state =>
        {
            check(state);
            return Enumerable.Empty<object>();
        });

    IEnumerable<object> OpenMenu(string name, Func<FunCore.StateMachine.State> create, Func<FunCore.StateMachine.State, IEnumerable<object>> steps)
    {
        Section(name);
        FunCore.StateMachine.State state = null;
        if (!Do("opens", () => GameStateManager.Instance.PushState(state = create())))
        {
            EndSection();
            yield break;
        }
        yield return new Wait(3);
        foreach (var step in steps(state))
            yield return step;
        Do("closes", () => GameStateManager.Instance.PopState());
        yield return new Wait(2);
        EndSection();
    }

    void Section(string name)
    {
        _section = name;
        _log.LogInfo($"--- {name}");
        _watcher.Mark();
    }

    void EndSection()
    {
        var errors = _watcher.ErrorsSinceMark();
        Check("no errors logged", () => errors.Count == 0, () => string.Join(" | ", errors.Take(5)));
    }

    bool Do(string what, Action action) => Check(what, () =>
    {
        action();
        return true;
    });

    bool Check(string what, Func<bool> condition, Func<string> detail = null)
    {
        bool passed;
        string info = "";
        try
        {
            passed = condition();
            if (!passed && detail != null)
                info = detail();
        }
        catch (Exception e)
        {
            passed = false;
            info = $"{e.GetType().Name}: {FirstLine(e.Message)}";
        }

        if (passed)
        {
            _passed++;
            Record($"PASS {_section}: {what}");
        }
        else
        {
            Fail(string.IsNullOrEmpty(info) ? what : $"{what} ({info})");
        }
        return passed;
    }

    void Fail(string what)
    {
        _failed++;
        Record($"FAIL {_section}: {what}");
    }

    void Record(string line)
    {
        _results.Add(line);
        if (line.StartsWith("FAIL"))
            _log.LogWarning(line);
        else
            _log.LogInfo(line);
    }

    void Finish()
    {
        string summary = _failed == 0 ? $"Self-test PASSED: {_passed} checks" : $"Self-test FAILED: {_failed} of {_passed + _failed} checks failed";
        _results.Add(summary);
        _log.LogMessage(summary);
        try
        {
            File.WriteAllLines(Path.Combine(Paths.BepInExRootPath, "OverTheHillUnlimited-selftest.txt"), _results);
        }
        catch (IOException e)
        {
            _log.LogWarning($"Couldn't write the results file: {e.Message}");
        }
        _watcher.Dispose();

        // Put the player's saved Unlimited mode back. If the run stopped mid-session, that's only allowed because the
        // game is about to quit.
        bool quit = !HasArgument(StayFlag);
        if (_savedMode.HasValue && Plugin.ConfigExpandPlayerSlots.Value != _savedMode.Value)
        {
            if (quit)
                UnlimitedMode.SaveForNextLaunch(_savedMode.Value);
            else if (!UnlimitedMode.TrySetEnabled(_savedMode.Value))
                _log.LogWarning("Still in a session, so your saved Unlimited mode setting wasn't restored");
        }

        if (quit)
            Application.Quit();
    }

    // The area to host in, looked up the way the game's benchmark mode does: by name, or its default area for null
    static AreaInfo FindArea(string name) => BenchmarkBootRunner.ResolveArea(name);

    static List<PlayerRef> AllPlayers(NetworkRunner runner)
    {
        var players = new List<PlayerRef> { runner.LocalPlayer };
        players.AddRange(BotPlayers.All);
        return players;
    }

    static int SlotOf(NetworkArray<PlayerRef> slots, PlayerRef player)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].RawEncoded == player.RawEncoded)
                return i;
        }
        return -1;
    }

    static int HandlerCount(UIButtonSelector button) =>
        button.OnPointerClickOrSubmit?.GetInvocationList().Length ?? 0;

    static double MicrosecondsPerCall(Action call, int calls = 2000)
    {
        call();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < calls; i++)
            call();
        return watch.Elapsed.TotalMilliseconds * 1000 / calls;
    }

    static int CountActive<T>(Il2CppSystem.Collections.Generic.List<T> elements) where T : Component
    {
        int active = 0;
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i] != null && elements[i].gameObject.activeSelf)
                active++;
        }
        return active;
    }

    // MapView.UpdatePlayerIndicators shows a slot's marker when the player is SpawnedInMap and has a position
    static string MissingMapMarkers(MapView map)
    {
        var ordered = Players.OrderedPlayerRefs;
        var missing = new List<string>();
        for (int slot = 0; slot < ordered.Length && slot < map.playerIndicators.Count; slot++)
        {
            var player = ordered[slot];
            if (player.IsNone || map.playerIndicators[slot].gameObject.activeSelf)
                continue;
            bool hasPosition = OverlandNetworkRunner.Instance.GetPlayerPositionAndRotation(player, out _, out _);
            missing.Add($"{Players.GetPlayerName(player)} in slot {slot} (spawned in map: {Players.SpawnedInMap(player)}, position: {hasPosition})");
        }
        return $"{CountActive(map.playerIndicators)} shown; missing {string.Join(", ", missing)}";
    }

    static string BotName(PlayerRef bot) => OverlandSessionPlayers.Instance?.GetPlayerName(bot) ?? $"Bot {bot.PlayerId}";

    static string FirstLine(string text)
    {
        int newline = text?.IndexOf('\n') ?? -1;
        return newline < 0 ? text : text[..newline];
    }

    static bool HasArgument(string flag) =>
        Environment.GetCommandLineArgs().Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    static string ArgumentValue(string flag)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}

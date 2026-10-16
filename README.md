# Over the Hill Unlimited

A [BepInEx](https://github.com/BepInEx/BepInEx) mod that raises the 4-player co-op cap in
[Over the Hill](https://store.steampowered.com/app/2929250/), inspired by [PEAK Unlimited](https://github.com/glarmer/PEAK-Unlimited).

> **Status: early and experimental.** A modded host can create a server for more than 4 players, and the game's
> networked player state has room for 16. That's been verified hosting solo. Sessions with real players haven't
> been tested yet.

**Download:** [Releases](https://github.com/t04glovern/over-the-hill-unlimited/releases). See [Installation](#installation).

## Status

| Piece | State |
|---|---|
| Host can pick up to 16 in Create Server, Max Players | Verified |
| Photon Fusion room uses that size (`1/8 players`) | Verified |
| A 5th player can join | Verified |
| Networked player list (order, colours, names) holds 16 players | Verified solo |
| Map pins, portable winch anchors and camp tents hold 16 players | Pins verified solo; anchors and tents untested |
| Map and player list UI refresh when the expanded data changes | Verified solo for map pins |
| Lobby spawn points and meteorite spawns with 5+ players | Both errored in the first 5-player session; padded in 0.1.1, 5-player retest needed |
| Lobby name labels, pause menu, map, car repair and tool selection menus, car health and winch anchor markers, compass | Room for 16 players; passes the [self-test](#testing-without-other-players) with 8 players, untested with real players. Pause menu rows 5+ open their player options (fixed in 0.3.0) |
| Lobby with 5+ cars | Extra rows behind the first, and the camera rises to show every car; verified by the self-test with 16 |
| Spawning in the map with 5+ players | Extra spawn points around each cabin, so cars don't land on top of each other; verified by the self-test with 16 |
| Game updates | The mod checks the game's networked layout at startup and stays vanilla if it changed |
| Colours for players 5+ | Their own colours on Windows (self-test); the game's default colour on native Linux |
| Native Linux (not Proton) | Joining crashed in 0.1.0; fixed in 0.1.1, untested |
| Players without the mod | Kept out: modded and unmodded players don't see each other's servers (verified: modded lobby lists 0 vanilla servers) |
| Switching between modded and vanilla in game | Verified: the "Unlimited mod servers" checkbox in the server browser switches the list and Max Players |

## Installation

Everyone in the session needs to do this, not just the host, and everyone needs the same mod version.

The server browser gets an **Unlimited mod servers** checkbox next to its filters. While it's ticked, you only see and
host modded servers, with up to 16 players. Untick it to see vanilla servers and play with friends who don't have the
mod (4 players max). Your choice is remembered.

### Windows

1. **Open your game folder.** In Steam, right-click Over the Hill, then Manage, then Browse local files.
2. **Install BepInEx.** Download
   [BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip)
   and extract it into the game folder, so that `winhttp.dll` sits next to `over the hill.exe`. Your game folder
   should then look like this:

   ![Game folder with BepInEx installed: BepInEx and dotnet folders, plus winhttp.dll, doorstop_config and .doorstop_version, next to over the hill.exe](docs/mod-installed.png)

3. **Run the game once**, wait for the main menu, then quit. The first launch takes a minute or two longer than usual.
4. **Install the mod.** Download `OverTheHillUnlimited-<version>.zip` from
   [Releases](https://github.com/t04glovern/over-the-hill-unlimited/releases) and extract it into the game folder.
   You should end up with `BepInEx\plugins\OverTheHillUnlimited\OverTheHillUnlimited.dll`.
5. **Play.** The host picks a Max Players above 4 under Create Server.

To uninstall, delete `winhttp.dll` from the game folder.

### Linux and Steam Deck

Over the Hill has a native Linux version, and that's what Steam installs by default. The mod is only built and tested
against the Windows version, so on Linux you run the Windows version through Proton. On a Steam Deck, do this in
Desktop Mode. These steps haven't been fully tested on Linux yet.

1. **Switch to the Windows version.** In Steam, right-click Over the Hill, then Properties, then Compatibility. Tick
   "Force the use of a specific Steam Play compatibility tool", pick Proton Experimental, and let Steam finish
   updating the game.
2. **Open your game folder.** In Steam, right-click Over the Hill, then Manage, then Browse local files.
   It's usually `~/.local/share/Steam/steamapps/common/over the hill`, and it should now contain `over the hill.exe`.
3. **Install BepInEx.** Download the
   [same BepInEx zip](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip)
   and extract it into the game folder, so that `winhttp.dll` sits next to `over the hill.exe`, like the screenshot above.
4. **Tell Proton to load BepInEx.** In Steam, right-click Over the Hill, then Properties. Under General, set
   Launch Options to:

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

5. **Run the game once**, wait for the main menu, then quit. The first launch takes a few minutes longer than usual.
6. **Install the mod.** Download `OverTheHillUnlimited-<version>.zip` from
   [Releases](https://github.com/t04glovern/over-the-hill-unlimited/releases) and extract it into the game folder.
   You should end up with `BepInEx/plugins/OverTheHillUnlimited/OverTheHillUnlimited.dll`.
7. **Play.** The host picks a Max Players above 4 under Create Server.

To uninstall, clear the launch option, then untick the compatibility option to go back to the native version.

## Configuration

`BepInEx/config/OverTheHillUnlimited.cfg` is created on first launch.

| Setting | Default | Description |
|---|---|---|
| `General.MaxPlayers` | `8` | Highest option in the Max Players menu in Unlimited mode, 4 to 16. The vanilla 2 to 4 are always offered. |
| `General.ExpandPlayerSlots` | `true` | Unlimited mode, the same setting as the **Unlimited mod servers** checkbox in the server browser. Off means vanilla behaviour, for playing with people who don't have the mod (4 players max). |
| `Debug.VerboseLogging` | `false` | Log the session size and player list as players join and leave. Useful for testing. |

## How it works

Over the Hill is a Unity 6 IL2CPP game that uses Photon Fusion 2 in host mode. Two separate things cap it at 4 players,
and fixing the second one exposes a third.

### 1. Session size

```
Create Server menu ──► GameSetupContext.MaxPlayers ──X   game always passes 4   ──► StartGameArgs.PlayerCount ──► Fusion room
 options "2" "3" "4"                                                                                                    max 4
```

- `CreateServerMenuPatch` appends `5..MaxPlayers` to the menu's options. The game parses the chosen option into
  `GameSetupContext.MaxPlayers` itself.
- The game ignores that value when it starts Fusion. It always passes `StartGameArgs.PlayerCount = 4` and enforces
  smaller choices on its own. `SessionSizePatches` remembers the menu value in `OverlandNetworkRunner.StartGame`.
  It then writes that value into the copy of `StartGameArgs` that Fusion's `StartGameModeCloud` async method keeps in
  its state machine. That happens on the state machine's first `MoveNext()`, before the Photon room is created from it.
  (Patching `NetworkRunner.StartGame(StartGameArgs)` directly works on Windows but crashes on native Linux; see
  [Gotchas](#gotchas).)
- It also raises Fusion's default player count, `NetworkProjectConfig.Simulation.PlayerCount` (shipped as 4), to 16
  on every modded peer, and puts it back to 4 in vanilla mode.

### 2. Fixed-size networked state

Fusion stores each `NetworkBehaviour`'s `[Networked]` properties in a fixed block of 32-bit words, and replicates that
block from the host to every client. Networked collections have a compile-time size (`[Capacity(n)]`), and Fusion's weaver
bakes their offsets into the property getters. The game sizes its per-player collections for 4 players:

| Behaviour | Per-player collections | Vanilla words | Modded words |
|---|---|---|---|
| `OverlandSessionPlayers` | player order, colours, names, busy state, dev team and supporter edition flags | 390 | 1250 |
| `MapNetworkManager` | map pinpoint positions and objects, portable winch anchors | 107 | 354 |
| `CampManager` | spawned tents | 10 | 26 |

The mod leaves the vanilla words alone. It appends a 16-slot copy of each collection after them:

```
OverlandSessionPlayers state block (replicated by Fusion)
┌───────────────────────────────┬──────────────────────────────────────────────────┐
│ vanilla: 390 words            │ appended by the mod: 860 words                   │
│ 4-slot collections (unused)   │ 16-slot player order, colours, names, flags ...  │
└───────────────────────────────┴──────────────────────────────────────────────────┘
```

- Fusion asks `NetworkBehaviourUtils.GetWordCount(behaviour)` how big each block should be. A postfix adds the extra
  words for these three behaviours.
- A postfix on each collection's property getter returns a collection built over the appended words instead. It uses
  the same constructor as the woven getter, with a different pointer and capacity. Game code that goes through the
  getters therefore sees 16 slots.
- Fusion replicates the whole block, so the appended slots should sync like any other networked state. This is the
  part the 2-player test still needs to confirm.
- Fusion's `ChangeDetector` only compares each property's *vanilla* word range, so on its own it never sees changes to
  the appended copies. `MapNetworkManager` and `OverlandSessionPlayers` refresh their UI from it in `Render()`, which made
  map pins appear only after reopening the map. To fix that, a prefix on their `Render()` runs on the host first. It writes
  a hash of each appended collection into the last word of the vanilla range it replaces, which is otherwise unused.
  When the real data changes the hash changes, so the property reads as changed. The hash also replicates to clients, so
  their change detectors fire too. `CampManager` doesn't use a `ChangeDetector`.

[`Networking/ExpandedState.cs`](src/OverTheHillUnlimited/Networking/ExpandedState.cs) holds the layout maths, builds the
collections and signals changes. Each behaviour has a small patch class that declares its collections and where their
vanilla versions live. At startup the mod checks those against the game's own weaving attributes (see
[Safety](#safety)).

### 3. UI built for 4 players

Several UIs keep one pre-built element per player, laid out in the menus or HUD, and index it by the player's slot or
position in a loop over the players. With 5 players they read past the end with `ArgumentOutOfRangeException`.
`PerPlayerUiPatches` pads each list to 16 with hidden clones of its last element. The padding runs in prefixes on the
methods that index the list, and the game shows the clones it uses, as it does its own. This covers:

- the lobby name labels (`StateLobbyMenu.UpdateCarsStatus`)
- the pause menu's player rows (`StatePauseMenu.SetupMenu`)
- the map's player markers and pins (`MapView.OnEnable`, `SetupPlayerNames` and `UpdatePlayersPinpoints`)
- the car repair buttons (`StateCarRepair`)
- the tool selection menu's car health buttons and tool panels (`StateSelectTool`)
- the HUD's car health markers (`CarHealthUI`) and portable winch anchor markers (`HUDManager.portableWinchAnchorIndicators`,
  an array)

The game positions these elements itself: most track a car or anchor in the world, and hidden rows take no space in a
layout group. So clones need no layout, and players 5 and up get their own.

`Instantiate` doesn't copy event handlers that the game adds in code. The pause menu adds each row's click handler, which
opens that player's options (kick, mute, volume), in `StatePauseMenu.Open` before it calls `SetupMenu`. So the padding
adds the same handler, `OpenPlayerMenu(row, slot)`, to each row it creates. The other padded elements are either not
clickable or are wired up after the padding runs.

The map also keeps `pinpointIndicatorsRectTransforms`, parallel to its pins, and fills it with a hard-coded loop over
the first 4 pins. A prefix on `MapView.UpdatePlayerPinpoint` adds the rects for the padded pins.

The compass builds its icons as it needs them, so it handles any number of players. Player notifications come from a
shared pool, not one per slot. Player colours (`UIManager.GetPlayerColor`) and pin flares come from a switch on the
player's colour slot, with a default for anything past the 4th. `PlayerColourPatches` gives colour slots 5 to 16 their
own colours, on Windows only. `GetPlayerColor` returns a 16-byte `Color`, which native Linux returns in registers, and
that's the kind of struct Il2CppInterop's trampolines get wrong there. Flares keep the default.

### 4. Game data sized for 4 players

Some game data has one entry per player, and the code indexes it by player count or slot. With 5 players, it reads
past the end. `PerPlayerTablePatches` pads each table up to 16 entries the first time the game reads it:

- `MeteoritesSpawnConfig.SpawnableMeteoritesByPlayersCount`, indexed by `ActivePlayers.Count() - 1`, gets copies of
  its 4-player entry.
- `Lobby.spawnPoints`, indexed by the player's slot, gets clones of the 4 vanilla spawn points, in extra rows 7 m
  behind them. The game's lobby camera is a fixed, nearly level shot of the front row, so `LobbyCameraPatches` raises
  it to look down over the rows from the same direction, and backs it off until every car the session can hold fits.
  Sessions for 4 or fewer keep the game's own shot.
- Each place players spawn in the map, such as a cabin, has a few spawn points (4 at the starting cabin). Once they're
  taken, `SpawnablePointOfInterest.GetNextFreeSpawnPoint` puts everyone else on the first one, on top of whoever is
  there, which is every player from the 5th on when a session starts. `CabinSpawnPointPatches` adds spawn points
  around the game's own the first time each place is used. These places sit among trees and rocks, so each extra spot
  must be on terrain, fairly level, a car's length from the others, and clear of anything solid.

### Why everyone needs the mod

PEAK uses Photon PUN, where raising the room size on the host is enough. Here, the appended words change the size of
replicated state, so every peer has to append exactly the same layout. That's why the slot count is a fixed 16 rather than
following `MaxPlayers`.

A player without the mod can't simply ignore the extra words. When a client sets up an object from the server, Fusion's
`NetworkRunner.InitializeNetworkObjectInstance` asserts that the object's word count matches the client's own, and an
unmodded client's doesn't. So `ModdedLobbyPatches` keeps the two groups apart. Modded players connect to Photon with the
game's AppVersion plus a suffix built from a protocol number and the layout, e.g. `-unlimited.p1.16.1250.354.26`. The
game builds its Photon settings in `OverlandNetworkRunner.BuildCustomAppSetting`, for both the server browser and
starting a session, and a postfix appends the suffix there. Photon only matches players with the same AppVersion, so
unmodded players never see or join modded servers, and modded players don't see vanilla ones. Mod versions whose
layouts differ are kept apart the same way. The protocol number is bumped when the appended state changes meaning
without changing size. With Unlimited mode off, the AppVersion is left alone, so you're matched with vanilla players
again.

### Switching modes

`UnlimitedMode` is a runtime switch, saved as `ExpandPlayerSlots`. Every patch is applied at startup either way, and
every patch that changes behaviour checks the switch, so with it off the game behaves as vanilla. The getters only
return the expanded collections for objects that were actually created bigger.

Networked objects keep the layout they were created with, so the switch only changes outside a session. That's from
when the runner joins the session's Photon room (`NetworkRunner.IsInSession`, before the session's objects spawn) until
those objects are gone. A change to the config made during a session by something outside the mod, such as a config
manager, is undone.

`ServerBrowserTogglePatches` adds the **Unlimited mod servers** checkbox. When the server list starts, it clones the
game's own "Hide private servers" filter. It places the clone after that filter, following the same step as the
game's first two filters (unless a layout group arranges them), and splices it into controller navigation.
`ContextUIServerList.SetFilterChecked` draws the tick. Clicking it flips the switch and re-applies the Max Players options
to any open Create Server menu. It then calls `ContextUIServerList.ServerRegionOnChanged` with the current region, the
same path as picking a region. That reconnects the lobby with the new AppVersion, and the list repopulates. If the
reconnect fails, the switch goes back. The checkbox is disabled until the lobby is connected and the game's region
dropdown is idle. That covers the first connection when the browser opens and a reconnect still in progress, either of
which would otherwise leave a connection open with the old AppVersion.

### Safety

- At startup the mod reads the game's own weaving attributes, the same ones Fusion builds the state from. These are
  each behaviour's `[NetworkBehaviourWeaved]` word count and each expanded property's `[NetworkedWeaved]` range. If a
  game update has added, moved or resized anything the mod relies on, it logs an error and Unlimited mode is
  unavailable. The checkbox reads "Unlimited mod (needs update)", and the whole game stays vanilla (matchmaking, Max
  Players, layout), so it never writes to a layout it doesn't understand. Everyone on that game version falls back the
  same way, so they can still play together, 4 at a time.
- If any patch fails to apply, for example because a game update renamed a method, the mod removes all of its patches
  and logs an error. A half-patched game is worse than a vanilla one.
- With Unlimited mode off, every patch leaves the game's behaviour vanilla.

## Development

### Prerequisites

- .NET SDK 8 or newer. The project targets `net6.0`, the runtime BepInEx 6 ships with.
- Over the Hill with BepInEx installed and launched once, so `BepInEx/interop/` exists.

### Build

```
dotnet build
```

References come straight from the game's `BepInEx/core` and `BepInEx/interop` folders, so the only package is the
.NET reference pack. `nuget.config` adds nuget.org for it. The build copies the DLL and PDB into
`BepInEx/plugins/OverTheHillUnlimited/`. The copy fails with a warning while the game is running, because the DLL is
locked. If the game isn't in the default Steam location, copy `Config.Build.user.props.template` to
`Config.Build.user.props` and set `GameDir`.

### Testing without other players

Launch the game with `-otu-selftest` and it tests itself:
1. It checks that the game's networked layout matches the mod, waits for the main menu, and hosts a session in
   Unlimited mode.
2. It checks that the mode can't be switched mid-session, not even by editing the setting directly.
3. It joins bot players through the game's own join path until the session holds `MaxPlayers` (8 by default, or
   `-otu-selftest-players <n>`).
4. In the lobby, it checks that every car can be seen from the lobby camera, not out of view or hidden behind another
   car. Then everyone spawns at the area's starting cabin the way players do (the host through the map's spawn
   request), and it checks that no two cars land on top of each other.
5. It exercises everything else that indexes per-player data: the player registry, name labels, busy states, map
   pins, portable winch anchors and their HUD markers, meteorite setups, the compass, the car health HUD, and the map,
   pause, car repair and tool selection menus. In the pause menu, it clicks a bot's padded row and checks that their
   player options open.
6. It removes the bots and quits to the main menu.
7. In the server browser, it checks that the **Unlimited mod servers** checkbox is disabled until the lobby connects.
   Then it clicks the checkbox through its real click handler, off and back on, and checks that the lobby reconnects
   each time.
8. It hosts again with Unlimited mode off and checks the session stays vanilla (4 players, nothing expanded, Fusion's
   default player count back to 4).
9. It restores your saved Unlimited mode setting and quits.

It also logs how long the expanded getters take compared with an unpatched one.

Each section also fails if the game or BepInEx logged an error during it. Results go to `BepInEx/LogOutput.log` (source
`OTU Self-test`) and to `BepInEx/OverTheHillUnlimited-selftest.txt`. Add `-otu-selftest-stay` to keep the game open
afterwards, `-otu-selftest-area <name>` to host somewhere other than the first area, or `-otu-selftest-screenshots` to
save screenshots of the lobby, free roam, map, pause menu and server browser to `BepInEx/OverTheHillUnlimited-screenshots/`.

To run it from a shell instead of Steam (Steam must still be running):

```powershell
$env:SteamAppId = '2929250'; $env:SteamGameId = '2929250'
& 'C:\Program Files (x86)\Steam\steamapps\common\over the hill\over the hill.exe' -otu-selftest
```

Bots have no network connection, so the self-test can't prove that the appended state replicates to clients. Test that
with a second machine. Fusion only tracks connected players, so the self-test answers `ActivePlayers` and
`TryGetPlayerObject` for the bots itself (`BotPlayers.cs`), and spawns the bots in the map through the RPC their own
client would send. None of this code runs without the launch option.

### Project layout

```
src/OverTheHillUnlimited/
  Plugin.cs                           entry point, config, patch registration
  UnlimitedMode.cs                    the modded/vanilla switch
  Networking/ExpandedState.cs         appended state layout and collection construction
  Patches/
    CreateServerMenuPatch.cs          Max Players menu options
    SessionSizePatches.cs             Fusion session size
    SessionPlayersCapacityPatches.cs  OverlandSessionPlayers
    MapNetworkCapacityPatches.cs      MapNetworkManager
    CampCapacityPatches.cs            CampManager
    PerPlayerTablePatches.cs          game data with one entry per player
    CabinSpawnPointPatches.cs         room for every car where players spawn in the map
    LobbyCameraPatches.cs             lobby camera that frames every car
    ModdedLobbyPatches.cs             separate Photon AppVersion for modded players
    ServerBrowserTogglePatches.cs     Unlimited mod servers checkbox in the server browser
    PerPlayerUiPatches.cs             UI with one element per player, padded to 16
    PlayerColourPatches.cs            colours for players 5 and up (Windows)
    SessionDiagnosticsPatches.cs      VerboseLogging output
  Testing/
    SelfTest.cs                       the -otu-selftest script and its checks
    SelfTestRunner.cs                 drives the script frame by frame
    BotPlayers.cs                     fake players for the self-test
    LogWatcher.cs                     collects game and BepInEx errors per test section
tools/
  Build-Release.ps1                   build and package a release zip
  Dump-GameCode.ps1                   dump the game's code with Cpp2IL
```

### Releasing

1. Bump `<Version>` in `src/OverTheHillUnlimited/OverTheHillUnlimited.csproj`. The plugin's BepInEx version is generated
   from it.
2. Run `powershell -File tools/Build-Release.ps1`. It builds Release and writes
   `artifacts/OverTheHillUnlimited-<version>.zip`, laid out to extract straight into the game folder.
3. Commit and push, then publish the zip:
   `gh release create v<version> artifacts/OverTheHillUnlimited-<version>.zip --title "v<version>" --notes "..."`

### Reading the game's code

IL2CPP compiles the game to native code, so there's no `Assembly-CSharp.dll` with method bodies to decompile.
`tools/Dump-GameCode.ps1` runs [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) and writes C# stubs to
`.dump/diffable/DiffableCs/`, which is gitignored; don't commit game code. The stubs have no method bodies. They do have
every type and field, Fusion's weaving attributes (`[Capacity]`, `[NetworkedWeaved(offset, words)]`,
`[NetworkBehaviourWeaved(words)]`), and `[Calls]`/`[CalledBy]` cross-references. That's how the patch targets were found.
If the mod logs that the game's layout doesn't match after a game update, re-run it and update the ranges in the
capacity patch classes from the new attributes.

For what a method actually does, run `tools/Dump-GameCode.ps1 -Format isil`. It writes every method's machine code as
ISIL to `.dump/isil/IsilDump/` (about 700 MB), with calls resolved to names. That shows constants, comparisons, the field
offsets a method reads (match them to the `Field offset` comments in the stubs) and bounds checks. A list index check
calls `ThrowHelper.ThrowArgumentOutOfRange_IndexException`. The search for 4-player assumptions scanned it for methods
that read player data and then index a list or array, and for loops with a fixed bound of 4. Almost all of the cap is
in data sized for 4. The one hard-coded loop in player code is the map's pin rects (see How it works). Look for both
loop shapes: a counter compared with 4, and a stride compared with an end offset, such as
`Add r14, r14, 8; Compare r14, 64` for 4 pointers starting at an array's 0x20 data offset.

Logs:
- The mod's output is in `BepInEx/LogOutput.log`.
- Exceptions thrown by game code are in `%USERPROFILE%\AppData\LocalLow\Funselektor\over the hill\Player.log`.
  Under Proton, that's
  `~/.local/share/Steam/steamapps/compatdata/2929250/pfx/drive_c/users/steamuser/AppData/LocalLow/Funselektor/over the hill/Player.log`.

### Gotchas

- The interop assemblies expose private members as public, under their original names. For example,
  `ContextUICreateServer.maxPlayersOptionsRange`.
- Don't patch `NetworkBehaviour.DynamicWordCount`. IL2CPP deduplicated it, so its native code is shared with
  unrelated methods. Patch `NetworkBehaviourUtils.GetWordCount` instead.
- The interop wrapper misreports `Nullable<int>` fields such as `StartGameArgs.PlayerCount` as 0. Check the session's
  real size with `NetworkRunner.SessionInfo.MaxPlayers` instead, as the self-test does.
- `Object.Instantiate` copies serialized UnityEvents, but not C# event handlers the game adds in code. Before padding a
  list of clickable elements, check whether the game wires them up before or after the padding runs.
- Don't patch methods that take a large struct by value, such as `NetworkRunner.StartGame(StartGameArgs)`. On Windows
  (Microsoft x64) the struct arrives as a pointer to a copy. On Linux (System V) it's copied onto the stack, which
  Il2CppInterop's trampolines don't handle, so native Linux crashes with a SIGSEGV. Patch somewhere that holds the struct
  in a field or behind a pointer instead.
- On native Linux, BepInEx's `UnityLogListening` crashed the game at startup for one tester. Setting
  `UnityLogListening = false` in `BepInEx/config/BepInEx.cfg` worked around it.
- Anything that moves a `[Networked]` property's data out of its woven range also hides it from `ChangeDetector`, and from
  any `[OnChangedRender]` callback, which works the same way. See the change-signal note under
  [How it works](#2-fixed-size-networked-state).

## Roadmap

- [x] Make Fusion's change detection notice changes in the appended state (map pins redrew late)
- [x] Release builds
- [x] First 5-player session: a 5th player joined; lobby spawn points and meteorite setups errored, now padded
- [x] Keep players without the mod out (a separate Photon AppVersion), since Fusion rejects the expanded objects on
      their side
- [x] Search the game code for other 4-player assumptions, and pad the UIs that index per-player elements
- [x] Pad the pause menu and map too, so players 5 and up show there
- [x] A self-test that exercises every per-player path with bot players, without needing other people
- [ ] Retest 5+ real players with everyone on the latest version, to cover what bots can't (replication to clients)
- [x] Colours of their own for players 5 and up (Windows)

## Credits

- [PEAK Unlimited](https://github.com/glarmer/PEAK-Unlimited) by glarmer, for the idea and the project structure
- [BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop),
  [HarmonyX](https://github.com/BepInEx/HarmonyX) and [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)

Not affiliated with Funselektor. Please don't report bugs to the game's developers while you're using mods.

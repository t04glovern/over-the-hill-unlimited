# Over the Hill Unlimited

A [BepInEx](https://github.com/BepInEx/BepInEx) mod that raises the 4-player co-op cap in
[Over the Hill](https://store.steampowered.com/app/2929250/), inspired by [PEAK Unlimited](https://github.com/glarmer/PEAK-Unlimited).

> **Status: early and experimental.** A modded host can create a server for more than 4 players, and the game's
> networked player state has room for 16. It's been played with 5 real players; bigger sessions have only been tested
> with bots, by the built-in self-test with 16.

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
| Lobby spawn points and meteorite spawns with 5+ players | Both errored in the first 5-player session; padded in 0.1.1, and work in a later 5-player session |
| Lobby name labels, pause menu, map, car repair and tool selection menus, car health and winch anchor markers, compass | Room for 16 players; passes the [self-test](#testing-without-other-players) with 8 players, untested with real players. Pause menu rows 5+ open their player options (fixed in 0.3.0) |
| Lobby with 5+ cars | Extra rows behind the first, and the camera rises to show every car; verified by the self-test with 16 |
| Spawning in the map with 5+ players | Extra spawn points around each cabin, so cars don't land on top of each other; verified by the self-test with 16 |
| Arriving through a portal with 5+ players | Players past the 4th used to arrive with no car; now everyone gets a spot (self-test with 16) |
| Winching with 5+ cars (one car, a meteorite, a recovery vehicle) | Room for 16 winches on each (self-test with 6 winches) |
| Meteorites with 5+ players | Each involved player gets the 4-player share of the pebbles, more meteorites in the map, and a wider reach for counting as involved (self-test with 16) |
| Tents at camp with 8+ players | The tent circle widens so tents don't overlap (self-test with 16) |
| Gathering the party with 5+ players (the boat, cabin camping, the camp tool) | The party counts once it's gathered as one group; the self-test parks 15 cars at the Valley's boat, 3 cabins and a camp, and checks it counts them, and stops once one drives off |
| Scenic points, crates, cabins and merchants with 5+ players | Their zones grow with the number of players (twice as wide for 16; self-test with 16) |
| Discord voice chat with 5+ players | An emitter for every other player (self-test of the slots; audio needs a real call) |
| Game updates | The mod checks the game's networked layout at startup and stays vanilla if it changed |
| Colours for players 5+ | Their own colours on Windows (self-test); the game's default colour on native Linux |
| MelonLoader 0.7.3 | The same mod, built from the same source: passes the self-test with 16 players and uses the same modded servers as the BepInEx version. Playing across loaders hasn't been tested with real players yet |
| Native Linux (not Proton) | Joining crashed in 0.1.0; fixed in 0.1.1, untested |
| Players without the mod | Kept out: modded and unmodded players don't see each other's servers (verified: modded lobby lists 0 vanilla servers) |
| Switching between modded and vanilla in game | Verified: the "Unlimited mod servers" checkbox in the server browser switches the list and Max Players |

## Installation

Everyone in the session needs to do this, not just the host, and everyone needs the same mod version.

The mod comes for both of the game's mod loaders, [BepInEx](https://github.com/BepInEx/BepInEx) 6 and
[MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7. Use the one your other mods use: most mods for Over the Hill
use MelonLoader. Don't install both loaders. The two versions are the same mod, built from the same code, and use the
same modded servers.

The server browser gets an **Unlimited mod servers** checkbox next to its filters. While it's ticked, you only see and
host modded servers, with up to 16 players. Untick it to see vanilla servers and play with friends who don't have the
mod (4 players max). Your choice is remembered.

### Windows, with BepInEx

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

### Windows, with MelonLoader

1. **Install MelonLoader 0.7.3.** Run the
   [MelonLoader installer](https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3) and pick Over the Hill, or
   extract `MelonLoader.x64.zip` from the same page into the game folder, so that `version.dll` sits next to
   `over the hill.exe`.
2. **Run the game once**, wait for the main menu, then quit. The first launch takes a few minutes longer than usual.
3. **Install the mod.** Download `OverTheHillUnlimited-<version>-MelonLoader.zip` from
   [Releases](https://github.com/t04glovern/over-the-hill-unlimited/releases) and extract it into the game folder.
   You should end up with `Mods\OverTheHillUnlimited.MelonLoader.dll`. If you had the community MelonLoader port of
   0.4.1, this replaces it: it's the same file name.
4. **Play.** The host picks a Max Players above 4 under Create Server.

To uninstall, delete `Mods\OverTheHillUnlimited.MelonLoader.dll`.

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

With MelonLoader instead, extract `MelonLoader.x64.zip` (step 3), set Launch Options to
`WINEDLLOVERRIDES="version=n,b" %command%` (step 4), and install the MelonLoader zip of the mod (step 6).

To uninstall, clear the launch option, then untick the compatibility option to go back to the native version.

## Configuration

The settings file is created on first launch: `BepInEx/config/OverTheHillUnlimited.cfg` with BepInEx, or
`UserData/MelonPreferences.cfg` with MelonLoader. MelonLoader keeps them all under one `[OverTheHillUnlimited]`
section, without the `General.`, `Meteorites.`, `Areas.` and `Debug.` prefixes below.

| Setting | Default | Description |
|---|---|---|
| `General.MaxPlayers` | `8` | Highest option in the Max Players menu in Unlimited mode, 4 to 16. The vanilla 2 to 4 are always offered. |
| `General.ExpandPlayerSlots` | `true` | Unlimited mode, the same setting as the **Unlimited mod servers** checkbox in the server browser. Off means vanilla behaviour, for playing with people who don't have the mod (4 players max). |
| `Meteorites.PebbleShareMaxPlayers` | `4` | A meteorite's pebbles are split between the players involved. With more involved than this, each gets the share for this many instead. `16` splits them between everyone, as the game does. |
| `Meteorites.PlayersPerExtraMeteorite` | `2` | With more than 4 players, one more meteorite in the map for every this many players past 4. `0` keeps the game's 2 or 3. |
| `Meteorites.ScaleRewardReach` | `true` | With more than 4 players, widen the 10 m distance for counting as involved in pulling a meteorite out (14 m for 8, 20 m for 16). |
| `Areas.GatherAsGroup` | `true` | With more than 4 players, the party counts as at a portal (such as the boat), in a cabin's camping area, or close enough to camp, once it's gathered as one group: every car within 15 m of another, and at least one of them in the area. |
| `Areas.ScaleReachZones` | `true` | With more than 4 players, the zones that count you as reaching a scenic point, crate, cabin or merchant grow with the number of cars (1.4 times as wide for 8, twice for 16), along with their markers and a meteorite's reach ring. |
| `Debug.VerboseLogging` | `false` | Log the session size and player list as players join and leave. Useful for testing. |

The meteorite settings are the host's: the host decides the rewards and spawns, so other players' settings don't
matter. See [Meteorites for more than 4 players](#5-meteorites-for-more-than-4-players). The area settings work on
each player's own game, so leave them the same for everyone. See [Room for everyone](#6-room-for-everyone).

## How it works

Over the Hill is a Unity 6 IL2CPP game that uses Photon Fusion 2 in host mode. Two separate things cap it at 4 players,
and fixing the second one exposes a third.

### 1. Session size

```
Create Server menu ──► GameSetupContext.MaxPlayers ──X  game caps it at 4  ──► StartGameArgs.PlayerCount ──► Fusion room
 options "2" "3" "4"                                                                                                 max 4
```

- `CreateServerMenuPatch` appends `5..MaxPlayers` to the menu's options. The game parses the chosen option into
  `GameSetupContext.MaxPlayers` itself.
- When the game starts Fusion, it passes `StartGameArgs.PlayerCount = Math.Min(MaxPlayers, 4)`, so any choice above 4
  becomes 4. `SessionSizePatches` remembers the menu value in `OverlandNetworkRunner.StartGame`.
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
| `VehicleWinchSystem` (every car) | the players whose winches are attached to the car | 25 | 41 |

The mod leaves the vanilla words alone. It appends a 16-slot copy of each collection after them:

```
OverlandSessionPlayers state block (replicated by Fusion)
┌───────────────────────────────┬──────────────────────────────────────────────────┐
│ vanilla: 390 words            │ appended by the mod: 860 words                   │
│ 4-slot collections (unused)   │ 16-slot player order, colours, names, flags ...  │
└───────────────────────────────┴──────────────────────────────────────────────────┘
```

- Fusion asks `NetworkBehaviourUtils.GetWordCount(behaviour)` how big each block should be. A postfix adds the extra
  words for these behaviours.
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
  their change detectors fire too. `CampManager` doesn't use a `ChangeDetector`, and `VehicleWinchSystem`'s only
  watches other properties.
- A car's `AttachedWinchesOwners` holds its own winch plus every other player winched to it. With 4 slots, a 5th was
  silently dropped, and `DetachAllWinches` (when the car teleports, respawns, leaves or opens the merchant) didn't
  release that player's rope.

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

Some game data and world state has one entry or one place per player, sized for 4. With 5 players the code reads past
the end, puts cars on top of each other, or drops players. Each gets room for 16:

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
- Portals have 4 spawn points too, and arriving through one is worse: `Portal.AssignPlayersToSpawnPoints` gives each
  player the spawn point at their position in `ActivePlayers` and skips anyone past the end, who then has no car and
  can't respawn. `CabinSpawnPointPatches` pads portals before that, and fills any slots the search couldn't with rows
  behind the portal's own points.
- `Meteorite.winchedPlayersPullDirections` is made as a `Vector2[4]`, but extraction writes a pull direction for every
  car winched to the meteorite, so a 5th winch threw on the host every tick and the meteorite never came out. It's
  grown before each winch is added.
- Tents go on one circle around the campfire, one per car, at a fixed distance that fits 7. `CampTentPatches` widens
  the circle for bigger parties in `StateCampTool.ShowTentDecalProjectors`, which the tents' placement reads too.
- An item recovery mission's networked status has 4 slots for the cars winched to the vehicle being recovered: a 5th
  winch threw on the host, letting go threw again, and completing the mission left it attached.
  `ItemRecoveryWinchPatches` keeps cars past the 4th in a host-side list, moves one into a slot whenever a recorded car
  lets go, and detaches them all on completion.
- In-game voice chat (through Discord) has emitters for 3 other players. The 4th and later were silent and logged an
  exception for every audio frame. `VoiceChatCapacityPatches` adds emitters before a call starts.

### 5. Meteorites for more than 4 players

The game balances meteorites for up to 4 players, and past that they stop being worth it. `MeteoriteBalancePatches`
carries the 4-player balance on. The host decides all three, so only the host needs this, and with the default
[settings](#configuration) nothing changes with 4 players or fewer:

- **Pebbles.** When a meteorite comes out, `Meteorite.CompleteObjectiveForInvolvedPlayers` tells every player through
  `RPC_CompleteObjectiveForLocalPlayer`: each involved player (every car winched to it, and every car within
  `prizeAwardMaxDistance`) gets `CeilToInt(PebbleValue / involved players)`, and everyone else 0. With more than 4
  involved (`PebbleShareMaxPlayers`), each involved player gets the 4-player share instead, so a very huge meteorite
  (80 pebbles) gives each of them 20, as in a 4-player game, not 10 with 8 players or 5 with 16. The RPCs are held back
  until the method is done and the number of players involved is known, then sent with that share.
- **How many.** `MeteoriteSpawnController` keeps 2 or 3 meteorites in the map (`meteoritesToSpawn`, rolled once by the
  host) whatever the number of players. One more is added for every 2 players past 4 (`PlayersPerExtraMeteorite`:
  3 or 4 for 6 players, 4 or 5 for 8, 8 or 9 for 16), so there are as many per player as in a 4-player game. There
  are plenty of spawn points (116 across Canada's 3 areas, 96 across Algeria's 2). Extra ones at the start are already
  landed like the rest; ones added when players join fall from the sky at the next check. With fewer players, the
  extra ones aren't replaced.
- **Reach.** Only cars within `prizeAwardMaxDistance` (10 m, about two car lengths) of the meteorite when it comes out
  count as involved, unless they're winched to it. That grows with the square root of the number of players past 4
  (14 m for 8, 20 m for 16; `ScaleRewardReach`), so the area around the meteorite grows with the number of cars.
  The ring the meteorite shows on the ground for its reach (`MeteoriteVisuals.prizeAwardMaxDistance`) grows with it.

### 6. Room for everyone

Some places need the whole party together, and the game sizes them for about 4 cars:
- **Travelling to another area** needs every car in the portal's trigger, a sphere 10 to 12 m in radius
  (`Portal.AllPlayersInArea`: your game checks it to show "Proceed through the portal" and let you press it, and the
  host's checks it again before sending everyone). The Valley's portal is a boat at the end of a one-car-wide dock:
  the deck and the end of the dock hold 4 cars, so the rest queue down the dock and along the shore.
- **Camping at a cabin** needs every car in its 40 by 40 m camping area (`CabinCampTrigger.CheckPartyIsInCampingArea`).
- **Camping anywhere else** needs every car within 15 m of whoever places the campfire (`CampManager.CheckPartyIsCloseBy`,
  `CampingConfig.maxPartyMembersDistance`).

No circle fits 16 cars on a dock or a summit, so `PartyGatheringPatches` adds a second way to count: with more than 4
players, the party is there once it's gathered as one group. Every car has to be within 15 m (about two car lengths
apart) of another, and at least one of them in the game's own area. The areas keep the game's size, and the game's
own answer still counts. A postfix on each check works this out from the cars' positions, which every player's game
knows. A car joining the group outside the portal's trigger doesn't refresh its prompt the way a car entering it
does, so while the prompt is up it's refreshed whenever the answer changes.

The places each player reaches on their own (scenic points, crates, cabins and merchants) count a car once it's in
their trigger, mostly 8 m in radius or 8 m across. `ReachZonePatches` widens each by √(players / 4) with more than 4
players, the same as the meteorite reach, so their area grows with the number of cars. The marker the game draws on
the ground for a zone keeps its proportion to the zone. Every player's game sizes its triggers from the session's
player count, which they all see the same. Summits and ends of tracks still can't fit 16 cars at once, but each
player only needs to reach them once.

### Why everyone needs the mod

PEAK uses Photon PUN, where raising the room size on the host is enough. Here, the appended words change the size of
replicated state, so every peer has to append exactly the same layout. That's why the slot count is a fixed 16 rather than
following `MaxPlayers`.

A player without the mod can't simply ignore the extra words. When a client sets up an object from the server, Fusion's
`NetworkRunner.InitializeNetworkObjectInstance` asserts that the object's word count matches the client's own, and an
unmodded client's doesn't. So `ModdedLobbyPatches` keeps the two groups apart. Modded players connect to Photon with the
game's AppVersion plus a suffix built from a protocol number and the layout, e.g. `-unlimited.p1.16.1250.354.26.41`. The
game builds its Photon settings in `OverlandNetworkRunner.BuildCustomAppSetting`, for both the server browser and
starting a session, and a postfix appends the suffix there. Photon only matches players with the same AppVersion, so
unmodded players never see or join modded servers, and modded players don't see vanilla ones. Mod versions whose
layouts differ are kept apart the same way. The protocol number is bumped when the appended state changes meaning
without changing size. With Unlimited mode off, the AppVersion is left alone, so you're matched with vanilla players
again.

Since the game's 17 October update (build 25992246), the server list also hides every server for more than 4 players:
`OverlandSessionInfo` sets `VisibleInList` from the session's Max Players, and `SessionInfoListAdapter.ApplyFilters`
skips the ones it leaves false. In Unlimited mode the lobby only holds modded servers, so a prefix on `ApplyFilters`
sets it again for servers of up to 16 players. Il2CppInterop can't patch the constructor that sets it, so it's done
just before the filters read it. In vanilla mode the game's rule stands.

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

- .NET SDK 8 or newer. The projects target `net6.0`, the runtime BepInEx 6 and MelonLoader 0.7 both ship with.
- Over the Hill with BepInEx installed and launched once, so `BepInEx/interop/` exists.
- For the MelonLoader build, a game folder with MelonLoader 0.7.3 installed and launched once, so
  `MelonLoader/Il2CppAssemblies/` exists. A copy of the game folder keeps the two loaders apart.

### Build

```
dotnet build
dotnet build src/OverTheHillUnlimited.MelonLoader
```

The first builds the BepInEx version: the solution leaves the MelonLoader project out of its build, so it works without
MelonLoader installed. The second builds the MelonLoader version. There are two projects, one per loader, built from
the same source:
- `src/OverTheHillUnlimited` is the BepInEx build. Its references come straight from the game's `BepInEx/core` and
  `BepInEx/interop` folders, and it copies the DLL and PDB into `BepInEx/plugins/OverTheHillUnlimited/`.
- `src/OverTheHillUnlimited.MelonLoader` is the MelonLoader build. It compiles the BepInEx project's source, except
  `Loader/BepInEx/`, against `MelonLoader/net6` and `MelonLoader/Il2CppAssemblies`, and copies
  `OverTheHillUnlimited.MelonLoader.dll` and its PDB into `Mods/`.

The only package is the .NET reference pack; `nuget.config` adds nuget.org for it. The copies fail with a warning while
the game is running, because the DLL is locked. If the game isn't in the default Steam location, or MelonLoader is in a
different copy of it, copy `Config.Build.user.props.template` to `Config.Build.user.props` and set `GameDir` and
`MelonLoaderGameDir`.

The two loaders differ in only a few ways, and those live in `Plugin`'s loader half: how the mod starts, where its
settings are stored, how it logs, and how the self-test is added. The rest is shared, apart from one naming difference.
MelonLoader's interop assemblies prefix the game's namespaces with `Il2Cpp` (`Il2Cpp` for the global namespace,
`Il2CppFusion`, `Il2CppFunCore`), so the shared source imports the game's namespaces through each project's global
usings, not `using` lines in the files.

### Testing without other players

Launch the game with `-otu-selftest` and it tests itself:
1. It checks that the game's networked layout matches the mod, waits for the main menu, and hosts a session in
   Unlimited mode.
2. It checks that the mode can't be switched mid-session, not even by editing the setting directly.
3. It joins bot players through the game's own join path until the session holds `MaxPlayers` (8 by default, or
   `-otu-selftest-players <n>`).
4. In the lobby, it checks that every car can be seen from the lobby camera, not out of view or hidden behind another
   car. Then everyone spawns at the area's starting cabin the way players do (the host through the map's spawn
   request), and it checks that the cabin has a spawn point for every player, a car's length apart. Where the cars
   end up after landing depends on physics, so that's only logged.
5. It exercises everything else that indexes per-player data: the player registry, name labels, busy states, map
   pins, portable winch anchors and their HUD markers, meteorite setups, the compass, the car health HUD, and the map,
   pause, car repair and tool selection menus. In the pause menu, it clicks a bot's padded row and checks that their
   player options open, that the rows grey out like the game's own, and that a gamepad can move down through them.
6. It has 6 cars winch one car, a meteorite and the area's recovery vehicle, and checks that every winch is tracked,
   extraction starts, and every rope is released. For the meteorite, it checks the map has the right number of
   meteorites for the session, and that when it comes out every winched car is rewarded with the 4-player share and
   the reach is widened for the number of players (the host's own profile is left out). It lays out a tent per
   player around a campfire and checks none overlap, and fills voice chat with 15 other participants.
   It parks the bots, as close as they fit, at each of the area's portals, the camping areas of 3 cabins, and around
   a camper, and checks the game's own party check counts them as one group, then that it doesn't once one drives off.
   It parks them around 2 scenic points, 2 crates, 2 cabins and every merchant, and logs how many fit in the zone at
   the game's size and the mod's. Cars park on open, level ground clear of buildings, rocks, trees and water, and the
   host's car stays where it spawned, so the real profile never reaches anything. `-otu-selftest-all-zones` tests
   every cabin, scenic point and crate. Finally it sends everyone through a portal and checks every player arrives
   with a car, on spawn points a car's length apart.
7. It removes the bots and quits to the main menu.
8. In the server browser, it checks that the **Unlimited mod servers** checkbox is disabled until the lobby connects,
   and that other players' Unlimited servers for more than 4 players aren't hidden (a real check only while some are
   up in your region). Then it clicks the checkbox through its real click handler, off and back on, and checks that
   the lobby reconnects each time.
9. It hosts again with Unlimited mode off and checks the session stays vanilla (4 players, nothing expanded, Fusion's
   default player count back to 4).
10. It restores your saved Unlimited mode setting and quits.

It also logs how long the expanded getters take compared with an unpatched one.

Each section also fails if the game or the mod loader logged an error during it. Results go to the loader's log (source
`OTU Self-test`, which MelonLoader shows as `OTU_Self-test`) and to `OverTheHillUnlimited-selftest.txt`: `BepInEx/LogOutput.log` and `BepInEx/` with BepInEx,
`MelonLoader/Latest.log` and `UserData/` with MelonLoader. It runs the same on both. Add `-otu-selftest-stay` to keep the
game open afterwards, `-otu-selftest-area <name>` to host somewhere other than the first area,
`-otu-selftest-areas-only` to run only the parking checks above, or `-otu-selftest-screenshots` to save screenshots of
the lobby, free roam, map, pause menu and server browser, and each parked group from above, to
`OverTheHillUnlimited-screenshots/` next to the results.

The self-test hosts with your own profile, so it changes your save the way a session would: the time you last played,
and the fog of war wherever your car goes. Hosting an area you haven't reached yet may also mark things there as
found. Back up `Documents/My Games/over the hill/Saves` before testing other areas.

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
src/
  OverTheHillUnlimited.props          shared by both projects: version, build settings
  OverTheHillUnlimited.MelonLoader/
    OverTheHillUnlimited.MelonLoader.csproj  the MelonLoader build of the source below
    Plugin.MelonLoader.cs             MelonLoader's half of Plugin
src/OverTheHillUnlimited/             the BepInEx build, and the source both builds share
  Plugin.cs                           startup, settings, patch registration (shared half)
  Loader/
    BepInEx/Plugin.BepInEx.cs         BepInEx's half of Plugin
    ModLog.cs, Setting.cs             a log and a setting on either loader
  UnlimitedMode.cs                    the modded/vanilla switch
  Networking/ExpandedState.cs         appended state layout and collection construction
  Patches/
    CreateServerMenuPatch.cs          Max Players menu options
    SessionSizePatches.cs             Fusion session size
    SessionPlayersCapacityPatches.cs  OverlandSessionPlayers
    MapNetworkCapacityPatches.cs      MapNetworkManager
    CampCapacityPatches.cs            CampManager
    WinchCapacityPatches.cs           VehicleWinchSystem (winches attached to each car)
    PerPlayerTablePatches.cs          game data with one entry per player
    CabinSpawnPointPatches.cs         room for every car where players spawn or arrive in the map
    LobbyCameraPatches.cs             lobby camera that frames every car
    CampTentPatches.cs                room around the campfire for every tent
    ItemRecoveryWinchPatches.cs       more than 4 winches on a recovery vehicle
    VoiceChatCapacityPatches.cs       voice chat emitters for every player
    MeteoriteBalancePatches.cs        pebble shares, meteorite count and reach for more than 4 players
    PartyGatheringPatches.cs          the party counts as gathered as one group (portals, cabin camping, camp tool)
    ReachZonePatches.cs               wider scenic point, crate, cabin and merchant zones, and meteorite ring
    ModdedLobbyPatches.cs             separate Photon AppVersion for modded players
    ServerBrowserTogglePatches.cs     Unlimited mod servers checkbox in the server browser
    PerPlayerUiPatches.cs             UI with one element per player, padded to 16
    PlayerColourPatches.cs            colours for players 5 and up (Windows)
    SessionDiagnosticsPatches.cs      VerboseLogging output
  Testing/
    SelfTest.cs                       the -otu-selftest script and its checks
    SelfTest.World.cs                 its checks for winches, tents, voice chat and portals
    SelfTest.Areas.cs                 its checks that park the party at portals, cabins, camps and zones
    SelfTestRunner.cs                 drives the script frame by frame
    FourSlotProbe.cs                  -otu-selftest-probe: lists every list or array still holding 4
    BotPlayers.cs                     fake players for the self-test
    LogWatcher.cs                     collects game and mod loader errors per test section
tools/
  Build-Release.ps1                   build and package the release zips
  Dump-GameCode.ps1                   dump the game's code with Cpp2IL
```

### Releasing

1. Bump `<Version>` in `src/OverTheHillUnlimited.props`. Both loaders' version attributes are generated from it.
2. Run `powershell -File tools/Build-Release.ps1` (add `-MelonLoaderGameDir <folder>` if MelonLoader is in a copy of
   the game). It builds both in Release and writes `artifacts/OverTheHillUnlimited-<version>.zip` (BepInEx) and
   `artifacts/OverTheHillUnlimited-<version>-MelonLoader.zip`, each laid out to extract straight into the game folder.
3. Commit and push, then publish both zips:
   `gh release create v<version> artifacts/OverTheHillUnlimited-<version>*.zip --title "v<version>" --notes "..."`

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
- The mod's output is in `BepInEx/LogOutput.log`, or `MelonLoader/Latest.log` with MelonLoader.
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
- [x] Retest 5+ real players with everyone on the latest version, to cover what bots can't (replication to clients)
- [x] Colours of their own for players 5 and up (Windows)
- [x] A MelonLoader build from the same source, so players can use it alongside the game's other MelonLoader mods
- [x] Room for the whole party where everyone has to gather (the boat, cabins, camps), and wider zones for scenic
      points, crates, cabins and merchants
- [ ] Play across loaders with real players (BepInEx and MelonLoader players in one session)
- [ ] Take 5+ real players through the boat and a cabin camp

## Credits

- [PEAK Unlimited](https://github.com/glarmer/PEAK-Unlimited) by glarmer, for the idea and the project structure
- [BepInEx](https://github.com/BepInEx/BepInEx), [MelonLoader](https://github.com/LavaGang/MelonLoader),
  [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop), [HarmonyX](https://github.com/BepInEx/HarmonyX) and
  [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)
- [livixis](https://www.nexusmods.com/profile/livixis) on Nexus Mods, for the idea of supporting MelonLoader
- The community MelonLoader port of 0.4.1 on Nexus Mods, which showed players wanted a MelonLoader version

## License

[MIT](LICENSE). Not affiliated with Funselektor. Please don't report bugs to the game's developers while you're using
mods.

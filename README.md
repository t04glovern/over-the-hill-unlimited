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
| Networked player list (order, colours, names) holds 16 players | Verified solo |
| Map pins, portable winch anchors and camp tents hold 16 players | Pins verified solo; anchors and tents untested |
| Map and player list UI refresh when the expanded data changes | Verified solo for map pins |
| Pause menu, map, compass and car health UI | Working, but they only show the first 4 players |
| Two modded players in one session | Untested |
| A 5th player joining and playing | Untested |

## Installation

Everyone in the session needs to do this, not just the host.

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

The game has a native Linux build. Use the Linux IL2CPP version of BepInEx for that build, or the Windows version if
you choose to run the Windows game through Proton. On a Steam Deck, make these changes in Desktop Mode.

#### Native Linux client

Startup was verified on native Linux x64 with Unity `6000.0.75f1`, BepInEx build `788` and mod `0.1.0`: the game reached
the main menu and the BepInEx log confirmed the mod was enabled with `MaxPlayers=8`. Hosting and multiplayer were not
tested in this native Linux check. Steam Deck has not been tested.

1. **Open your game folder.** In Steam, right-click Over the Hill, then Manage, then Browse local files.
   It's usually `~/.local/share/Steam/steamapps/common/over the hill`. The native build contains
   `over the hill.x86_64` and `GameAssembly.so`.
2. **Install the Linux loader.** Download
   [BepInEx-Unity.IL2CPP-linux-x64-6.0.0-be.788+5b766a3.zip](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-linux-x64-6.0.0-be.788%2B5b766a3.zip)
   and extract it into the game folder, so that `run_bepinex.sh` and `libdoorstop.so` sit next to
   `over the hill.x86_64`, alongside the `BepInEx/` and `dotnet/` folders. Use the **IL2CPP** build, not Mono or BepInEx 5.
3. **Make the launcher executable.** Open a terminal in the game folder and run:

   ```sh
   chmod +x run_bepinex.sh
   ```

4. **Disable BepInEx's Unity log capture before launching.** Create `BepInEx/config/` if needed, then create
   `BepInEx/config/BepInEx.cfg` with:

   ```ini
   [Logging]
   UnityLogListening = false
   ```

   If that file already exists, change `UnityLogListening` under `[Logging]` instead of replacing the file.
   With Unity log capture enabled, the tested native setup crashed during startup, even with the mod's gameplay
   patches disabled. This setting resolved the crash. BepInEx still writes the mod's logs, and the game's own
   `Player.log` remains available.
5. **Set the Steam launch option.** In Steam, right-click Over the Hill, then Properties. Under General, set
   Launch Options to:

   ```sh
   ./run_bepinex.sh %command%
   ```

6. **Run the game once**, wait for the main menu, then quit. The first launch takes longer while BepInEx generates
   its interop assemblies.
7. **Install the mod.** Download `OverTheHillUnlimited-<version>.zip` from
   [Releases](https://github.com/t04glovern/over-the-hill-unlimited/releases) and extract it into the game folder.
   You should end up with `BepInEx/plugins/OverTheHillUnlimited/OverTheHillUnlimited.dll`.
8. **Launch through Steam.** Check `BepInEx/LogOutput.log` for `Over the Hill Unlimited` and `ExpandPlayerSlots=True`
   to confirm the mod loaded. The host can then pick a Max Players above 4 under Create Server.

To disable the native loader, clear the launch option above, or remove `./run_bepinex.sh` if you have other launch options.

#### Windows client through Proton

These Proton steps have not been tested. To use this alternative, select a Proton version under Steam's
Properties, Compatibility, then let Steam install the Windows build. Confirm the game folder contains
`over the hill.exe` before installing the Windows loader.

1. **Open your game folder.** In Steam, right-click Over the Hill, then Manage, then Browse local files.
   It's usually `~/.local/share/Steam/steamapps/common/over the hill`.
2. **Install BepInEx.** Download the
   [same BepInEx zip](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip)
   and extract it into the game folder, so that `winhttp.dll` sits next to `over the hill.exe`, like the screenshot above.
3. **Tell Proton to load BepInEx.** In Steam, right-click Over the Hill, then Properties. Under General, set
   Launch Options to:

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

4. **Run the game once**, wait for the main menu, then quit. The first launch takes a few minutes longer than usual.
5. **Install the mod.** Download `OverTheHillUnlimited-<version>.zip` from
   [Releases](https://github.com/t04glovern/over-the-hill-unlimited/releases) and extract it into the game folder.
   You should end up with `BepInEx/plugins/OverTheHillUnlimited/OverTheHillUnlimited.dll`.
6. **Play.** The host picks a Max Players above 4 under Create Server.

To uninstall, clear the launch option.

## Configuration

`BepInEx/config/OverTheHillUnlimited.cfg` is created on first launch.

| Setting | Default | Description |
|---|---|---|
| `General.MaxPlayers` | `8` | Highest option in the Max Players menu, 1 to 16. |
| `General.ExpandPlayerSlots` | `true` | Master switch. Turn it off to play vanilla with people who don't have the mod. |
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
  It then writes that value into `StartGameArgs.PlayerCount` in a prefix on Fusion's `NetworkRunner.StartGame`.
- It also raises Fusion's default player count, `NetworkProjectConfig.Simulation.PlayerCount` (shipped as 4), to 16
  on every modded peer.

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
vanilla versions live.

### 3. UI built for 4 players

Some UIs keep one element per player but loop over the whole player list. With 16 slots, the pause menu and the map threw
`ArgumentOutOfRangeException`, which broke pause menu navigation. `PlayerListUiPatches` makes the player list report only
its first 4 slots while those UI methods run. The compass and car health UI get the same treatment as a precaution. They
all work as before, but players 5 and up don't appear in them yet.

### Why everyone needs the mod

PEAK uses Photon PUN, where raising the room size on the host is enough. Here, the appended words change the size of
replicated state, so every peer has to append exactly the same layout. That's why the slot count is a fixed 16 rather than
following `MaxPlayers`. A player without the mod, or with `ExpandPlayerSlots` off, is not expected to work in a modded
host's session.

### Safety

- The mod checks each behaviour's vanilla word count at runtime. If a game update changes it, the mod logs an error and
  leaves that behaviour at 4 slots, rather than writing to a layout it doesn't understand.
- `ExpandPlayerSlots = false` turns every patch off.

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

### Project layout

```
src/OverTheHillUnlimited/
  Plugin.cs                           entry point, config, patch registration
  Networking/ExpandedState.cs         appended state layout and collection construction
  Patches/
    CreateServerMenuPatch.cs          Max Players menu options
    SessionSizePatches.cs             Fusion session size
    SessionPlayersCapacityPatches.cs  OverlandSessionPlayers
    MapNetworkCapacityPatches.cs      MapNetworkManager
    CampCapacityPatches.cs            CampManager
    PlayerListUiPatches.cs            UIs built for 4 players
    SessionDiagnosticsPatches.cs      VerboseLogging output
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
Re-run it after game updates, and check that the vanilla word counts in the patch classes still match.

Logs:
- The mod's output is in `BepInEx/LogOutput.log`.
- Exceptions thrown by game code are in `%USERPROFILE%\AppData\LocalLow\Funselektor\over the hill\Player.log`.
  Under Proton, that's
  `~/.local/share/Steam/steamapps/compatdata/2929250/pfx/drive_c/users/steamuser/AppData/LocalLow/Funselektor/over the hill/Player.log`.
- On native Linux, the game's log is `~/.config/unity3d/Funselektor/over the hill/Player.log`.

### Gotchas

- The interop assemblies expose private members as public, under their original names. For example,
  `ContextUICreateServer.maxPlayersOptionsRange`.
- Don't patch `NetworkBehaviour.DynamicWordCount`. IL2CPP deduplicated it, so its native code is shared with
  unrelated methods. Patch `NetworkBehaviourUtils.GetWordCount` instead.
- The interop wrapper misreports `StartGameArgs.PlayerCount` (a `Nullable<int>`) as 0, so `SessionSizePatches` reads
  the raw struct.
- Anything that moves a `[Networked]` property's data out of its woven range also hides it from `ChangeDetector`, and from
  any `[OnChangedRender]` callback, which works the same way. See the change-signal note under
  [How it works](#2-fixed-size-networked-state).

## Roadmap

- [x] Make Fusion's change detection notice changes in the appended state (map pins redrew late)
- [ ] Test with 2 modded players to confirm the appended state syncs
- [ ] Test with 5 or more players and fix what breaks. Likely candidates are the game's own join check
      (`OverlandNetworkRunner.OnConnectRequest`), lobby spawn points (`Lobby.spawnPoints`) and player colours
      (`UIManager.GetPlayerColor`).
- [ ] Give the 4-slot UIs more elements so players 5 and up show up
- [x] Release builds

## Credits

- [PEAK Unlimited](https://github.com/glarmer/PEAK-Unlimited) by glarmer, for the idea and the project structure
- [BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop),
  [HarmonyX](https://github.com/BepInEx/HarmonyX) and [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)

Not affiliated with Funselektor. Please don't report bugs to the game's developers while you're using mods.

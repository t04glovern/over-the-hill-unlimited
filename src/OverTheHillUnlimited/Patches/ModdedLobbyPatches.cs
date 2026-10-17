using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Keeps modded and unmodded players apart. Unmodded clients can't use the expanded networked objects: when a client
/// sets up an object from the server, Fusion's InitializeNetworkObjectInstance asserts that its word count matches the
/// client's own. So modded players connect to Photon with a different AppVersion, and Photon only matches players
/// with the same AppVersion: unmodded players never see or join modded servers, and modded players don't see vanilla
/// ones. With Unlimited mode off the AppVersion is left alone, so you're back with vanilla players.
/// </summary>
public class ModdedLobbyPatches
{
    // Bump when the appended state changes meaning without changing size, such as how changes are signalled
    const int Protocol = 1;

    // Built from the protocol and the expanded layout, so mod versions that can't play together are kept apart too
    static readonly string AppVersionSuffix =
        $"-unlimited.p{Protocol}.{ExpandedState.Capacity}" +
        $".{SessionPlayersCapacityPatches.State.WordCount}" +
        $".{MapNetworkCapacityPatches.State.WordCount}" +
        $".{CampCapacityPatches.State.WordCount}" +
        $".{WinchCapacityPatches.State.WordCount}";

    // The game builds fresh app settings here both for the server browser (JoinLobby) and for starting or joining
    // a session (StartGameAsync)
    [HarmonyPatch(typeof(OverlandNetworkRunner), nameof(OverlandNetworkRunner.BuildCustomAppSetting))]
    [HarmonyPostfix]
    static void Postfix(FusionAppSettings __result)
    {
        if (!UnlimitedMode.Enabled || __result == null || (__result.AppVersion?.EndsWith(AppVersionSuffix) ?? false))
            return;

        __result.AppVersion += AppVersionSuffix;
        if (!_logged)
            Plugin.Logger.LogInfo($"Using Photon AppVersion '{__result.AppVersion}' in Unlimited mode, so only " +
                                  "players with this mod version see each other's servers");
        _logged = true;
    }

    static bool _logged;

    // Since build 25992246 the game leaves servers for more than 4 players out of the server list: each session's
    // VisibleInList is set from its MaxPlayers, and the list's filters skip the rest. In Unlimited mode the lobby only
    // holds modded servers, so those are listed again; in vanilla mode the game's rule stands. Set here, just before the
    // filters read it, because Il2CppInterop can't patch the constructor that sets it.
    [HarmonyPatch(typeof(SessionInfoListAdapter), nameof(SessionInfoListAdapter.ApplyFilters))]
    [HarmonyPrefix]
    static void ListUnlimitedServers()
    {
        var sessions = UnlimitedMode.Enabled ? OverlandNetworkRunner.Instance?.SessionList : null;
        if (sessions == null)
            return;
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            if (!session.VisibleInList && session.FusionSessionInfo?.MaxPlayers <= ExpandedState.Capacity)
                session.VisibleInList = true;
        }
    }
}

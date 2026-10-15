using Fusion.Photon.Realtime;
using HarmonyLib;
using OverTheHillUnlimited.Networking;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Keeps modded and unmodded players apart. Unmodded clients can't use the expanded networked objects: when a client
/// sets up an object from the server, Fusion's InitializeNetworkObjectInstance asserts that its word count matches the
/// client's own. So modded players connect to Photon with a different AppVersion, and Photon only matches players
/// with the same AppVersion: unmodded players never see or join modded servers, and modded players don't see vanilla
/// ones. With ExpandPlayerSlots off this patch isn't applied, so the mod is back on the vanilla AppVersion.
/// </summary>
public class ModdedLobbyPatches
{
    // Derived from the expanded layout, so mod versions whose layouts differ are kept apart too
    static readonly string AppVersionSuffix =
        $"-unlimited.{ExpandedState.Capacity}" +
        $".{SessionPlayersCapacityPatches.State.WordCount}" +
        $".{MapNetworkCapacityPatches.State.WordCount}" +
        $".{CampCapacityPatches.State.WordCount}";

    // The game builds fresh app settings here both for the server browser (JoinLobby) and for starting or joining
    // a session (StartGameAsync)
    [HarmonyPatch(typeof(OverlandNetworkRunner), nameof(OverlandNetworkRunner.BuildCustomAppSetting))]
    [HarmonyPostfix]
    static void Postfix(FusionAppSettings __result)
    {
        if (__result == null || (__result.AppVersion?.EndsWith(AppVersionSuffix) ?? false))
            return;

        __result.AppVersion += AppVersionSuffix;
        Plugin.Logger.LogInfo($"Using Photon AppVersion '{__result.AppVersion}', so only players with this mod " +
                              "version can see this server");
    }
}

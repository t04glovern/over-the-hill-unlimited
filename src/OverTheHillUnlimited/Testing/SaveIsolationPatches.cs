using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Keeps a self-test run out of the player's progress. The self-test hosts with the real profile, so anything it does
/// (spawning at a cabin, reaching a point of interest, revealing fog of war, visiting an area for the first time) would
/// otherwise be saved, and achievements and Steam stats would count it. For the whole run the game doesn't write its
/// save or settings (OverlandSaver.SaveGame, SaveSettings and the SaveJson they both write through) or fog of war
/// (FogOfWarManager.SaveFogOfWarTexture), and doesn't unlock achievements or change stats (AchievementManager.Unlock,
/// SetStat, AddToStat). The profile still loads as usual. The self-test also checks no file in the save folder was
/// written. Applied only in self-test runs.
/// </summary>
public class SaveIsolationPatches
{
    /// <summary>The game's save folder: Documents/My Games/over the hill/Saves.</summary>
    public static string SaveFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "over the hill", "Saves");

    /// <summary>When each file in the save folder was last written, to check none are written during the run.</summary>
    public static Dictionary<string, DateTime> SaveFileTimes()
    {
        var times = new Dictionary<string, DateTime>();
        var folder = SaveFolder;
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                times[file] = File.GetLastWriteTimeUtc(file);
        }
        return times;
    }

    /// <summary>The save files written since these times, or created since.</summary>
    public static List<string> WrittenSince(Dictionary<string, DateTime> before) =>
        SaveFileTimes().Where(file => !before.TryGetValue(file.Key, out var time) || time != file.Value)
            .Select(file => Path.GetRelativePath(SaveFolder, file.Key)).ToList();

    /// <summary>How many times the game tried to save during the run (for the self-test's log).</summary>
    public static int SkippedSaves { get; private set; }

    [HarmonyPatch(typeof(OverlandSaver), nameof(OverlandSaver.SaveGame))]
    [HarmonyPrefix]
    static bool SkipSaveGame()
    {
        SkippedSaves++;
        return false;
    }

    [HarmonyPatch(typeof(OverlandSaver), nameof(OverlandSaver.SaveSettings))]
    [HarmonyPrefix]
    static bool SkipSaveSettings() => false;

    [HarmonyPatch(typeof(OverlandSaver), nameof(OverlandSaver.SaveJson))]
    [HarmonyPrefix]
    static bool SkipSaveJson() => false;

    [HarmonyPatch(typeof(FogOfWarManager), nameof(FogOfWarManager.SaveFogOfWarTexture))]
    [HarmonyPrefix]
    static bool SkipFogOfWar() => false;

    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.Unlock))]
    [HarmonyPrefix]
    static bool NoAchievements() => false;

    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.SetStat))]
    [HarmonyPrefix]
    static bool NoStatChanges() => false;

    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.AddToStat))]
    [HarmonyPrefix]
    static bool NoStatIncreases() => false;
}

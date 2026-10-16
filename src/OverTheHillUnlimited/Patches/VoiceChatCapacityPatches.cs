using HarmonyLib;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Room in voice chat for everyone. In-game voice goes through Discord, and DiscordManager plays each other player's
/// voice through one of its voice emitters, tracked by voiceEmitters and voiceEmittersOccupationStatus, which have
/// slots for 3 other players. The 4th and later get an index past the end: they're silent, every audio frame from them
/// logs an exception, and leaving the call leaves a stale entry that makes rejoining fail. Before a call starts (the
/// only way into SetupCallParticipants, and participant callbacks only fire after it), this clones the last emitter
/// until there's one per possible player, and adds a free slot for each.
/// </summary>
public class VoiceChatCapacityPatches
{
    [HarmonyPatch(typeof(DiscordManager), nameof(DiscordManager.StartCall))]
    [HarmonyPrefix]
    static void PadBeforeCall(DiscordManager __instance)
    {
        if (UnlimitedMode.Enabled)
            PadVoiceEmitters(__instance);
    }

    /// <summary>Pads the emitters and their slots to ExpandedState.Capacity (enough even if the call lists you too).</summary>
    internal static void PadVoiceEmitters(DiscordManager manager)
    {
        var emitters = manager.voiceEmitters;
        var occupied = manager.voiceEmittersOccupationStatus;
        if (emitters == null || occupied == null || emitters.Count == 0)
            return;

        var template = emitters[emitters.Count - 1];
        int vanillaCount = emitters.Count;
        while (emitters.Count < ExpandedState.Capacity)
        {
            var clone = Object.Instantiate(template.gameObject, template.transform.parent).GetComponent<DiscordVoiceEmitter>();
            clone.name = $"{template.gameObject.name} (slot {emitters.Count + 1})";
            if (template._initialized && !clone._initialized)
                clone.Init(template._sampleRate, template._channelCount, template._inputFormat);
            emitters.Add(clone);
        }
        while (occupied.Count < emitters.Count)
            occupied.Add(false);
        if (emitters.Count > vanillaCount)
            Plugin.Logger.LogInfo($"Voice chat emitters padded from {vanillaCount} to {emitters.Count}");
    }
}

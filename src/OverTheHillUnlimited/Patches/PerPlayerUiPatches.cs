using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using OverTheHillUnlimited.Networking;
using UnityEngine;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// UIs that keep one pre-built element per vanilla player slot and index them by the player's slot or position in a
/// loop over the session's players, so with 5+ players they read past the end. Each list is padded to
/// ExpandedState.Capacity with clones of its last element, in a prefix on the methods that index it first, so players 5
/// and up get elements of their own. The game positions these elements itself (most track a car or anchor in the world,
/// and hidden rows take no space in a layout group), so clones need no layout. Clones start hidden; the game shows the
/// ones it uses, the same way it does for the vanilla elements.
/// </summary>
public class PerPlayerUiPatches
{
    // Name labels over each car in the lobby, indexed by the player's spawn point
    [HarmonyPatch(typeof(StateLobbyMenu), nameof(StateLobbyMenu.UpdateCarsStatus))]
    [HarmonyPrefix]
    static void PadLobbyNameLabels(StateLobbyMenu __instance)
    {
        Pad(__instance.lobbyMenuContextUI?.LobbyPlayersStatus);
    }

    // Repair buttons over other players' cars in the car repair menu
    [HarmonyPatch(typeof(StateCarRepair), nameof(StateCarRepair.SetupCarRepairButtons))]
    [HarmonyPrefix]
    static void PadRepairButtonsOnSetup(StateCarRepair __instance)
    {
        Pad(__instance.contextUICarRepair?.CarRepairButtons);
    }

    [HarmonyPatch(typeof(StateCarRepair), nameof(StateCarRepair.VehicleHealChanged))]
    [HarmonyPrefix]
    static void PadRepairButtonsOnHeal(StateCarRepair __instance)
    {
        Pad(__instance.contextUICarRepair?.CarRepairButtons);
    }

    // Car health buttons and available tools panels in the tool selection menu
    [HarmonyPatch(typeof(StateSelectTool), nameof(StateSelectTool.SetupCarHealthAndAvailableToolsUI))]
    [HarmonyPrefix]
    static void PadSelectToolOnSetup(StateSelectTool __instance)
    {
        PadSelectTool(__instance);
    }

    [HarmonyPatch(typeof(StateSelectTool), nameof(StateSelectTool.VehicleHealChanged))]
    [HarmonyPrefix]
    static void PadSelectToolOnHeal(StateSelectTool __instance)
    {
        PadSelectTool(__instance);
    }

    [HarmonyPatch(typeof(StateSelectTool), nameof(StateSelectTool.ItemSlotChanged))]
    [HarmonyPrefix]
    static void PadSelectToolOnItemSlot(StateSelectTool __instance)
    {
        PadSelectTool(__instance);
    }

    // Player rows in the pause menu, indexed by slot
    [HarmonyPatch(typeof(StatePauseMenu), nameof(StatePauseMenu.SetupMenu))]
    [HarmonyPrefix]
    static void PadPauseMenuRows(StatePauseMenu __instance)
    {
        Pad(__instance.contextUIPauseMenu?.PlayerInfos);
    }

    // Player markers and pins on the map, indexed by slot
    [HarmonyPatch(typeof(MapView), nameof(MapView.OnEnable))]
    [HarmonyPrefix]
    static void PadMapMarkers(MapView __instance)
    {
        Pad(__instance.playerIndicators);
        Pad(__instance.pinpointIndicators);
    }

    // The map's DelayedEnable coroutine builds pinpointIndicatorsRectTransforms, parallel to pinpointIndicators, with a
    // hard-coded loop over the first 4 pins, so padded pins get no rect. Add theirs before the map indexes it by slot.
    [HarmonyPatch(typeof(MapView), nameof(MapView.UpdatePlayerPinpoint))]
    [HarmonyPrefix]
    static void AddPinRects(MapView __instance)
    {
        var pins = __instance.pinpointIndicators;
        var rects = __instance.pinpointIndicatorsRectTransforms;
        if (!UnlimitedMode.Enabled || pins == null || rects == null || rects.Count >= pins.Count)
            return;
        for (int i = rects.Count; i < pins.Count; i++)
            rects.Add(pins[i].GetComponent<RectTransform>());
    }

    // Car health markers over other players' cars on the HUD
    [HarmonyPatch(typeof(CarHealthUI), nameof(CarHealthUI.SetupPlayerCarHealthStatus))]
    [HarmonyPrefix]
    static void PadCarHealthMarkers(CarHealthUI __instance)
    {
        Pad(__instance.carHealthStatusButtons);
    }

    // HUD markers for players' portable winch anchors
    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.OnPortableWinchAnchorsChanged))]
    [HarmonyPrefix]
    static void PadWinchAnchorMarkersOnChange(HUDManager __instance)
    {
        PadWinchAnchorMarkers(__instance);
    }

    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PortalActivated))]
    [HarmonyPrefix]
    static void PadWinchAnchorMarkersOnPortalStart(HUDManager __instance)
    {
        PadWinchAnchorMarkers(__instance);
    }

    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PortalFinished))]
    [HarmonyPrefix]
    static void PadWinchAnchorMarkersOnPortalEnd(HUDManager __instance)
    {
        PadWinchAnchorMarkers(__instance);
    }

    static void PadSelectTool(StateSelectTool state)
    {
        var ui = state.contextUISelectTool;
        if (ui == null)
            return;
        Pad(ui.CarHealthButtons);
        Pad(ui.AvailableToolsPanels);
    }

    static void Pad<T>(Il2CppSystem.Collections.Generic.List<T> list) where T : Component
    {
        if (!UnlimitedMode.Enabled || list == null || list.Count == 0 || list.Count >= ExpandedState.Capacity)
            return;
        var template = list[list.Count - 1];
        if ((Object)template == null)
            return;

        int vanillaCount = list.Count;
        while (list.Count < ExpandedState.Capacity)
            list.Add(Clone(template, list.Count));
        Plugin.Logger.LogInfo($"Padded {typeof(T).Name} elements from {vanillaCount} to {list.Count}");
    }

    static void PadWinchAnchorMarkers(HUDManager hud)
    {
        if (!UnlimitedMode.Enabled)
            return;
        var markers = hud.portableWinchAnchorIndicators;
        if (markers == null || markers.Length == 0 || markers.Length >= ExpandedState.Capacity)
            return;
        var template = markers[markers.Length - 1];
        if (template == null)
            return;

        var padded = new Il2CppReferenceArray<WorldTrackingRectTransform>(ExpandedState.Capacity);
        for (int i = 0; i < markers.Length; i++)
            padded[i] = markers[i];
        for (int i = markers.Length; i < padded.Length; i++)
            padded[i] = Clone(template, i);
        hud.portableWinchAnchorIndicators = padded;
        Plugin.Logger.LogInfo($"Padded portable winch anchor markers from {markers.Length} to {padded.Length}");
    }

    static T Clone<T>(T template, int index) where T : Component
    {
        var clone = Object.Instantiate(template.gameObject, template.transform.parent);
        clone.name = $"{template.gameObject.name} (slot {index + 1})";
        clone.SetActive(false);
        return clone.GetComponent<T>();
    }
}

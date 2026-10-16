using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using OverTheHillUnlimited.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// UIs that keep one pre-built element per vanilla player slot and index them by the player's slot or position in a
/// loop over the session's players, so with 5+ players they read past the end. Each list is padded to
/// ExpandedState.Capacity with clones of its last element, in prefixes on the methods that index it, so players 5 and up
/// get elements of their own. The game positions these elements itself (most track a car or anchor in the world, and
/// hidden rows take no space in a layout group), so clones need no layout. Clones start hidden; the game shows the ones
/// it uses, the same way it does for the vanilla elements. Instantiate doesn't copy event handlers the game added in
/// code, so where the game wires elements up before the padding runs (the pause menu), the patch wires the clones too.
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

    // Player rows in the pause menu, indexed by slot. StatePauseMenu.Open gives each row its click handler,
    // OpenPlayerMenu(row, slot) for the player options (kick, mute, volume), before it calls SetupMenu, so the rows
    // added here need theirs added too. The menu's UI is spawned afresh each time it opens, so this runs once per row.
    [HarmonyPatch(typeof(StatePauseMenu), nameof(StatePauseMenu.SetupMenu))]
    [HarmonyPrefix]
    static void PadPauseMenuRows(StatePauseMenu __instance)
    {
        var menu = __instance.contextUIPauseMenu;
        var rows = menu?.PlayerInfos;
        int vanillaCount = rows?.Count ?? 0;
        if (vanillaCount > 0 && vanillaCount < ExpandedState.Capacity)
            _pauseRowsExit = rows[vanillaCount - 1].ButtonSelector.navigation.selectOnDown;
        if (!Pad(rows))
            return;
        for (int i = vanillaCount; i < rows.Count; i++)
        {
            int slot = i;
            rows[i].ButtonSelector.add_OnPointerClickOrSubmit(
                (System.Action<UIButtonSelector>)(button => __instance.OpenPlayerMenu(button, slot)));

            // ContextUIPauseMenu.Awake gives every selector in the menu the menu's StyleParentDisabled, once. Clones
            // keep UISelector's default instead, so they'd grey out whenever the menu loses focus (player options,
            // settings) while the game's own rows don't.
            foreach (var selector in rows[i].GetComponentsInChildren<UISelector>(true))
                selector.StyleParentDisabled = menu.styleParentDisabled;
        }
    }

    // The rows have explicit gamepad links, which clones copy from the last vanilla row, so "down" never got past the
    // 4th. Once SetupMenu has shown the rows in use, chain them, ending where the last vanilla row's "down" first went.
    [HarmonyPatch(typeof(StatePauseMenu), nameof(StatePauseMenu.SetupMenu))]
    [HarmonyPostfix]
    static void LinkPauseMenuRows(StatePauseMenu __instance)
    {
        var rows = __instance.contextUIPauseMenu?.PlayerInfos;
        if (!UnlimitedMode.Enabled || rows == null || rows.Count <= Plugin.VanillaMaxPlayers)
            return;

        var shown = new List<Selectable>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].gameObject.activeSelf)
                shown.Add(rows[i].ButtonSelector);
        }
        for (int i = 0; i < shown.Count; i++)
        {
            var navigation = shown[i].navigation;
            if (navigation.mode != Navigation.Mode.Explicit)
                return;
            if (i > 0)
                navigation.selectOnUp = shown[i - 1];
            navigation.selectOnDown = i + 1 < shown.Count ? shown[i + 1] : _pauseRowsExit;
            shown[i].navigation = navigation;
        }
    }

    // Where "down" from the last vanilla pause row went before any linking (the menu is spawned afresh on each open)
    static Selectable _pauseRowsExit;

    // Player markers and pins on the map, indexed by slot
    [HarmonyPatch(typeof(MapView), nameof(MapView.OnEnable))]
    [HarmonyPrefix]
    static void PadMapOnEnable(MapView __instance)
    {
        PadMap(__instance);
    }

    [HarmonyPatch(typeof(MapView), nameof(MapView.SetupPlayerNames))]
    [HarmonyPrefix]
    static void PadMapOnSetupNames(MapView __instance)
    {
        PadMap(__instance);
    }

    [HarmonyPatch(typeof(MapView), nameof(MapView.UpdatePlayersPinpoints))]
    [HarmonyPrefix]
    static void PadMapOnUpdatePins(MapView __instance)
    {
        PadMap(__instance);
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
        var buttons = __instance.carHealthStatusButtons;
        int vanillaCount = buttons?.Count ?? 0;
        if (!Pad(buttons))
            return;

        // CarHealthUI.Start hides each marker's WorldTrackingRectTransform, and Show only ever toggles that object,
        // never the marker's root. So give clones the template's root state and Start's hidden tracker, wherever in
        // the marker the tracker sits.
        var template = buttons[vanillaCount - 1];
        for (int i = vanillaCount; i < buttons.Count; i++)
        {
            buttons[i].gameObject.SetActive(template.gameObject.activeSelf);
            buttons[i].WorldTrackingRectTransform?.gameObject.SetActive(false);
        }
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

    static void PadMap(MapView map)
    {
        Pad(map.playerIndicators);
        Pad(map.pinpointIndicators);
    }

    // Returns whether it added any elements
    static bool Pad<T>(Il2CppSystem.Collections.Generic.List<T> list) where T : Component
    {
        if (!UnlimitedMode.Enabled || list == null || list.Count == 0 || list.Count >= ExpandedState.Capacity)
            return false;
        var template = list[list.Count - 1];
        if ((Object)template == null)
            return false;

        int vanillaCount = list.Count;
        while (list.Count < ExpandedState.Capacity)
            list.Add(Clone(template, list.Count));
        Plugin.Logger.LogInfo($"Padded {typeof(T).Name} elements from {vanillaCount} to {list.Count}");
        return true;
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

using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OverTheHillUnlimited.Patches;

/// <summary>
/// Adds an "Unlimited mod servers" checkbox to the server browser, next to the game's own server filters, cloned from
/// "Hide private servers" so it looks and behaves the same. Ticking it switches Unlimited mode and reconnects the
/// lobby, so the list repopulates with servers from the other pool.
/// </summary>
public class ServerBrowserTogglePatches
{
    const string Label = "Unlimited mod servers";

    // After a click the lobby reconnects in the background; clicks during that overlapped reconnects and the list
    // could end up showing the other pool. The checkbox is disabled until the lobby is back, within these bounds.
    const float MinReconnectSeconds = 1f;
    const float MaxReconnectSeconds = 10f;

    static ContextUIButtonSelectorWithTextAndIcon _toggle;
    static bool? _shownChecked;
    static bool? _shownInteractable;
    static float _lastClickTime = float.NegativeInfinity;

    /// <summary>The checkbox in the open server browser, if any (for the self-test).</summary>
    internal static ContextUIButtonSelectorWithTextAndIcon Toggle => _toggle;

    /// <summary>Whether the checkbox is waiting for the lobby to reconnect (for the self-test).</summary>
    internal static bool IsReconnecting => Reconnecting;

    static bool Reconnecting
    {
        get
        {
            float elapsed = Time.unscaledTime - _lastClickTime;
            if (elapsed >= MaxReconnectSeconds)
                return false;
            if (elapsed < MinReconnectSeconds)
                return true;
            var runner = OverlandNetworkRunner.Instance;
            return runner != null && !runner.IsConnectedToLobby;
        }
    }

    [HarmonyPatch(typeof(ContextUIServerList), nameof(ContextUIServerList.Start))]
    [HarmonyPostfix]
    static void AddToggle(ContextUIServerList __instance)
    {
        var template = __instance.HidePrivateServersFilter;
        if (template == null)
        {
            Plugin.Logger.LogWarning("Server browser has no Hide private servers filter, so there's no Unlimited mode checkbox");
            return;
        }

        var templateTransform = template.transform;
        var clone = Object.Instantiate(template.gameObject, templateTransform.parent);
        clone.name = "OverTheHillUnlimitedModeFilter";
        clone.transform.SetSiblingIndex(templateTransform.GetSiblingIndex() + 1);

        // The step from the game's first filter to its second tells us which way the filters run
        var step = FilterStep(__instance.HideFullServersFilter, template);
        PlaceAfter(template, clone, step);

        // Instantiate doesn't copy the click handlers the game adds in code, so this is the clone's only one
        _toggle = clone.GetComponent<ContextUIButtonSelectorWithTextAndIcon>();
        _toggle.buttonSelector.add_OnPointerClickOrSubmit((System.Action<UIButtonSelector>)(_ => OnClicked(__instance)));
        LinkNavigation(template.buttonSelector, _toggle.buttonSelector, horizontal: Mathf.Abs(step.x) > Mathf.Abs(step.y));
        _shownChecked = null;
        _shownInteractable = null;
        Refresh();
    }

    // Keeps the label, tick and enabled state right every frame, in case the game's localisation rewrites the label
    [HarmonyPatch(typeof(ContextUIServerList), nameof(ContextUIServerList.LateUpdate))]
    [HarmonyPostfix]
    static void Refresh()
    {
        if (_toggle == null)
            return;

        if (_toggle.label != null && _toggle.label.text != Label)
            _toggle.label.text = Label;

        bool enabled = UnlimitedMode.Enabled;
        if (_shownChecked != enabled)
        {
            ContextUIServerList.SetFilterChecked(_toggle, enabled);
            _shownChecked = enabled;
        }

        bool interactable = !Reconnecting;
        if (_shownInteractable != interactable)
        {
            _toggle.buttonSelector.interactable = interactable;
            _shownInteractable = interactable;
        }
    }

    static void OnClicked(ContextUIServerList serverList)
    {
        if (Reconnecting || !UnlimitedMode.TrySetEnabled(!UnlimitedMode.Enabled))
            return;

        _lastClickTime = Time.unscaledTime;
        Refresh();
        CreateServerMenuPatch.ApplyToOpenMenu();

        // Reconnect to the lobby the way changing region does, so the new AppVersion takes effect
        var dropdown = serverList.RegionSelectionDropdown;
        if (dropdown != null)
            serverList.ServerRegionOnChanged(dropdown.value);
        else if (OverlandNetworkRunner.Instance != null)
            OverlandNetworkRunner.Instance.ChangeLobbyRegion(OverlandNetworkRunner.Instance.ConnectedRegion);
    }

    // Offset from the game's first filter to its second; one filter height downwards if they aren't siblings
    static Vector2 FilterStep(ContextUIButtonSelectorWithTextAndIcon first, ContextUIButtonSelectorWithTextAndIcon second)
    {
        var secondRect = second.GetComponent<RectTransform>();
        if (first != null && first.transform.parent == second.transform.parent)
            return secondRect.anchoredPosition - first.GetComponent<RectTransform>().anchoredPosition;
        return new Vector2(0, -secondRect.rect.height);
    }

    // Continue the filters' row or column, unless a layout group arranges them anyway
    static void PlaceAfter(ContextUIButtonSelectorWithTextAndIcon template, GameObject clone, Vector2 step)
    {
        var parent = template.transform.parent;
        if (parent != null && parent.GetComponent<LayoutGroup>() != null)
            return;
        clone.GetComponent<RectTransform>().anchoredPosition = template.GetComponent<RectTransform>().anchoredPosition + step;
    }

    // Splice the checkbox into explicit controller navigation right after the template. Automatic navigation finds
    // it on its own. The clone starts with a copy of the template's links, which is what it should keep on its far side.
    static void LinkNavigation(Selectable template, Selectable toggle, bool horizontal)
    {
        var templateNav = template.navigation;
        if (templateNav.mode != Navigation.Mode.Explicit)
            return;

        var toggleNav = toggle.navigation;
        var next = horizontal ? templateNav.selectOnRight : templateNav.selectOnDown;
        if (horizontal)
        {
            toggleNav.selectOnLeft = template;
            templateNav.selectOnRight = toggle;
        }
        else
        {
            toggleNav.selectOnUp = template;
            templateNav.selectOnDown = toggle;
        }
        template.navigation = templateNav;
        toggle.navigation = toggleNav;

        if (next == null)
            return;
        var nextNav = next.navigation;
        if (horizontal && nextNav.selectOnLeft == template)
            nextNav.selectOnLeft = toggle;
        else if (!horizontal && nextNav.selectOnUp == template)
            nextNav.selectOnUp = toggle;
        next.navigation = nextNav;
    }
}

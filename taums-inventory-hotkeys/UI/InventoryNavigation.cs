using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Collections;
using System.Reflection;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private ClickableComponent? GetInventoryControllerTarget(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var pageFocus = page?.currentlySnappedComponent;
        var menuFocus = menu.currentlySnappedComponent;
        var focused = pageFocus?.myID is ProfilePreviousSnapId or ProfileNextSnapId ? pageFocus
            : menuFocus?.myID is ProfilePreviousSnapId or ProfileNextSnapId ? menuFocus
            : pageFocus ?? menuFocus;

        // Share the focused target between tooltips and input. Refresh before
        // activation if navigation, a profile switch, or a new menu changed it.
        if (!ReferenceEquals(_controllerTargetMenu, menu)
            || !ReferenceEquals(_controllerTargetPage, page)
            || _controllerTargetProfile != _config.ActiveProfile
            || !ReferenceEquals(_controllerTooltipTarget, focused))
        {
            _controllerTargetMenu = menu;
            _controllerTargetPage = page;
            _controllerTargetProfile = _config.ActiveProfile;
            _controllerTooltipTarget = focused;
        }
        return _controllerTooltipTarget;
    }

    private static bool IsInventoryPage(GameMenu menu)
    {
        // GameMenu's first page is Inventory. Read the live selected tab so
        // the custom row only exists while that page is active.
        if (CurrentTabField?.GetValue(menu) is int tabIndex)
            return tabIndex == 0;

        if (CurrentTabProperty?.GetValue(menu) is int propertyTabIndex)
            return propertyTabIndex == 0;

        // Keep the controls hidden if the game changes its internal page API;
        // showing them on the wrong page is more confusing than hiding them.
        return false;
    }

    private bool IsInventoryTabFocused(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var snapped = page?.currentlySnappedComponent ?? menu.currentlySnappedComponent;
        if (snapped is null)
            return false;

        var tab = GetNativeInventoryTab(menu);
        var pageFocus = page?.currentlySnappedComponent;
        var menuFocus = menu.currentlySnappedComponent;
        bool IsInventoryTab(ClickableComponent? component) => component is not null
            && (ReferenceEquals(component, tab)
                || (tab is not null && component.myID == tab.myID)
                || component.name.Equals("inventory", StringComparison.OrdinalIgnoreCase)
                || (tab is not null && component.bounds == tab.bounds));
        var focused = IsInventoryTab(pageFocus) || IsInventoryTab(menuFocus);
        // if (focused)
            // Monitor.Log($"Controller-left from Inventory focus detected (page='{pageFocus?.name}'/{pageFocus?.myID}, menu='{menuFocus?.name}'/{menuFocus?.myID}, tab='{tab?.name}'/{tab?.myID}).", LogLevel.Info);
        return focused;
    }

    private static ClickableComponent? GetNativeInventoryTab(GameMenu menu)
    {
        // GameMenu's tabs list is the authoritative source. Geometry-based
        // matching is ambiguous because page controls can share those bounds.
        if (GameMenuTabsField?.GetValue(menu) is IList tabs && tabs.Count > 0)
            return tabs[0] as ClickableComponent;
        return null;
    }

    private void FocusAddHotkey(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        if (page is null)
        {
            // Monitor.Log("Could not route controller focus to Add hotkey: Inventory page is missing.", LogLevel.Warn);
            return;
        }

        var inventoryTab = GetNativeInventoryTab(menu);
        if (_plusTabBounds.IsEmpty && inventoryTab is not null)
            _plusTabBounds = new Rectangle(inventoryTab.bounds.Left - inventoryTab.bounds.Width + 8,
                inventoryTab.bounds.Y + 6, inventoryTab.bounds.Width, inventoryTab.bounds.Height);
        _controllerAddHotkeyComponent ??= new ClickableComponent(_plusTabBounds, "Add hotkey")
        {
            myID = AddHotkeySnapId,
            rightNeighborID = inventoryTab?.myID ?? ClickableComponent.SNAP_AUTOMATIC,
            leftNeighborID = (GetNativeTabComponents(menu) as IList)?
                .OfType<ClickableComponent>().Where(tab => tab.visible)
                .OrderBy(tab => tab.bounds.Left).LastOrDefault()?.myID
                ?? inventoryTab?.myID ?? ClickableComponent.SNAP_AUTOMATIC
        };
        _controllerAddHotkeyComponent.bounds = _plusTabBounds;
        if (page.allClickableComponents is not null && !page.allClickableComponents.Contains(_controllerAddHotkeyComponent))
            page.allClickableComponents.Add(_controllerAddHotkeyComponent);
        var focusChanged = page.currentlySnappedComponent?.myID != AddHotkeySnapId;
        page.currentlySnappedComponent = _controllerAddHotkeyComponent;
        menu.currentlySnappedComponent = _controllerAddHotkeyComponent;
        _controllerAddHotkeyFocused = true;

        // Use the game's standard snap cursor method so the visual cursor and
        // hover tooltip follow the component for both keyboard and controller.
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
        _useControllerTooltipGlyphs = true;
        if (focusChanged)
        {
            Game1.playSound("smallSelect");
            LogDebug("User action: focused Add hotkey.");
        }
        // Monitor.Log($"Controller focus moved directly to Add hotkey (page={page.GetType().Name}, pageComponents={page.allClickableComponents?.Count ?? -1}, id={AddHotkeySnapId}, bounds={_plusTabBounds}).", LogLevel.Info);
    }

    private void FocusHotkey(GameMenu menu, int index)
    {
        var page = GetInventoryPage(menu);

        if (page is null)
        {
            // Monitor.Log("FOCUS FAILED: inventory page is null", LogLevel.Warn);
            return;
        }

        if (!_customHotkeyComponents.TryGetValue(index, out ClickableComponent? component))
        {
            UpdateHotkeySnapComponents(menu);

            if (!_customHotkeyComponents.TryGetValue(index, out component))
            {
                // Monitor.Log(
                //     $"FOCUS FAILED: shortcut {index} still missing after rebuilding components",
                //     LogLevel.Warn);
                return;
            }
        }

        var focusChanged = page.currentlySnappedComponent?.myID != component.myID;
        page.currentlySnappedComponent = component;
        menu.currentlySnappedComponent = component;
        _controllerAddHotkeyFocused = false;

        SnapControllerCursor(page, SnapCursorMethod);
        if (focusChanged)
            Game1.playSound("smallSelect");

        if (focusChanged)
            LogDebug($"User action: focused hotkey {index + 1} ('{_config.Tabs[index].Name}').");

        LogDebug(
            $"FOCUS RESULT: requested={index}, " +
            $"PageFocus={page.currentlySnappedComponent?.name}/{page.currentlySnappedComponent?.myID}, " +
            $"MenuFocus={menu.currentlySnappedComponent?.name}/{menu.currentlySnappedComponent?.myID}");
    }
    
    private bool TryGetHotkeyComponentIndex(ClickableComponent? component, out int index)
    {
        index = -1;
        if (component is null)
            return false;

        foreach ((int hotkeyIndex, ClickableComponent hotkeyComponent) in _customHotkeyComponents)
        {
            if (!ReferenceEquals(component, hotkeyComponent) && component.myID != hotkeyComponent.myID)
                continue;
            index = hotkeyIndex;
            return index >= 0 && index < _config.Tabs.Count;
        }
        return false;
    }

    private bool TryGetNativeTabIndex(GameMenu menu, ClickableComponent? component, out int index)
    {
        index = -1;
        if (component is null)
            return false;

        if (GetNativeTabComponents(menu) is IList tabs)
        {
            for (var i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] is not ClickableComponent tab)
                    continue;
                if (!ReferenceEquals(component, tab) && component.myID != tab.myID)
                    continue;
                index = i;
                return true;
            }
        }

        // Some game builds expose the visible tabs through a different field.
        // The layout already found their bounds, so use those for controller focus too.
        for (var i = 0; i < _layoutNativeTabs.Count; i++)
        {
            if (component.bounds != _layoutNativeTabs[i])
                continue;
            index = i;
            return true;
        }
        return false;
    }

    private bool TryGetHotkeyNearestToTab(ClickableComponent tab, out int index)
    {
        index = -1;
        if (_customTabBounds.Count == 0)
            return false;

        var nearestRowY = _customTabBounds.Max(entry => entry.bounds.Top);
        index = _customTabBounds.Where(entry => entry.bounds.Top == nearestRowY)
            .OrderBy(entry => Math.Abs(entry.bounds.Center.X - tab.bounds.Center.X))
            .First().index;
        return true;
    }

    private void FocusNearestNativeTab(GameMenu menu, int x)
    {
        var page = GetInventoryPage(menu);
        var tab = (GetNativeTabComponents(menu) as IList)?.OfType<ClickableComponent>()
            .Where(component => component.visible)
            .OrderBy(component => Math.Abs(component.bounds.Center.X - x))
            .FirstOrDefault();
        if (page is null || tab is null)
            return;

        var focusChanged = page.currentlySnappedComponent?.myID != tab.myID;
        page.currentlySnappedComponent = tab;
        menu.currentlySnappedComponent = tab;
        _controllerAddHotkeyFocused = false;
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
        if (focusChanged)
            Game1.playSound("smallSelect");
    }

    private void FocusProfileSwitcher(GameMenu menu, bool next)
    {
        var page = GetInventoryPage(menu);
        if (page is null || !_config.EnableProfileButtons)
            return;
        var component = next ? _controllerProfileNextComponent : _controllerProfilePreviousComponent;
        if (component is null)
            return;
        page.currentlySnappedComponent = component;
        menu.currentlySnappedComponent = component;
        _controllerAddHotkeyFocused = false;
        SnapControllerCursor(page, SnapCursorMethod);
        _useControllerTooltipGlyphs = true;
        Game1.playSound("smallSelect");
        LogDebug($"User action: focused {(next ? "next" : "previous")} profile button.");
    }

    private void FocusHotkeyEdgeFromProfile(GameMenu menu, bool rightEdge)
    {
        var edge = _customHotkeyComponents.Values
            .GroupBy(component => component.bounds.Top)
            .Select(group => rightEdge
                ? group.OrderByDescending(component => component.bounds.Right).First()
                : group.OrderBy(component => component.bounds.Left).First())
            .OrderBy(component => Math.Abs(component.bounds.Center.Y -
                (rightEdge ? _profileNextBounds.Center.Y : _profilePreviousBounds.Center.Y)))
            .FirstOrDefault();
        if (edge is not null)
        {
            var index = edge.myID - 988000;
            FocusHotkey(menu, index);
        }
        else
            FocusAddHotkey(menu);
    }

    private void FocusInventoryTab(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var tab = GetNativeInventoryTab(menu);
        if (page is null || tab is null)
            return;

        var focusChanged = page.currentlySnappedComponent?.myID != tab.myID;
        page.currentlySnappedComponent = tab;
        menu.currentlySnappedComponent = tab;
        _controllerAddHotkeyFocused = false;
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
        if (focusChanged)
            Game1.playSound("smallSelect");
        if (focusChanged)
            LogDebug("User action: focused Inventory tab.");
    }

    private void FocusLastNativeTab(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var lastTab = (GetNativeTabComponents(menu) as IList)?.OfType<ClickableComponent>()
            .Where(tab => tab.visible)
            .OrderBy(tab => tab.bounds.Left)
            .LastOrDefault();
        if (page is null || lastTab is null)
            return;

        var focusChanged = page.currentlySnappedComponent?.myID != lastTab.myID;
        page.currentlySnappedComponent = lastTab;
        menu.currentlySnappedComponent = lastTab;
        _controllerAddHotkeyFocused = false;
        SnapControllerCursor(page, SnapCursorMethod);
        if (focusChanged)
            Game1.playSound("smallSelect");
        if (focusChanged)
            LogDebug("User action: focused last inventory tab.");
    }

    private bool IsAddHotkeyHighlighted(GameMenu menu, bool mouseInput, Point cursor)
    {
        if (!mouseInput || _useControllerTooltipGlyphs)
            return GetInventoryControllerTarget(menu)?.myID == AddHotkeySnapId;
        return _mouseHoverStateUpdated ? _mouseHoveringPlusTab : _plusTabBounds.Contains(cursor);
    }

    private void RestoreInventoryControllerCursor(GameMenu menu)
    {
        if (_pendingInventoryControllerResnapId is not int id)
            return;
        _pendingInventoryControllerResnapId = null;
        if (!_useControllerTooltipGlyphs || GetInventoryPage(menu) is not { } page)
            return;

        var component = page.allClickableComponents?.FirstOrDefault(candidate => candidate.myID == id);
        if (component is null)
            return;

        page.currentlySnappedComponent = component;
        menu.currentlySnappedComponent = component;
        SnapControllerCursor(page, SnapCursorMethod);
    }

    private static Point GetUiCursorPosition()
    {
        var previousUiMode = Game1.uiMode;
        try
        {
            Game1.uiMode = true;
            return new Point(Game1.getMouseX(), Game1.getMouseY());
        }
        finally
        {
            Game1.uiMode = previousUiMode;
        }
    }

    private void SnapControllerCursor(IClickableMenu page, MethodInfo? snapCursor)
    {
        _pendingControllerCursorPosition = null;
        _snappingControllerCursor = true;
        var previousUiMode = Game1.uiMode;
        try
        {
            // Input callbacks can run outside UI mode. Snap using the same UI
            // scale as rendering, independently of the world's zoom level.
            Game1.uiMode = true;
            snapCursor?.Invoke(page, null);
            // The game's snap point is offset inside the component, not its
            // centre. Record the actual warp in physical pixels so the next
            // cursor event is recognised across UI scales and rounding.
            if (snapCursor is not null)
                _pendingControllerCursorPosition = Microsoft.Xna.Framework.Input.Mouse.GetState().Position;
        }
        finally
        {
            Game1.uiMode = previousUiMode;
            _snappingControllerCursor = false;
        }
    }

    private void SetHotkeysVisible(bool visible)
    {
        if (_hotkeysVisible == visible)
            return;
        _hotkeysVisible = visible;
        _layoutDirty = true;
        Game1.playSound(visible ? "shwip" : "smallSelect");
        LogDebug($"User action: {(visible ? "showed" : "hid")} hotkeys.");
        // Monitor.Log($"{(visible ? "Showing" : "Hiding")} custom hotkeys via {(_useControllerTooltipGlyphs ? "controller Y" : "middle mouse button")}.", LogLevel.Info);
    }

    private void ActivateHotkey(int index)
    {
        if (index < 0 || index >= _config.Tabs.Count)
            return;

        var selectedTab = _config.Tabs[index];
        LogDebug($"User action: activated hotkey {index + 1} ('{selectedTab.Name}'); bound={selectedTab.Keybinds.IsBound}; keybind='{selectedTab.Keybinds}'.");
        // Monitor.Log($"Hotkey activated: index={index}, name='{selectedTab.Name}', keybind='{selectedTab.Keybinds}', bound={selectedTab.Keybinds.IsBound}.", LogLevel.Info);

        if (!selectedTab.Keybinds.IsBound)
        {
            ShowHotkeyNotification($"Hotkey not triggered: {selectedTab.Name} has no keybind assigned.");
            return;
        }

        _pendingKeybind = new PendingKeybind(selectedTab.Name, selectedTab.Keybinds, selectedTab.Icon, 2);
        Game1.exitActiveMenu();
        // Monitor.Log($"Closed inventory for hotkey '{selectedTab.Name}'; will dispatch '{selectedTab.Keybinds}' after a short delay.", LogLevel.Info);
    }

}

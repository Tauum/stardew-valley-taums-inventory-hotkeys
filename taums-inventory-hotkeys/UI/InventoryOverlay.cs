using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System.Collections;
using System.Reflection;

namespace Taums_menu_supreme;

public sealed partial class ModEntry
{
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
            _plusTabBounds = new Rectangle(inventoryTab.bounds.Left - inventoryTab.bounds.Width + 28, inventoryTab.bounds.Y + 6, inventoryTab.bounds.Width, inventoryTab.bounds.Height);
        _controllerAddHotkeyComponent ??= new ClickableComponent(_plusTabBounds, "Add hotkey")
        {
            myID = AddHotkeySnapId,
            rightNeighborID = inventoryTab?.myID ?? ClickableComponent.SNAP_AUTOMATIC,
            leftNeighborID = inventoryTab?.myID ?? ClickableComponent.SNAP_AUTOMATIC
        };
        _controllerAddHotkeyComponent.bounds = _plusTabBounds;
        if (page.allClickableComponents is not null && !page.allClickableComponents.Contains(_controllerAddHotkeyComponent))
            page.allClickableComponents.Add(_controllerAddHotkeyComponent);
        page.currentlySnappedComponent = _controllerAddHotkeyComponent;
        menu.currentlySnappedComponent = _controllerAddHotkeyComponent;
        _controllerAddHotkeyFocused = true;

        // Use the game's standard snap cursor method so the visual cursor and
        // hover tooltip follow the component for both keyboard and controller.
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
        _useControllerTooltipGlyphs = true;
        // Monitor.Log($"Controller focus moved directly to Add hotkey (page={page.GetType().Name}, pageComponents={page.allClickableComponents?.Count ?? -1}, id={AddHotkeySnapId}, bounds={_plusTabBounds}).", LogLevel.Info);
    }

    private void FocusHotkey(GameMenu menu, int index)
    {
        var page = GetInventoryPage(menu);
        if (page is null || !_customHotkeyComponents.TryGetValue(index, out ClickableComponent? component))
            return;

        page.currentlySnappedComponent = component;
        menu.currentlySnappedComponent = component;
        _controllerAddHotkeyFocused = false;
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
        // Monitor.Log($"Controller focus moved to hotkey [{index}] '{_config.Tabs[index].Name}' at {component.bounds}.", LogLevel.Trace);
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

        var firstRowCount = Math.Min(10, _customTabBounds.Count);
        index = _customTabBounds.Take(firstRowCount)
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

        page.currentlySnappedComponent = tab;
        menu.currentlySnappedComponent = tab;
        _controllerAddHotkeyFocused = false;
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
    }

    private void FocusInventoryTab(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var tab = GetNativeInventoryTab(menu);
        if (page is null || tab is null)
            return;

        page.currentlySnappedComponent = tab;
        menu.currentlySnappedComponent = tab;
        _controllerAddHotkeyFocused = false;
        var snapCursor = SnapCursorMethod;
        SnapControllerCursor(page, snapCursor);
    }

    private bool IsAddHotkeyHighlighted(GameMenu menu, bool mouseInput)
    {
        var focused = GetInventoryPage(menu)?.currentlySnappedComponent;
        if (!mouseInput && _controllerAddHotkeyComponent is not null
            && (ReferenceEquals(focused, _controllerAddHotkeyComponent) || focused?.myID == AddHotkeySnapId))
            return true;

        return mouseInput && _plusTabBounds.Contains(Game1.getMouseX(), Game1.getMouseY());
    }

    private void SnapControllerCursor(IClickableMenu page, MethodInfo? snapCursor)
    {
        _pendingControllerCursorPosition = page.currentlySnappedComponent?.bounds.Center;
        _snappingControllerCursor = true;
        try { snapCursor?.Invoke(page, null); }
        finally { _snappingControllerCursor = false; }
    }

    private void SetHotkeysVisible(bool visible)
    {
        _hotkeysVisible = visible;
        _layoutDirty = true;
        // Monitor.Log($"{(visible ? "Showing" : "Hiding")} custom hotkeys via {(_useControllerTooltipGlyphs ? "controller Y" : "middle mouse button")}.", LogLevel.Info);
    }

    private void ActivateHotkey(int index)
    {
        if (index < 0 || index >= _config.Tabs.Count)
            return;

        var selectedTab = _config.Tabs[index];
        // Monitor.Log($"Hotkey activated: index={index}, name='{selectedTab.Name}', keybind='{selectedTab.Keybinds}', bound={selectedTab.Keybinds.IsBound}.", LogLevel.Info);

        if (!selectedTab.Keybinds.IsBound)
        {
            // Monitor.Log($"Hotkey '{selectedTab.Name}' has no assigned keybind; no key was sent.", LogLevel.Info);
            return;
        }

        _pendingKeybind = new PendingKeybind(selectedTab.Keybinds, 2);
        Game1.exitActiveMenu();
        // Monitor.Log($"Closed inventory for hotkey '{selectedTab.Name}'; will dispatch '{selectedTab.Keybinds}' after a short delay.", LogLevel.Info);
    }


    private void EnsureLayout(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        Rectangle menuBounds = new(menu.xPositionOnScreen, menu.yPositionOnScreen, menu.width, menu.height);
        Rectangle viewport = new(Game1.uiViewport.X, Game1.uiViewport.Y, Game1.uiViewport.Width, Game1.uiViewport.Height);
        Rectangle nativeTabBounds = GetNativeInventoryTab(menu)?.bounds ?? Rectangle.Empty;
        if (!_layoutDirty && _layoutMenu == menu && _layoutPage == page
            && _layoutMenuBounds == menuBounds && _layoutViewport == viewport
            && _layoutNativeTabBounds == nativeTabBounds
            && _layoutHotkeyCount == _config.Tabs.Count && _layoutHotkeysVisible == _hotkeysVisible)
            return;

        RepositionTabs(menu);
        _layoutMenu = menu;
        _layoutPage = page;
        _layoutMenuBounds = menuBounds;
        _layoutViewport = viewport;
        _layoutNativeTabBounds = nativeTabBounds;
        _layoutHotkeyCount = _config.Tabs.Count;
        _layoutHotkeysVisible = _hotkeysVisible;
        _layoutDirty = false;
    }

    private void RepositionTabs(GameMenu menu)
    {
        if (_snapGraphMenu is not null
            && (_snapGraphMenu != menu || _snapGraphInventoryPage != GetInventoryPage(menu)))
            RemoveAddHotkeyFromSnapGraph(_snapGraphMenu);

        // Place the add-hotkey button directly beside the native Inventory tab.
        var tabY = menu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY + 64;
        List<Rectangle> nativeTabs = GetNativeTabBounds(menu, tabY);
        _layoutNativeTabs.Clear();
        _layoutNativeTabs.AddRange(nativeTabs);
        Rectangle? leftmostNativeTab = nativeTabs.Count > 0 ? nativeTabs[0] : null;
        var rowWidth = leftmostNativeTab?.Width ?? 64;
        var rowHeight = leftmostNativeTab?.Height ?? 64;
        var nativeLeft = leftmostNativeTab?.Left ?? menu.xPositionOnScreen + 48;
        int[] nativeColumnAnchors = nativeTabs.Select(bounds => bounds.Left).Distinct().OrderBy(x => x).ToArray();
        // Align each custom-tab column to the actual inventory item slots shown
        // below the tab strip. Fall back to native tab anchors if the game
        // changes its inventory page internals.
        int[] inventoryColumnAnchors = GetInventoryColumnAnchors(menu, tabY);
        int[] columnAnchors = inventoryColumnAnchors.Length >= 10 ? inventoryColumnAnchors : nativeColumnAnchors;
        var columnSpacing = GetColumnSpacing(columnAnchors, rowWidth);
        _plusTabBounds = leftmostNativeTab is Rectangle nativeTab
            ? new Rectangle(nativeTab.Left - rowWidth + 28, nativeTab.Y + 6, rowWidth, nativeTab.Height)
            : new Rectangle(nativeLeft - rowWidth + 24, tabY + 4, rowWidth, rowWidth);
        // The custom row matches the native 64px tabs and overlaps the native
        // row by four pixels, like a stacked tab strip. It is drawn first so
        // the native tabs cover its lower edge.
        var customY = tabY - rowHeight + 4;
        _customTabBounds.Clear();
        const int maxPerRow = 10;
        for (var i = 0; i < _config.Tabs.Count; i++)
        {
            var row = i / maxPerRow;
            var col = i % maxPerRow;
            // Reuse the exact X positions from the inventory slot columns;
            // extrapolate only if fewer than ten anchors were exposed.
            var columnLeft = col < columnAnchors.Length
                ? columnAnchors[col]
                : columnAnchors.Length > 0
                    ? columnAnchors[^1] + (col - columnAnchors.Length + 1) * columnSpacing
                    : nativeLeft + col * columnSpacing;
            // Inventory slot anchors sit a few pixels left of the native tab art.
            var bounds = new Rectangle(columnLeft + 8, customY - row * rowHeight, rowWidth, rowHeight);
            _customTabBounds.Add((bounds, i));
        }
        RepositionEditorControls(menu);
    }

    private void RepositionEditorControls(GameMenu menu)
    {
        var editorWidth = Math.Min(760, menu.width - 48);
        const int confirmationSize = 52;
        const int confirmationGap = 10;
        var editorHeight = Math.Min(680, menu.height - 32);
        _editorBounds = new Rectangle(menu.xPositionOnScreen + (menu.width - editorWidth) / 2,
            menu.yPositionOnScreen + (menu.height - editorHeight) / 2, editorWidth, editorHeight);
        _editorNameBounds = new Rectangle(_editorBounds.X + 184, _editorBounds.Y + 164, _editorBounds.Width - 360, 72);
        _editorNameHitBounds = new Rectangle(_editorNameBounds.X - 48, _editorNameBounds.Y - 56,
            _editorNameBounds.Width + 96, _editorNameBounds.Height + 112);
        _editorIconCarouselBounds = new Rectangle(_editorBounds.X + 32, _editorBounds.Y + 276, _editorBounds.Width - 64, 124);
        // Keep the carousel controls on the right half of the row and reserve
        // clear space at the far right for its position counter.
        var iconGroupCenterX = _editorIconCarouselBounds.Center.X + 8;
        var iconRowCenterY = _editorIconCarouselBounds.Y + 78;
        _editorIconPreviousBounds = new Rectangle(iconGroupCenterX - 104, iconRowCenterY - 24, 48, 48);
        _editorIconBounds = new Rectangle(iconGroupCenterX - 48, iconRowCenterY - 44, 96, 88);
        _editorIconNextBounds = new Rectangle(iconGroupCenterX + 56, iconRowCenterY - 24, 48, 48);
        _editorIconCounterBounds = new Rectangle(_editorIconNextBounds.Right + 12, iconRowCenterY - 30, 80, 60);
        _editorIconDiceBounds = new Rectangle(_editorIconCounterBounds.Right + 8, iconRowCenterY - 22, 44, 44);
        _editorKeybindBounds = new Rectangle(_editorBounds.Right - 360, _editorBounds.Y + 456, 208, 96);
        _editorKeybindHitBounds = _editorKeybindBounds;
        _keybindCaptureBounds = new Rectangle(_editorBounds.Center.X - 280, _editorBounds.Center.Y - 100, 560, 200);
        var confirmationY = _editorBounds.Bottom - confirmationSize - 16;
        var confirmationX = _editorBounds.Right - confirmationSize - 13;
        _editorCreateBounds = new Rectangle(confirmationX, confirmationY, confirmationSize, confirmationSize);
        _editorCancelBounds = new Rectangle(_editorCreateBounds.X - confirmationGap - confirmationSize, confirmationY, confirmationSize, confirmationSize);
        _editorDeleteBounds = new Rectangle(_editorCancelBounds.X - confirmationGap - confirmationSize,
            confirmationY, confirmationSize, confirmationSize);
        _deleteConfirmBounds = new Rectangle(_editorBounds.Center.X - 260, _editorBounds.Center.Y - 110, 520, 220);
        const int deleteButtonWidth = 104;
        const int deleteButtonGap = 12;
        var deleteButtonsWidth = deleteButtonWidth * 2 + deleteButtonGap;
        var deleteButtonsLeft = _deleteConfirmBounds.Center.X - deleteButtonsWidth / 2;
        var deleteButtonsY = _deleteConfirmBounds.Bottom - 76;
        _deleteConfirmYesBounds = new Rectangle(deleteButtonsLeft, deleteButtonsY, deleteButtonWidth, 48);
        _deleteConfirmNoBounds = new Rectangle(deleteButtonsLeft + deleteButtonWidth + deleteButtonGap,
            deleteButtonsY, deleteButtonWidth, 48);
    }

    private void DrawPlusTab(SpriteBatch b)
    {
        DrawVanillaTabBackground(b, _plusTabBounds);
        // Use the vanilla Energy HUD cap from LooseSprites/Cursors. The old
        // source rectangle was a harvesting mouse cursor, which looked like a
        // pointer placed on top of the tab icon.
        Rectangle energySource = new(256, 408, 12, 16);
        const int iconWidth = 30;
        const int iconHeight = 40;
        Rectangle iconBounds = new(_plusTabBounds.Center.X - iconWidth / 2, _plusTabBounds.Center.Y - iconHeight / 2, iconWidth, iconHeight);
        b.Draw(Game1.mouseCursors, iconBounds, energySource, Color.White);
    }

    private void DrawCustomTabs(SpriteBatch b)
    {
        foreach (var entry in _customTabBounds)
        {
            DrawVanillaTabBackground(b, entry.bounds);
            // The native tab art has a slightly top-heavy silhouette; lower
            // the centered emoji a few pixels to match the native item-icon balance.
            Rectangle iconBounds = new(entry.bounds.X, entry.bounds.Y + 4, entry.bounds.Width, entry.bounds.Height);
            _emojiCatalog.DrawIcon(b, _config.Tabs[entry.index].Icon, iconBounds, maxSize: 38);
        }
    }

    private static void DrawVanillaTabBackground(SpriteBatch b, Rectangle bounds)
    {
        b.Draw(Game1.mouseCursors, bounds, new Rectangle(16, 368, 16, 16), Color.White);
    }

    private void AddHotkeyToSnapGraph(GameMenu menu, IReadOnlyList<Rectangle> nativeTabs)
    {
        var inventoryPage = GetInventoryPage(menu);
        if (nativeTabs.Count == 0 || inventoryPage?.allClickableComponents is null)
            return;

        // GameMenu adds the native tab components to every page's clickable
        // list. Search the inventory page, where the actual snap cursor lives.
        var inventoryTab = GetNativeInventoryTab(menu);
        inventoryTab ??= inventoryPage.allClickableComponents
            .FirstOrDefault(component => component.name.Equals("inventory", StringComparison.OrdinalIgnoreCase));
        inventoryTab ??= FindNativeTabComponent(menu, nativeTabs[0]);
        if (inventoryTab is null)
            return;

        if (_snapGraphMenu != menu || _snapGraphInventoryPage != inventoryPage || _snapGraphInventoryTab != inventoryTab)
        {
            RemoveAddHotkeyFromSnapGraph(_snapGraphMenu);
            _snapGraphMenu = menu;
            _snapGraphInventoryPage = inventoryPage;
            _snapGraphInventoryTab = inventoryTab;
            _addedInventoryTabToClickableSet = !inventoryPage.allClickableComponents.Contains(inventoryTab);
            _originalInventoryTabLeftNeighborId = inventoryTab.leftNeighborID;
        }

        _controllerAddHotkeyComponent ??= new ClickableComponent(_plusTabBounds, "Add hotkey");
        _controllerAddHotkeyComponent.bounds = _plusTabBounds;
        _controllerAddHotkeyComponent.myID = AddHotkeySnapId;
        _controllerAddHotkeyComponent.rightNeighborID = inventoryTab.myID;
        _controllerAddHotkeyComponent.leftNeighborID = inventoryTab.myID;
        _controllerAddHotkeyComponent.upNeighborID = ClickableComponent.SNAP_AUTOMATIC;
        _controllerAddHotkeyComponent.downNeighborID = ClickableComponent.SNAP_AUTOMATIC;
        inventoryTab.leftNeighborID = AddHotkeySnapId;
        if (_addedInventoryTabToClickableSet && !inventoryPage.allClickableComponents.Contains(inventoryTab))
            inventoryPage.allClickableComponents.Add(inventoryTab);
        if (!inventoryPage.allClickableComponents.Contains(_controllerAddHotkeyComponent))
            inventoryPage.allClickableComponents.Add(_controllerAddHotkeyComponent);
        UpdateHotkeySnapComponents(menu);
        // Monitor.Log($"Add-hotkey snap graph active: page={inventoryPage.GetType().Name}, tab='{inventoryTab.name}' id={inventoryTab.myID}, tabInPage={inventoryPage.allClickableComponents.Contains(inventoryTab)}, addInPage={inventoryPage.allClickableComponents.Contains(_controllerAddHotkeyComponent)}, leftNeighbor={inventoryTab.leftNeighborID}, addBounds={_plusTabBounds}.", LogLevel.Info);
    }

    private void UpdateHotkeySnapComponents(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        if (page?.allClickableComponents is null)
            return;

        if (!_hotkeysVisible)
        {
            foreach (var component in _customHotkeyComponents.Values)
                page.allClickableComponents.Remove(component);
            return;
        }

        for (int staleIndex = _config.Tabs.Count; staleIndex < MaxCustomTabs; staleIndex++)
        {
            if (!_customHotkeyComponents.TryGetValue(staleIndex, out ClickableComponent? staleComponent))
                continue;
            page.allClickableComponents.Remove(staleComponent);
            _customHotkeyComponents.Remove(staleIndex);
        }

        foreach ((Rectangle bounds, int index) in _customTabBounds)
        {
            if (!_customHotkeyComponents.TryGetValue(index, out ClickableComponent? component))
            {
                component = new ClickableComponent(bounds, _config.Tabs[index].Name);
                _customHotkeyComponents[index] = component;
            }

            component.name = _config.Tabs[index].Name;
            component.bounds = bounds;
            component.myID = 988000 + index;
            if (!page.allClickableComponents.Contains(component))
                page.allClickableComponents.Add(component);

            var row = index / 10;
            var column = index % 10;
            component.leftNeighborID = column > 0 ? 988000 + index - 1 : ClickableComponent.SNAP_AUTOMATIC;
            component.rightNeighborID = index + 1 < _config.Tabs.Count && (index + 1) / 10 == row
                ? 988000 + index + 1
                : ClickableComponent.SNAP_AUTOMATIC;
            component.upNeighborID = index + 10 < _config.Tabs.Count
                ? 988000 + index + 10
                : ClickableComponent.SNAP_AUTOMATIC;
            component.downNeighborID = row > 0
                ? 988000 + index - 10
                : FindNearestNativeTabId(menu, bounds.Center.X);
        }
    }

    private static int FindNearestNativeTabId(GameMenu menu, int x)
    {
        if (GetNativeTabComponents(menu) is not IList tabs)
            return ClickableComponent.SNAP_AUTOMATIC;
        ClickableComponent? nearest = null;
        int nearestDistance = int.MaxValue;
        foreach (object? item in tabs)
        {
            if (item is not ClickableComponent { visible: true } tab)
                continue;
            int distance = Math.Abs(tab.bounds.Center.X - x);
            if (distance >= nearestDistance)
                continue;
            nearest = tab;
            nearestDistance = distance;
        }
        return nearest?.myID ?? ClickableComponent.SNAP_AUTOMATIC;
    }

    private static object? GetNativeTabComponents(GameMenu menu)
    {
        return GameMenuTabsField?.GetValue(menu);
    }

    private static ClickableComponent? FindNativeTabComponent(GameMenu menu, Rectangle targetBounds)
    {
        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;
            var match = values.OfType<ClickableComponent>()
                .FirstOrDefault(component => component.bounds == targetBounds);
            if (match is not null)
                return match;
        }
        return null;
    }

    private void RemoveAddHotkeyFromSnapGraph(GameMenu? menu)
    {
        if (_snapGraphMenu is null)
            return;

        if (_snapGraphInventoryTab is not null && _snapGraphInventoryTab.leftNeighborID == AddHotkeySnapId)
            _snapGraphInventoryTab.leftNeighborID = _originalInventoryTabLeftNeighborId;
        if (_snapGraphInventoryPage?.allClickableComponents is not null && _controllerAddHotkeyComponent is not null)
            _snapGraphInventoryPage.allClickableComponents.Remove(_controllerAddHotkeyComponent);
        if (_snapGraphInventoryPage?.allClickableComponents is not null)
        {
            foreach (var component in _customHotkeyComponents.Values)
                _snapGraphInventoryPage.allClickableComponents.Remove(component);
        }
        _customHotkeyComponents.Clear();
        if (_addedInventoryTabToClickableSet && _snapGraphInventoryPage?.allClickableComponents is not null && _snapGraphInventoryTab is not null)
            _snapGraphInventoryPage.allClickableComponents.Remove(_snapGraphInventoryTab);

        if (menu is not null && menu != _snapGraphMenu)
            return;

        _snapGraphMenu = null;
        _snapGraphInventoryPage = null;
        _snapGraphInventoryTab = null;
        _addedInventoryTabToClickableSet = false;
        _controllerAddHotkeyFocused = false;

    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not GameMenu gameMenu)
            return;

        if (!IsInventoryPage(gameMenu))
        {
            _pendingEditorReturnFocus = null;
            RemoveAddHotkeyFromSnapGraph(gameMenu);
            _customTabBounds.Clear();
            _plusTabBounds = Rectangle.Empty;
            _layoutDirty = true;
            return;
        }
        EnsureLayout(gameMenu);
        AddHotkeyToSnapGraph(gameMenu, _layoutNativeTabs);
        if (_pendingEditorReturnFocus is int returnFocus)
        {
            _pendingEditorReturnFocus = null;
            if (returnFocus < 0)
                FocusInventoryTab(gameMenu);
            else
                FocusHotkey(gameMenu, returnFocus);
        }
        _controllerAddHotkeyFocused = _controllerAddHotkeyComponent is not null
            && GetInventoryPage(gameMenu)?.currentlySnappedComponent == _controllerAddHotkeyComponent;
        if (_hotkeysVisible)
            DrawCustomTabs(e.SpriteBatch);
        RedrawNativeTabs(e.SpriteBatch, gameMenu);
        DrawPlusTab(e.SpriteBatch);
        DrawTabTooltip(e.SpriteBatch);
    }


}

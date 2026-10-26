using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using System.Collections;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void AddHotkeyToSnapGraph(GameMenu menu, IReadOnlyList<Rectangle> nativeTabs)
    {
        var inventoryPage = GetInventoryPage(menu);
        if (inventoryPage?.allClickableComponents is null)
            return;

        // GameMenu adds the native tab components to every page's clickable
        // list. Search the inventory page, where the actual snap cursor lives.
        var inventoryTab = GetNativeInventoryTab(menu);
        inventoryTab ??= inventoryPage.allClickableComponents
            .FirstOrDefault(component => component.name.Equals("inventory", StringComparison.OrdinalIgnoreCase));
        if (inventoryTab is null && nativeTabs.Count > 0)
            inventoryTab = FindNativeTabComponent(menu, nativeTabs[0]);
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
        _controllerAddHotkeyComponent.leftNeighborID = (GetNativeTabComponents(menu) as IList)?
            .OfType<ClickableComponent>().Where(tab => tab.visible)
            .OrderBy(tab => tab.bounds.Left).LastOrDefault()?.myID ?? inventoryTab.myID;
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

        if (!_hotkeysVisible || _config.HideHotkeysGlobally)
        {
            foreach (var component in _customHotkeyComponents.Values)
                page.allClickableComponents.Remove(component);
            UpdateProfileSnapComponents(menu, page);
            return;
        }

        for (int staleIndex = _customTabBounds.Count; staleIndex < MaxCustomTabs; staleIndex++)
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

            var columns = UseChestGrid ? _chestGridColumns : _config.Columns;
            var row = index / columns;
            var column = index % columns;
            component.leftNeighborID = column > 0 ? 988000 + index - 1
                : _config.EnableProfileButtons ? ProfilePreviousSnapId : ClickableComponent.SNAP_AUTOMATIC;
            component.rightNeighborID = index + 1 < _customTabBounds.Count && (index + 1) / columns == row
                ? 988000 + index + 1
                : _config.EnableProfileButtons ? ProfileNextSnapId : ClickableComponent.SNAP_AUTOMATIC;
            component.upNeighborID = UseChestGrid
                ? row > 0 ? 988000 + index - columns : ClickableComponent.SNAP_AUTOMATIC
                : index + columns < _customTabBounds.Count
                    ? 988000 + index + columns : ClickableComponent.SNAP_AUTOMATIC;
            component.downNeighborID = UseChestGrid
                ? index + columns < _customTabBounds.Count
                    ? 988000 + index + columns : FindNearestNativeTabId(menu, bounds.Center.X)
                : row > 0 ? 988000 + index - columns : FindNearestNativeTabId(menu, bounds.Center.X);
        }
        if (_customTabBounds.Count > 0
            && GetNativeTabComponents(menu) is IList nativeTabs)
        {
            foreach (ClickableComponent nativeTab in nativeTabs
                         .OfType<ClickableComponent>())
            {
                if (!TryGetHotkeyNearestToTab(nativeTab, out int index))
                    continue;

                if (_customHotkeyComponents.TryGetValue(
                        index, out ClickableComponent? hotkey))
                {
                    nativeTab.upNeighborID = hotkey.myID;
                }
            }
        }
        UpdateProfileSnapComponents(menu, page);
    }

    private void UpdateProfileSnapComponents(GameMenu menu, IClickableMenu page)
    {
        if (page.allClickableComponents is null)
            return;
        if (!_config.EnableProfileButtons || !_hotkeysVisible || _config.HideHotkeysGlobally)
        {
            if (_controllerProfilePreviousComponent is not null)
                page.allClickableComponents.Remove(_controllerProfilePreviousComponent);
            if (_controllerProfileNextComponent is not null)
                page.allClickableComponents.Remove(_controllerProfileNextComponent);
            return;
        }

        _controllerProfilePreviousComponent ??= new ClickableComponent(_profilePreviousBounds, "Previous profile")
            { myID = ProfilePreviousSnapId };
        _controllerProfileNextComponent ??= new ClickableComponent(_profileNextBounds, "Next profile")
            { myID = ProfileNextSnapId };
        _controllerProfilePreviousComponent.bounds = _profilePreviousBounds;
        _controllerProfileNextComponent.bounds = _profileNextBounds;
        var rowEdges = _customHotkeyComponents.Values
            .GroupBy(component => component.bounds.Top)
            .Select(group => new
            {
                Left = group.OrderBy(component => component.bounds.Left).First(),
                Right = group.OrderByDescending(component => component.bounds.Right).First()
            }).ToArray();
        _controllerProfilePreviousComponent.leftNeighborID = rowEdges.FirstOrDefault()?.Left.myID
            ?? ClickableComponent.SNAP_AUTOMATIC;
        _controllerProfilePreviousComponent.rightNeighborID = ProfileNextSnapId;
        _controllerProfileNextComponent.leftNeighborID = ProfilePreviousSnapId;
        _controllerProfileNextComponent.rightNeighborID = ProfilePreviousSnapId;
        if (_controllerAddHotkeyComponent is not null)
        {
            _controllerAddHotkeyComponent.upNeighborID = _config.EnableProfileButtons
                ? ProfilePreviousSnapId
                : rowEdges.FirstOrDefault()?.Left.myID ?? ClickableComponent.SNAP_AUTOMATIC;
            _controllerProfilePreviousComponent.downNeighborID = AddHotkeySnapId;
            _controllerProfileNextComponent.downNeighborID = AddHotkeySnapId;
        }
        if (!page.allClickableComponents.Contains(_controllerProfilePreviousComponent))
            page.allClickableComponents.Add(_controllerProfilePreviousComponent);
        if (!page.allClickableComponents.Contains(_controllerProfileNextComponent))
            page.allClickableComponents.Add(_controllerProfileNextComponent);
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
            if (_controllerProfilePreviousComponent is not null)
                _snapGraphInventoryPage.allClickableComponents.Remove(_controllerProfilePreviousComponent);
            if (_controllerProfileNextComponent is not null)
                _snapGraphInventoryPage.allClickableComponents.Remove(_controllerProfileNextComponent);
        }
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

}

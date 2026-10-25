using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace Taums_menu_supreme;

public sealed partial class ModEntry
{
    private void HandleGameMenuButton(ButtonPressedEventArgs e, GameMenu menu)
    {
        if (IsInventoryPage(menu))
        {
            if (HandleInventoryMenuButton(e, menu)) return;
        }

        if (e.Button is SButton.MouseLeft or SButton.MouseRight)
        {
            HandleInventoryPointerButton(e, menu);
        }
    }

    private bool HandleInventoryMenuButton(ButtonPressedEventArgs e, GameMenu menu)
    {
        if ((e.Button is SButton.ControllerY or SButton.MouseMiddle)
            && IsAddHotkeyHighlighted(menu, e.Button == SButton.MouseMiddle))
        {
            SetHotkeysVisible(!_hotkeysVisible);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        var inventoryPage = GetInventoryPage(menu);
        var focusedComponent = inventoryPage?.currentlySnappedComponent;
        if (TryHandleInventoryNavigation(e, menu, focusedComponent)) return true;

        if ((_controllerAddHotkeyFocused
             || (_controllerAddHotkeyComponent is not null
                 && GetInventoryPage(menu)?.currentlySnappedComponent == _controllerAddHotkeyComponent))
            && e.Button == SButton.ControllerA)
        {
            _controllerAddHotkeyFocused = false;
            if (_config.Tabs.Count >= MaxCustomTabs)
            {
                Game1.addHUDMessage(new HUDMessage("You can't add another hotkey. All four rows are full.", HUDMessage.error_type));
            }
            else
            {
                OpenEditor(menu);
            }
            Helper.Input.Suppress(e.Button);
            return true;
        }

        if (_hotkeysVisible && e.Button == SButton.ControllerA
                            && TryGetHotkeyComponentIndex(inventoryPage?.currentlySnappedComponent, out int activatedHotkeyIndex))
        {
            ActivateHotkey(activatedHotkeyIndex);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        if (_hotkeysVisible && e.Button == SButton.ControllerX
                            && TryGetHotkeyComponentIndex(inventoryPage?.currentlySnappedComponent, out int editedHotkeyIndex))
        {
            OpenEditor(menu, editedHotkeyIndex);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        return false;
    }

    private bool TryHandleInventoryNavigation(ButtonPressedEventArgs e, GameMenu menu, ClickableComponent? focusedComponent)
    {
        if (TryGetEditorDirection(e.Button, out var horizontal, out var vertical))
        {
            if (TryGetHotkeyComponentIndex(focusedComponent, out var focusedHotkeyIndex))
            {
                if (horizontal != 0)
                {
                    var destination = focusedHotkeyIndex + horizontal;
                    if (destination >= 0 && destination < _config.Tabs.Count && destination / 10 == focusedHotkeyIndex / 10)
                        FocusHotkey(menu, destination);
                }
                else if (vertical < 0 && focusedHotkeyIndex + 10 < _config.Tabs.Count)
                    FocusHotkey(menu, focusedHotkeyIndex + 10);
                else if (vertical > 0 && focusedHotkeyIndex >= 10)
                    FocusHotkey(menu, focusedHotkeyIndex - 10);
                else if (vertical > 0)
                    FocusNearestNativeTab(menu, focusedComponent!.bounds.Center.X);
                Helper.Input.Suppress(e.Button);
                return true;
            }
//
            // var focusedNativeTab = TryGetNativeTabIndex(menu, focusedComponent, out _)
            //     ? focusedComponent
            //     : TryGetNativeTabIndex(menu, menu.currentlySnappedComponent, out _)
            //         ? menu.currentlySnappedComponent
            //         : null;
            // if (_hotkeysVisible && vertical < 0 && focusedNativeTab is not null
            //     && TryGetHotkeyNearestToTab(focusedNativeTab, out var hotkeyIndex))
            // {
            //     FocusHotkey(menu, hotkeyIndex);
            //     Helper.Input.Suppress(e.Button);
            //     return true;
            // }
            //
            
            
            var focusedNativeTab = TryGetNativeTabIndex(menu, focusedComponent, out _)
                ? focusedComponent
                : TryGetNativeTabIndex(menu, menu.currentlySnappedComponent, out _)
                    ? menu.currentlySnappedComponent
                    : null;

            if (_hotkeysVisible && vertical < 0 && _customTabBounds.Count > 0)
            {
                if (focusedNativeTab is not null
                    && TryGetHotkeyNearestToTab(focusedNativeTab, out var hotkeyIndex))
                {
                    FocusHotkey(menu, hotkeyIndex);
                    Helper.Input.Suppress(e.Button);
                    return true;
                }

                // Fallback: navigate from the top inventory row
                // when neither focus reference is a native tab.
                var currentFocus = focusedComponent ?? menu.currentlySnappedComponent;
                if (currentFocus is not null)
                {
                    var firstRow = _customTabBounds.Take(Math.Min(10, _customTabBounds.Count));

                    var nearest = firstRow
                        .OrderBy(tab => Math.Abs(
                            tab.bounds.Center.X - currentFocus.bounds.Center.X))
                        .First();

                    var nativeTabs = GetNativeTabComponents(menu) as System.Collections.IList;

                    var nativeTabBottom = nativeTabs?
                        .OfType<ClickableComponent>()
                        .Where(tab => tab.visible)
                        .Select(tab => tab.bounds.Bottom)
                        .DefaultIfEmpty(int.MinValue)
                        .Max() ?? int.MinValue;

                    // Only intercept Up when focus is on or above
                    // the native tab strip, not within the inventory grid.
                    if (currentFocus.bounds.Center.Y <= nativeTabBottom)
                    {
                        FocusHotkey(menu, nearest.index);
                        Helper.Input.Suppress(e.Button);
                        return true;
                    }
                }
            }


            if (_hotkeysVisible && vertical > 0 && ReferenceEquals(focusedComponent, _controllerAddHotkeyComponent)
                && _customTabBounds.Count > 0)
            {
                FocusHotkey(menu, 0);
                Helper.Input.Suppress(e.Button);
                return true;
            }
        }

        var isLeftInput = e.Button is SButton.DPadLeft or SButton.LeftThumbstickLeft or SButton.Left;
        // if (isLeftInput)
        // {
        //     var page = GetInventoryPage(menu);
        //     var pageFocus = page?.currentlySnappedComponent;
        //     var menuFocus = menu.currentlySnappedComponent;
        //     Monitor.Log($"Inventory left input '{e.Button}': pageFocus='{pageFocus?.name}'/{pageFocus?.myID} {pageFocus?.bounds}, menuFocus='{menuFocus?.name}'/{menuFocus?.myID} {menuFocus?.bounds}, inventoryTab='{GetNativeInventoryTab(menu)?.name}'/{GetNativeInventoryTab(menu)?.myID} {GetNativeInventoryTab(menu)?.bounds}.", LogLevel.Info);
        // }
        if (isLeftInput && IsInventoryTabFocused(menu))
        {
            FocusAddHotkey(menu);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        return false;
    }

    private void HandleInventoryPointerButton(ButtonPressedEventArgs e, GameMenu menu)
    {
        _controllerAddHotkeyFocused = false;
        var cursor = e.Cursor.GetScaledScreenPixels().ToPoint();
        var x = cursor.X;
        var y = cursor.Y;
        if (e.Button == SButton.MouseLeft && _plusTabBounds.Contains(x, y))
        {
            if (_config.Tabs.Count >= MaxCustomTabs)
            {
                Game1.addHUDMessage(new HUDMessage("You can't add another hotkey. All four rows are full.", HUDMessage.error_type));
                // Monitor.Log($"Blocked add-hotkey request: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
                Helper.Input.Suppress(e.Button);
                return;
            }
            OpenEditor(menu);
            Helper.Input.Suppress(e.Button);
            return;
        }
        if (!_hotkeysVisible)
            return;

        foreach (var tab in _customTabBounds)
        {
            if (!tab.bounds.Contains(x, y)) continue;
            if (e.Button == SButton.MouseRight)
            {
                OpenEditor(menu, tab.index);
                Helper.Input.Suppress(e.Button);
                return;
            }

            ActivateHotkey(tab.index);
            Helper.Input.Suppress(e.Button);
            return;
        }
    }}

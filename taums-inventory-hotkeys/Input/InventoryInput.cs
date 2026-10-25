using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Input;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private bool MatchesAction(StardewModdingAPI.Utilities.KeybindList? bindings, SButton pressed)
    {
        return bindings?.Keybinds.Any(binding => binding.Buttons.Contains(pressed)
            && binding.Buttons.All(button => button == pressed || IsDownForChord(button))) == true;
    }

    private bool IsDownForChord(SButton button)
    {
        var mouse = Mouse.GetState();
        return button switch
        {
            SButton.MouseLeft => mouse.LeftButton == ButtonState.Pressed,
            SButton.MouseRight => mouse.RightButton == ButtonState.Pressed,
            SButton.MouseMiddle => mouse.MiddleButton == ButtonState.Pressed,
            _ => Helper.Input.IsDown(button)
        };
    }

    private void ShowGridFullMessage()
    {
        Game1.addHUDMessage(new HUDMessage($"The maximum of {MaxCustomTabs} saved hotkeys has been reached.", HUDMessage.error_type));
    }

    private void HandleGameMenuButton(ButtonPressedEventArgs e, GameMenu menu)
    {
        if (_config.HideHotkeysGlobally)
            return;
        if (IsInventoryPage(menu))
        {
            if (_config.EnableProfileButtons && e.Button == SButton.MouseLeft)
            {
                EnsureLayout(menu);
                var point = e.Cursor.GetScaledScreenPixels().ToPoint();
                if (_profilePreviousBounds.Contains(point) || _profileNextBounds.Contains(point))
                {
                    SwitchProfile(_profilePreviousBounds.Contains(point) ? -1 : 1);
                    Helper.Input.Suppress(e.Button);
                    return;
                }
            }
            if (e.Button == SButton.MouseLeft && TryBeginChestDrag(e, menu))
                return;
            if (HandleInventoryMenuButton(e, menu)) return;
        }

        if (e.Button.ToString().StartsWith("Mouse", StringComparison.Ordinal))
        {
            HandleInventoryPointerButton(e, menu);
        }
    }

    private bool HandleInventoryMenuButton(ButtonPressedEventArgs e, GameMenu menu)
    {
        if (MatchesAction(_config.OpenSettings, e.Button) && IsAddHotkeyHighlighted(menu, mouseInput: true))
        {
            Helper.Input.Suppress(e.Button);
            if (_gmcmApi is null)
                Game1.addHUDMessage(new HUDMessage("Install Generic Mod Config Menu to open this mod's settings.", HUDMessage.error_type));
            else
            {
                _gmcmControllerNavigationActive = true;
                _waitForSettingsChordRelease = IsControllerButton(e.Button);
                _gmcmVirtualCursorInitialized = false;
                _gmcmApi.OpenModMenu(ModManifest);
                LogDebug("User action: opened mod settings from Add hotkey.");
            }
            return true;
        }

        if ((MatchesAction(_config.ToggleButton, e.Button) || MatchesAction(_config.ToggleMouse, e.Button))
            && IsAddHotkeyHighlighted(menu, e.Button.ToString().StartsWith("Mouse", StringComparison.Ordinal)))
        {
            SetHotkeysVisible(!_hotkeysVisible);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        var inventoryPage = GetInventoryPage(menu);
        var focusedComponent = inventoryPage?.currentlySnappedComponent;
        if (TryHandleInventoryNavigation(e, menu, focusedComponent)) return true;

        if (_config.EnableProfileButtons && MatchesAction(_config.ActivateButton, e.Button))
        {
            if (ReferenceEquals(focusedComponent, _controllerProfilePreviousComponent))
            {
                SwitchProfile(-1);
                Helper.Input.Suppress(e.Button);
                return true;
            }
            if (ReferenceEquals(focusedComponent, _controllerProfileNextComponent))
            {
                SwitchProfile(1);
                Helper.Input.Suppress(e.Button);
                return true;
            }
        }

        if ((_controllerAddHotkeyFocused
             || (_controllerAddHotkeyComponent is not null
                 && GetInventoryPage(menu)?.currentlySnappedComponent == _controllerAddHotkeyComponent))
            && MatchesAction(_config.ActivateButton, e.Button))
        {
            _controllerAddHotkeyFocused = false;
            if (_config.Tabs.Count >= MaxCustomTabs)
            {
                ShowGridFullMessage();
            }
            else
            {
                OpenEditor(menu);
            }
            Helper.Input.Suppress(e.Button);
            return true;
        }

        if (_hotkeysVisible && MatchesAction(_config.ActivateButton, e.Button)
                            && TryGetHotkeyComponentIndex(inventoryPage?.currentlySnappedComponent, out int activatedHotkeyIndex))
        {
            ActivateHotkey(activatedHotkeyIndex);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        if (_hotkeysVisible && MatchesAction(_config.EditButton, e.Button)
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
        bool isLeft = e.Button is SButton.DPadLeft or SButton.LeftThumbstickLeft or SButton.Left;
        bool isRight = e.Button is SButton.DPadRight or SButton.LeftThumbstickRight or SButton.Right;
        if (ReferenceEquals(focusedComponent, _controllerAddHotkeyComponent)
            && e.Button is SButton.DPadUp or SButton.LeftThumbstickUp or SButton.Up)
        {
            if (_config.EnableProfileButtons && _hotkeysVisible)
                FocusProfileSwitcher(menu, next: false);
            else if (!_config.HideHotkeysGlobally && _hotkeysVisible && _customTabBounds.Count > 0)
                FocusHotkey(menu, 0);
            else
                return false;

            Helper.Input.Suppress(e.Button);
            return true;
        }

        if (_config.EnableProfileButtons
            && (ReferenceEquals(focusedComponent, _controllerProfilePreviousComponent)
                || ReferenceEquals(focusedComponent, _controllerProfileNextComponent)))
        {
            if (e.Button is SButton.DPadDown or SButton.LeftThumbstickDown or SButton.Down)
            {
                FocusAddHotkey(menu);
                Helper.Input.Suppress(e.Button);
                return true;
            }
            if (isLeft || isRight)
            {
                // The two profile buttons are a horizontal pair. Always allow
                // left/right to move directly between them before leaving the row.
                var nextIsRight = ReferenceEquals(focusedComponent, _controllerProfilePreviousComponent);
                var moveToOtherProfileButton = nextIsRight ? isRight : isLeft;
                if (moveToOtherProfileButton)
                    FocusProfileSwitcher(menu, next: nextIsRight);
                else
                    FocusHotkeyEdgeFromProfile(menu, rightEdge: !nextIsRight);
                Helper.Input.Suppress(e.Button);
                return true;
            }
        }

        if (_config.EnableProfileButtons && _hotkeysVisible
            && TryGetHotkeyComponentIndex(focusedComponent, out var profileEdgeIndex))
        {
            var focusedHotkey = _customHotkeyComponents[profileEdgeIndex];
            var rowHotkeys = _customHotkeyComponents.Values
                .Where(component => component.bounds.Top == focusedHotkey.bounds.Top)
                .OrderBy(component => component.bounds.Left).ToArray();
            if (isLeft && rowHotkeys.Length > 0 && ReferenceEquals(focusedHotkey, rowHotkeys[0]))
            {
                FocusProfileSwitcher(menu, next: false);
                Helper.Input.Suppress(e.Button);
                return true;
            }
            if (isRight && rowHotkeys.Length > 0 && ReferenceEquals(focusedHotkey, rowHotkeys[^1]))
            {
                FocusProfileSwitcher(menu, next: true);
                Helper.Input.Suppress(e.Button);
                return true;
            }
        }
        if (ReferenceEquals(focusedComponent, _controllerAddHotkeyComponent) && (isLeft || isRight))
        {
            if (isRight)
                FocusInventoryTab(menu);
            else
                FocusLastNativeTab(menu);

            Helper.Input.Suppress(e.Button);
            return true;
        }

        var lastNativeTab = (GetNativeTabComponents(menu) as System.Collections.IList)?
            .OfType<ClickableComponent>().Where(tab => tab.visible)
            .OrderBy(tab => tab.bounds.Left).LastOrDefault();
        if (isRight && lastNativeTab is not null
            && (ReferenceEquals(focusedComponent, lastNativeTab)
                || focusedComponent?.myID == lastNativeTab.myID))
        {
            FocusAddHotkey(menu);
            Helper.Input.Suppress(e.Button);
            return true;
        }

        if (TryGetEditorDirection(e.Button, out var horizontal, out var vertical))
        {
            if (TryGetHotkeyComponentIndex(focusedComponent, out var focusedHotkeyIndex))
            {
                var columns = UseChestGrid ? _chestGridColumns : _config.Columns;
                if (horizontal != 0)
                {
                    var destination = focusedHotkeyIndex + horizontal;
                    if (destination >= 0 && destination < _customTabBounds.Count && destination / columns == focusedHotkeyIndex / columns)
                        FocusHotkey(menu, destination);
                }
                else if (UseChestGrid)
                {
                    if (vertical < 0 && focusedHotkeyIndex >= columns)
                        FocusHotkey(menu, focusedHotkeyIndex - columns);
                    else if (vertical > 0 && focusedHotkeyIndex + columns < _customTabBounds.Count)
                        FocusHotkey(menu, focusedHotkeyIndex + columns);
                    else if (vertical > 0)
                        FocusNearestNativeTab(menu, focusedComponent!.bounds.Center.X);
                }
                else if (vertical < 0 && focusedHotkeyIndex + columns < _customTabBounds.Count)
                    FocusHotkey(menu, focusedHotkeyIndex + columns);
                else if (vertical > 0 && focusedHotkeyIndex >= columns)
                    FocusHotkey(menu, focusedHotkeyIndex - columns);
                else if (vertical > 0)
                    FocusNearestNativeTab(menu, focusedComponent!.bounds.Center.X);
                Helper.Input.Suppress(e.Button);
                return true;
            }
            
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
                    var nearestRowY = _customTabBounds.Max(tab => tab.bounds.Top);
                    var firstRow = _customTabBounds.Where(tab => tab.bounds.Top == nearestRowY);

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


            if (_hotkeysVisible && (UseChestGrid ? vertical < 0 : vertical > 0)
                && ReferenceEquals(focusedComponent, _controllerAddHotkeyComponent)
                && _customTabBounds.Count > 0)
            {
                if (UseChestGrid && TryGetHotkeyNearestToTab(focusedComponent!, out var closestIndex))
                    FocusHotkey(menu, closestIndex);
                else
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
        if (MatchesAction(_config.ActivateMouse, e.Button) && _plusTabBounds.Contains(x, y))
        {
            if (_config.Tabs.Count >= MaxCustomTabs)
            {
                ShowGridFullMessage();
                // Monitor.Log($"Blocked add-hotkey request: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
                Helper.Input.Suppress(e.Button);
                return;
            }
            OpenEditor(menu);
            LogDebug("User action: opened create hotkey editor from Add hotkey controller action.");
            Helper.Input.Suppress(e.Button);
            return;
        }
        if (!_hotkeysVisible)
            return;

        foreach (var tab in _customTabBounds)
        {
            if (!tab.bounds.Contains(x, y)) continue;
            if (MatchesAction(_config.EditMouse, e.Button))
            {
                OpenEditor(menu, tab.index);
                LogDebug($"User action: opened edit hotkey editor for '{_config.Tabs[tab.index].Name}' by mouse.");
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (MatchesAction(_config.ActivateMouse, e.Button))
            {
                ActivateHotkey(tab.index);
                LogDebug($"User action: activated hotkey '{_config.Tabs[tab.index].Name}' by mouse.");
                Helper.Input.Suppress(e.Button);
                return;
            }
        }
    }}

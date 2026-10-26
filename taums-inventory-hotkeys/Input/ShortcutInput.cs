using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private bool SimulateKeybind(KeybindList keybinds)
    {
        // Use SMAPI's input simulator so the game and mods see this through the
        // same input state and keybind processing as a physical press.
        var binding = keybinds.Keybinds.FirstOrDefault();
        if (binding is null)
        {
            // Monitor.Log($"Cannot press keybind: no alternatives in '{keybinds}'.", LogLevel.Warn);
            return false;
        }

        var buttons = binding.Buttons.Distinct().ToArray();
        if (buttons.Length == 0)
        {
            // Monitor.Log($"Cannot press keybind '{binding}': it contains no buttons.", LogLevel.Warn);
            return false;
        }

        foreach (var button in buttons)
        {
            _simulatedButtonsAwaitingDelivery.Add(button);
            _simulatedButtonsAwaitingChangedEvent[button] = 3;
            Helper.Input.Press(button);
        }

        return true;

    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_chestDragSourceIndex is not null
            && (Game1.activeClickableMenu is not GameMenu dragMenu || !IsInventoryPage(dragMenu)
                || !UseChestGrid || !_hotkeysVisible || _config.HideHotkeysGlobally))
            ClearChestDrag();
        else if (_chestDragSourceIndex is not null
                 && !Helper.Input.IsSuppressed(SButton.MouseLeft)
                 && !Helper.Input.IsDown(SButton.MouseLeft))
            FinishChestDrag();
        if (Game1.activeClickableMenu is not InventoryTabEditorMenu and not DeleteConfirmationMenu
            and not HotkeyNamingMenu and not GameMenu)
        {
            _blurredMenuBackground?.Dispose();
            _blurredMenuBackground = null;
        }

        if (_simulatedButtonsAwaitingChangedEvent.Count > 0)
        {
            foreach (var button in _simulatedButtonsAwaitingChangedEvent.Keys.ToArray())
            {
                var ticksRemaining = _simulatedButtonsAwaitingChangedEvent[button] - 1;
                if (ticksRemaining <= 0)
                {
                    _simulatedButtonsAwaitingChangedEvent.Remove(button);
                    // Monitor.Log($"Simulated key did not appear in ButtonsChanged within the expected window: {button}.", LogLevel.Warn);
                }
                else
                    _simulatedButtonsAwaitingChangedEvent[button] = ticksRemaining;
            }
        }

        if (_pendingKeybind is PendingKeybind action)
        {
            if (Game1.activeClickableMenu is not null)
            {
                // Another menu opened before the delay completed; don't send
                // the shortcut into an unrelated modal.
                _pendingKeybind = null;
                // Monitor.Log("Cancelled delayed hotkey shortcut because a menu is still open.", LogLevel.Warn);
            }
            else if (action.TicksRemaining > 1)
                _pendingKeybind = action with { TicksRemaining = action.TicksRemaining - 1 };
            else
            {
                _pendingKeybind = null;
                // Monitor.Log($"Dispatching delayed hotkey shortcut after inventory closed: '{action.Keybinds}'.", LogLevel.Info);
                if (SimulateKeybind(action.Keybinds))
                {
                    Game1.playSound("coin");
                    ShowHotkeyNotification($"Hotkey triggered: {action.Name} ({action.Keybinds})", action.Icon);
                }
            }
        }

        if (_simulatedButtonsAwaitingDelivery.Count > 0)
        {
            foreach (var button in _simulatedButtonsAwaitingDelivery.ToArray())
            {
                if (!Helper.Input.IsDown(button))
                    continue;

                // Monitor.Log($"SMAPI input state reports simulated key down: {button}.", LogLevel.Info);
                _simulatedButtonsAwaitingDelivery.Remove(button);
            }
        }

        if (_gmcmKeybindOverlay is not null
            && _gmcmFinishedProperty?.GetValue(_gmcmKeybindOverlay) is true)
        {
            // Monitor.Log($"GMCM keybind sub-modal closed. Draft keybind='{_draftKeybind}', bound={_draftKeybind.IsBound}.", LogLevel.Info);
            _gmcmKeybindOverlay = null;
        }
    }

    private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
    {
        _useControllerTooltipGlyphs = false;
        var cursor = new Point(Game1.getMouseX(), Game1.getMouseY());
        if (Game1.activeClickableMenu is GameMenu gameMenu && IsInventoryPage(gameMenu)
            && !_config.HideHotkeysGlobally && _hotkeysVisible && _config.EnableProfileButtons)
        {
            EnsureLayout(gameMenu);
            if (_plusTabBounds.Contains(cursor))
            {
                SwitchProfile(e.Delta < 0 ? 1 : -1);
                return;
            }
        }

        if (_confirmingDelete || Game1.activeClickableMenu is not InventoryTabEditorMenu || !_editorIconCarouselBounds.Contains(cursor))
            return;

        CycleDraftIcon(Math.Sign(e.Delta));
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        _useControllerTooltipGlyphs = IsControllerButton(e.Button);

        if (_gmcmKeybindOverlay is not null && Game1.activeClickableMenu is IClickableMenu configMenu
            && IsGmcmConfigMenu(configMenu))
        {
            HandleGmcmOverlayButton(e);
            return;
        }

        if (HandleGmcmActionClick(e))
            return;

        if (!Context.IsWorldReady)
            return;

        if (HandleGmcmConfigControllerButton(e))
            return;

        if (Game1.activeClickableMenu is DeleteConfirmationMenu deleteMenu)
        {
            HandleDeleteConfirmationButton(e, deleteMenu);
            return;
        }

        if (Game1.activeClickableMenu is InventoryTabEditorMenu)
        {
            HandleEditorButton(e);
            return;
        }

        if (Game1.activeClickableMenu is GameMenu menu)
        {
            HandleGameMenuButton(e, menu);
        }
    }
}

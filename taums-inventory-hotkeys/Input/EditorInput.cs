using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace Taums_menu_supreme;

public sealed partial class ModEntry
{
    private void HandleDeleteConfirmationButton(ButtonPressedEventArgs e, DeleteConfirmationMenu deleteMenu)
    {
        if (TryGetEditorDirection(e.Button, out var deleteHorizontal, out var deleteVertical))
            deleteMenu.MoveFocus(deleteHorizontal, deleteVertical);
        else if (e.Button is SButton.ControllerA or SButton.Enter or SButton.Space)
            deleteMenu.ActivateFocused();
        else if (e.Button is SButton.ControllerB or SButton.Escape)
            CancelDeleteConfirmation();
        else if (e.Button == SButton.MouseLeft)
        {
            var click = e.Cursor.GetScaledScreenPixels().ToPoint();
            deleteMenu.receiveLeftClick(click.X, click.Y);
        }

        Helper.Input.Suppress(e.Button);
        return;
    }

    private void HandleEditorButton(ButtonPressedEventArgs e)
    {
        if (Game1.activeClickableMenu is InventoryTabEditorMenu navigatingEditor
            && _gmcmKeybindOverlay is null
            && !_capturingDraftKeybind
            && TryGetEditorDirection(e.Button, out var horizontal, out var vertical))
        {
            navigatingEditor.MoveFocus(horizontal, vertical);
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (e.Button == SButton.ControllerA
            && Game1.activeClickableMenu is InventoryTabEditorMenu nameEditor
            && _gmcmKeybindOverlay is null
            && !_capturingDraftKeybind)
        {
            nameEditor.ActivateFocused();
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (_gmcmKeybindOverlay is not null)
        {
            HandleGmcmOverlayButton(e);
            return;
        }

        if (_capturingDraftKeybind)
        {
            HandleFallbackKeybindButton(e);
            return;
        }

        if (e.Button is SButton.Escape or SButton.ControllerB)
        {
            CloseEditor();
        }
        else if (e.Button == SButton.MouseLeft)
        {
            // Use the same scaled screen coordinates as the inventory-tab
            // click handler, but fall back to Game1's menu coordinates when
            // the active UI scale makes the event cursor differ.
            var cursor = e.Cursor.GetScaledScreenPixels().ToPoint();
            if (!IsEditorInteractivePoint(cursor))
            {
                Point gameCursor = new(Game1.getMouseX(), Game1.getMouseY());
                if (IsEditorInteractivePoint(gameCursor))
                    cursor = gameCursor;
            }
            HandleEditorClick(cursor.X, cursor.Y);
            Helper.Input.Suppress(e.Button);
            return;
        }
        else if (!_nameEditFocused && e.Button is SButton.Left or SButton.Right)
            CycleDraftIcon(e.Button == SButton.Left ? -1 : 1);

        Helper.Input.Suppress(e.Button);
        return;
    }

    private void HandleFallbackKeybindButton(ButtonPressedEventArgs e)
    {
        if (e.Button is SButton.Escape or SButton.ControllerB)
        {
            _capturingDraftKeybind = false;
        }
        else if (e.Button != SButton.MouseLeft)
        {
            _draftKeybind = KeybindList.Parse(e.Button.ToString());
            _capturingDraftKeybind = false;
            // Monitor.Log($"Fallback key capture accepted '{e.Button}'; draft keybind is now '{_draftKeybind}'.", LogLevel.Info);
        }
        Helper.Input.Suppress(e.Button);
        return;
    }

    private void HandleGmcmOverlayButton(ButtonPressedEventArgs e)
    {
        if (e.Button is SButton.Escape or SButton.ControllerB)
        {
            // Monitor.Log($"Cancelled GMCM keybind capture with '{e.Button}'; retained draft keybind='{_draftKeybind}'.", LogLevel.Info);
            _gmcmKeybindOverlay = null;
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (!IsGmcmCapturingKeybind())
        {
            if (TryGetEditorDirection(e.Button, out var overlayHorizontal, out var overlayVertical))
            {
                MoveGmcmFocus(overlayHorizontal, overlayVertical);
                Helper.Input.Suppress(e.Button);
                return;
            }
            if (e.Button == SButton.ControllerA)
            {
                ActivateGmcmFocusedButton();
                _skipGmcmControllerAOnButtonsChanged = true;
                Helper.Input.Suppress(e.Button);
                return;
            }
        }

        if (e.Button == SButton.MouseLeft)
        {
            Point overlayCursor = e.Cursor.GetScaledScreenPixels().ToPoint();
            // Monitor.Log($"Keybind sub-modal click at ({overlayCursor.X},{overlayCursor.Y}); forwarding to GMCM overlay.", LogLevel.Info);
            InvokeGmcmOverlay("OnLeftClick", overlayCursor.X, overlayCursor.Y);
        }
        else if (e.Button is SButton.ControllerA or SButton.ControllerX or SButton.ControllerY
                 or SButton.DPadUp or SButton.DPadDown or SButton.DPadLeft or SButton.DPadRight
                 or SButton.LeftThumbstickUp or SButton.LeftThumbstickDown or SButton.LeftThumbstickLeft or SButton.LeftThumbstickRight)
        {
            // GMCM's keybind overlay is a capture prompt: the controller
            // button becomes the selected binding through ButtonsChanged.
            // Monitor.Log($"Controller input '{e.Button}' received during GMCM capture; forwarding via ButtonsChanged.", LogLevel.Info);
        }
        Helper.Input.Suppress(e.Button);
        return;
    }

}

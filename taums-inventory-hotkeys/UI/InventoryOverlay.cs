using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        DrawGmcmConfigFocus(e.SpriteBatch);
        if (_gmcmKeybindOverlay is not null && Game1.activeClickableMenu is IClickableMenu configMenu
            && IsGmcmConfigMenu(configMenu))
        {
            InvokeGmcmOverlay("Draw", e.SpriteBatch);
            DrawGmcmControllerFocus(e.SpriteBatch);
            if (!Game1.options.hardwareCursor)
                configMenu.drawMouse(e.SpriteBatch, ignore_transparency: true);
        }
        if (Game1.activeClickableMenu is not GameMenu gameMenu)
            return;

        if (_config.HideHotkeysGlobally)
        {
            RemoveAddHotkeyFromSnapGraph(gameMenu);
            _customTabBounds.Clear();
            _plusTabBounds = Rectangle.Empty;
            _layoutDirty = true;
            return;
        }

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
        DrawProfileSwitcher(e.SpriteBatch);
        DrawTabTooltip(e.SpriteBatch);
        DrawHotkeyNotification(e.SpriteBatch);
        DrawChestDragFeedback(e.SpriteBatch);
    }


}

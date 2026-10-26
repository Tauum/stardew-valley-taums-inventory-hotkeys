using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
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

        // Place the add-hotkey button beside the native Inventory tab.
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
        int[] inventoryColumnAnchors = GetInventoryColumnAnchors(menu);
        int[] columnAnchors = inventoryColumnAnchors.Length >= _config.Columns ? inventoryColumnAnchors : nativeColumnAnchors;
        var columnSpacing = GetColumnSpacing(columnAnchors, rowWidth);
        _plusTabBounds = leftmostNativeTab is Rectangle nativeTab
            ? new Rectangle(nativeTab.Left - rowWidth + 28, nativeTab.Y + 8, rowWidth, nativeTab.Height)
            : new Rectangle(nativeLeft - rowWidth + 24, tabY + 6, rowWidth, rowWidth);
        const int profileButtonSize = 48;
        const int profileButtonGap = 4;
        int profileButtonsY = _plusTabBounds.Top - profileButtonSize - 6;
        int nextProfileX = _plusTabBounds.Center.X - profileButtonSize / 2;
        _profileNextBounds = new Rectangle(nextProfileX, profileButtonsY, profileButtonSize, profileButtonSize);
        _profilePreviousBounds = new Rectangle(nextProfileX - profileButtonSize - profileButtonGap,
            profileButtonsY, profileButtonSize, profileButtonSize);
        if (UseChestGrid)
        {
            RepositionChestGrid(menu, nativeTabs, rowWidth);
            if (_config.ProfileSwitcherPosition == "BesideChestGrid" && !_chestGridBounds.IsEmpty)
            {
                var sideSize = Math.Min(64, _chestGridBounds.Height);
                var sideY = _chestGridBounds.Center.Y - sideSize / 2;
                _profilePreviousBounds = new Rectangle(_chestGridBounds.Left - sideSize - 8, sideY, sideSize, sideSize);
                _profileNextBounds = new Rectangle(_chestGridBounds.Right + 8, sideY, sideSize, sideSize);
            }
            RepositionEditorControls(menu);
            return;
        }
        _chestGridBounds = Rectangle.Empty;
        _chestInventoryRows.Clear();
        // The custom row matches the native 64px tabs and overlaps the native
        // row by four pixels, like a stacked tab strip. It is drawn first so
        // the native tabs cover its lower edge.
        var customY = tabY - rowHeight + 4;
        _customTabBounds.Clear();
        int maxPerRow = _config.Columns;
        for (var i = 0; i < Math.Min(_config.Tabs.Count, GridCapacity); i++)
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

    private void RepositionChestGrid(GameMenu menu, IReadOnlyList<Rectangle> nativeTabs, int inventorySlotSize)
    {
        var displayedCount = Math.Min(_config.Tabs.Count, GridCapacity);
        _chestInventoryRows.Clear();
        _customTabBounds.Clear();
        if (displayedCount == 0)
        {
            _chestGridBounds = Rectangle.Empty;
            _chestGridColumns = 0;
            _chestGridRows = 0;
            return;
        }

        _chestGridColumns = Math.Min(displayedCount, _config.Columns);
        _chestGridRows = (displayedCount + _chestGridColumns - 1) / _chestGridColumns;
        var nativeTop = nativeTabs.Count > 0 ? nativeTabs.Min(bounds => bounds.Top) : menu.yPositionOnScreen + 80;
        _chestSlotSize = inventorySlotSize;
        var panelWidth = _chestGridColumns * _chestSlotSize + 28;
        var panelHeight = _chestGridRows * _chestSlotSize + 24;
        var panelX = (int)Math.Round((Game1.uiViewport.Width - panelWidth) / 2f)
            + _config.ChestGridOffsetX;
        const int chestGridGapFromTabs = 100;
        var panelY = Math.Max(8, nativeTop - panelHeight - chestGridGapFromTabs + _config.ChestGridOffsetY);
        _chestGridBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        var nextIndex = 0;
        for (var row = 0; row < _chestGridRows; row++)
        {
            var rowCount = Math.Min(_chestGridColumns, displayedCount - nextIndex);
            var rowWidth = rowCount * _chestSlotSize;
            var rowX = panelX + 14 + (panelWidth - 28 - rowWidth) / 2;
            var rowY = panelY + 12 + row * _chestSlotSize;
            var inventoryRow = new InventoryMenu(rowX, rowY, false, new List<Item>(),
                capacity: rowCount, rows: 1);
            _chestInventoryRows.Add(inventoryRow);
            foreach (var slot in inventoryRow.inventory)
                _customTabBounds.Add((slot.bounds, nextIndex++));
        }
    }

    private void RepositionEditorControls(IClickableMenu menu, bool useViewport = false)
    {
        var viewport = Game1.uiViewport;
        // GMCM menu bounds can be large but refer to its own frame; the editor
        // should always use the same full viewport position as the inventory flow.
        bool useMenuBounds = !useViewport && menu is GameMenu && menu.width > 200 && menu.height > 200;
        int parentX = useMenuBounds ? menu.xPositionOnScreen : viewport.X;
        int parentY = useMenuBounds ? menu.yPositionOnScreen : viewport.Y;
        int parentWidth = useMenuBounds ? menu.width : viewport.Width;
        int parentHeight = useMenuBounds ? menu.height : viewport.Height;
        var editorWidth = Math.Min(760, parentWidth - 48);
        const int confirmationSize = 52;
        const int confirmationGap = 10;
        var editorHeight = Math.Min(680, parentHeight - 32);
        _editorBounds = new Rectangle(parentX + (parentWidth - editorWidth) / 2,
            parentY + (parentHeight - editorHeight) / 2, editorWidth, editorHeight);
        _editorNameBounds = new Rectangle(_editorBounds.X + 184, _editorBounds.Y + 164, _editorBounds.Width - 360, 72);
        _editorNameHitBounds = new Rectangle(_editorNameBounds.X - 48, _editorNameBounds.Y - 12,
            _editorNameBounds.Width + 96, _editorNameBounds.Height + 24);
        _editorDescriptionBounds = new Rectangle(_editorNameBounds.X, _editorBounds.Y + 258, _editorNameBounds.Width, 64);
        _editorDescriptionHitBounds = new Rectangle(_editorDescriptionBounds.X - 48, _editorDescriptionBounds.Y - 8,
            _editorDescriptionBounds.Width + 96, _editorDescriptionBounds.Height + 16);
        _editorIconCarouselBounds = new Rectangle(_editorBounds.X + 32, _editorBounds.Y + 340, _editorBounds.Width - 64, 124);
        // Keep the carousel controls on the right half of the row and reserve
        // clear space at the far right for its position counter.
        var iconGroupCenterX = _editorIconCarouselBounds.Center.X + 8;
        var iconRowCenterY = _editorIconCarouselBounds.Y + 78;
        _editorIconPreviousBounds = new Rectangle(iconGroupCenterX - 104, iconRowCenterY - 24, 48, 48);
        _editorIconBounds = new Rectangle(iconGroupCenterX - 48, iconRowCenterY - 44, 96, 88);
        _editorIconNextBounds = new Rectangle(iconGroupCenterX + 56, iconRowCenterY - 24, 48, 48);
        _editorIconCounterBounds = new Rectangle(_editorIconNextBounds.Right + 12, iconRowCenterY - 30, 80, 60);
        _editorIconDiceBounds = new Rectangle(_editorIconCounterBounds.Right + 8, iconRowCenterY - 22, 44, 44);
        _editorKeybindBounds = new Rectangle(_editorBounds.Right - 360, _editorBounds.Y + 490, 208, 80);
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

}

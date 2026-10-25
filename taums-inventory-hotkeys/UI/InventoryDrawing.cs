using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void DrawPlusTab(SpriteBatch b)
    {
        DrawVanillaTabBackground(b, _plusTabBounds);
        // Use Stardew's Floor TV furniture icon and draw it at inventory-item
        // scale, anchored to the tab's full icon cell so it stays centered.
        _plusTabFloorTvIcon ??= ItemRegistry.Create("(F)1680");
        _plusTabFloorTvIcon.drawInMenu(b, new Vector2(_plusTabBounds.X, _plusTabBounds.Y + 3), 0.675f);
    }

    private void DrawProfileArrow(SpriteBatch b, Rectangle bounds, bool right)
    {
        var hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height, hovered ? Color.Wheat : Color.White, 4f);
        var source = right ? new Rectangle(365, 495, 12, 11) : new Rectangle(352, 495, 12, 11);
        var scale = Math.Min(4f, Math.Min((bounds.Width - 16) / (float)source.Width,
            (bounds.Height - 16) / (float)source.Height));
        var size = new Vector2(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private void DrawProfileSwitcher(SpriteBatch b)
    {
        if (!_config.EnableProfileButtons || !_hotkeysVisible || _config.HideHotkeysGlobally)
            return;
        DrawProfileArrow(b, _profilePreviousBounds, right: false);
        DrawProfileArrow(b, _profileNextBounds, right: true);
    }

    private void DrawCustomTabs(SpriteBatch b)
    {
        if (UseChestGrid)
        {
            DrawChestGrid(b);
            return;
        }
        foreach (var entry in _customTabBounds)
        {
            DrawVanillaTabBackground(b, entry.bounds);
            // The native tab art has a slightly top-heavy silhouette; lower
            // the centered emoji a few pixels to match the native item-icon balance.
            Rectangle iconBounds = new(entry.bounds.X, entry.bounds.Y + 4, entry.bounds.Width, entry.bounds.Height);
            _emojiCatalog.DrawIcon(b, _config.Tabs[entry.index].Icon, iconBounds, maxSize: 38);
        }
    }

    private void DrawChestGrid(SpriteBatch b)
    {
        if (_chestGridBounds.IsEmpty || _chestInventoryRows.Count == 0)
            return;

        IClickableMenu.drawTextureBox(b, _chestGridBounds.X, _chestGridBounds.Y,
            _chestGridBounds.Width, _chestGridBounds.Height, Color.White);
        foreach (var row in _chestInventoryRows)
            row.draw(b);
        foreach (var (slot, index) in _customTabBounds)
            _emojiCatalog.DrawIcon(b, _config.Tabs[index].Icon, slot, maxSize: Math.Min(48, slot.Width - 12));
    }

    private void DrawChestDragFeedback(SpriteBatch b)
    {
        if (!_chestDragActive || _chestDragSourceIndex is not int source
            || source < 0 || source >= _customTabBounds.Count || source >= _config.Tabs.Count)
            return;

        b.Draw(Game1.staminaRect, _customTabBounds[source].bounds, Color.Black * 0.35f);
        if (_chestDragTargetIndex is int destination && destination >= 0
            && destination < _customTabBounds.Count)
        {
            var target = _customTabBounds[destination].bounds;
            const int border = 3;
            var color = new Color(255, 241, 137);
            b.Draw(Game1.staminaRect, new Rectangle(target.X, target.Y, target.Width, border), color);
            b.Draw(Game1.staminaRect, new Rectangle(target.X, target.Bottom - border, target.Width, border), color);
            b.Draw(Game1.staminaRect, new Rectangle(target.X, target.Y, border, target.Height), color);
            b.Draw(Game1.staminaRect, new Rectangle(target.Right - border, target.Y, border, target.Height), color);
        }

        var size = _customTabBounds[source].bounds.Width;
        var ghost = new Rectangle(_chestDragCursor.X - size / 2, _chestDragCursor.Y - size / 2, size, size);
        IClickableMenu.drawTextureBox(b, ghost.X, ghost.Y, ghost.Width, ghost.Height, Color.White * 0.85f);
        _emojiCatalog.DrawIcon(b, _config.Tabs[source].Icon, ghost, maxSize: Math.Min(48, size - 12));
    }

    private static void DrawVanillaTabBackground(SpriteBatch b, Rectangle bounds)
    {
        b.Draw(Game1.mouseCursors, bounds, new Rectangle(16, 368, 16, 16), Color.White);
    }

}

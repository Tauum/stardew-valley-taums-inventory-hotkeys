using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void LogDebug(string message)
    {
        if (_config.EnableDebugLogs)
            Monitor.Log(message, LogLevel.Debug);
    }

    private void ShowHotkeyEvent(string message)
    {
        Monitor.Log(message, LogLevel.Info);
        LogDebug($"User action: {message}");
        Game1.addHUDMessage(new HUDMessage(message));
    }

    private void ShowHotkeyNotification(string message, int? icon = null)
    {
        Monitor.Log(message, LogLevel.Info);
        LogDebug($"User action: {message}");
        _hotkeyNotification = new HotkeyNotification(message, icon, DateTime.UtcNow.AddSeconds(4));
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (Game1.activeClickableMenu is null)
            DrawHotkeyNotification(e.SpriteBatch);
    }

    private void DrawHotkeyNotification(SpriteBatch batch)
    {
        if (_hotkeyNotification is not { } notice)
            return;
        if (DateTime.UtcNow >= notice.ExpiresAt)
        {
            _hotkeyNotification = null;
            return;
        }

        const int height = 72;
        const int iconSize = 42;
        const int padding = 14;
        var width = Math.Min(Game1.uiViewport.Width - 32,
            (int)Math.Ceiling(Game1.smallFont.MeasureString(notice.Message).X) + iconSize + padding * 3);
        var bounds = new Rectangle(16, Game1.uiViewport.Height - height - 16, width, height);
        IClickableMenu.drawTextureBox(batch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        var iconBounds = new Rectangle(bounds.X + padding, bounds.Center.Y - iconSize / 2, iconSize, iconSize);
        if (notice.Icon is int icon)
            _emojiCatalog.DrawIcon(batch, icon, iconBounds, iconSize);
        else
        {
            batch.Draw(Game1.staminaRect, iconBounds, new Color(175, 59, 42));
            var symbol = "!";
            var size = Game1.dialogueFont.MeasureString(symbol);
            batch.DrawString(Game1.dialogueFont, symbol,
                new Vector2(iconBounds.Center.X - size.X / 2, iconBounds.Center.Y - size.Y / 2), Color.White);
        }

        var availableWidth = bounds.Width - iconSize - padding * 3;
        var text = FitText(notice.Message, Game1.smallFont, availableWidth);
        var textSize = Game1.smallFont.MeasureString(text);
        batch.DrawString(Game1.smallFont, text,
            new Vector2(iconBounds.Right + padding, bounds.Center.Y - textSize.Y / 2), Game1.textColor);
    }
}

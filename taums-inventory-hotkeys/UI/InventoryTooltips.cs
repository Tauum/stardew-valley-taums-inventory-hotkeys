using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Taums_menu_supreme;

public sealed partial class ModEntry
{
    private void DrawTabTooltip(SpriteBatch b)
    {
        if (!_hotkeysVisible)
            return;

        if (_controllerAddHotkeyFocused)
        {
            DrawAddHotkeyTooltip(b);
            return;
        }

        var x = Game1.getMouseX();
        var y = Game1.getMouseY();
        if (_plusTabBounds.Contains(x, y))
        {
            DrawAddHotkeyTooltip(b);
            return;
        }
        foreach (var entry in _customTabBounds)
        {
            if (!entry.bounds.Contains(x, y)) continue;
            IClickableMenu.drawHoverText(b, _config.Tabs[entry.index].Name, Game1.smallFont);
            return;
        }
    }

    private void DrawAddHotkeyTooltip(SpriteBatch b)
    {
        const string heading = "Add hotkey";
        var createText = _useControllerTooltipGlyphs ? "Create:" : "Create: Left-click";
        var editText = _useControllerTooltipGlyphs ? "Edit hotkeys:" : "Edit hotkeys: Right-click";
        var toggleText = _hotkeysVisible ? "Hide hotkeys:" : "Show hotkeys:";
        if (!_useControllerTooltipGlyphs)
            toggleText += " Middle-click";
        const int padding = 12;
        const int iconSize = 20;
        const int iconGap = 5;
        const int lineGap = 4;

        var headingSize = Game1.smallFont.MeasureString(heading);
        var createSize = Game1.smallFont.MeasureString(createText);
        var editSize = Game1.smallFont.MeasureString(editText);
        var toggleSize = Game1.smallFont.MeasureString(toggleText);
        var glyphWidth = _useControllerTooltipGlyphs ? iconGap + iconSize : 0;
        var contentWidth = (int)Math.Ceiling(Math.Max(headingSize.X,
            Math.Max(createSize.X + glyphWidth, Math.Max(editSize.X + glyphWidth, toggleSize.X + glyphWidth))));
        var lineHeight = Math.Max(Game1.smallFont.LineSpacing, iconSize);
        var boxWidth = contentWidth + padding * 2;
        var boxHeight = padding * 2 + lineHeight * 4 + lineGap * 3;

        var x = Game1.getMouseX() + 16;
        var y = Game1.getMouseY() + 16;
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        if (x + boxWidth > viewport.Right - 8)
            x = Game1.getMouseX() - boxWidth - 16;
        if (y + boxHeight > viewport.Bottom - 8)
            y = Game1.getMouseY() - boxHeight - 16;
        x = Math.Clamp(x, viewport.Left + 8, viewport.Right - boxWidth - 8);
        y = Math.Clamp(y, viewport.Top + 8, viewport.Bottom - boxHeight - 8);

        IClickableMenu.drawTextureBox(b, x, y, boxWidth, boxHeight, Color.White);
        var textX = x + padding;
        var firstY = y + padding;
        b.DrawString(Game1.smallFont, heading, new Vector2(textX, firstY), Game1.textColor);

        var createY = firstY + lineHeight + lineGap;
        b.DrawString(Game1.smallFont, createText, new Vector2(textX, createY), Game1.textColor);
        if (_useControllerTooltipGlyphs)
            DrawControllerGlyph(b, new Rectangle(textX + (int)Math.Ceiling(createSize.X) + iconGap,
                createY + (lineHeight - iconSize) / 2, iconSize, iconSize), isAButton: true);

        var editY = createY + lineHeight + lineGap;
        b.DrawString(Game1.smallFont, editText, new Vector2(textX, editY), Game1.textColor);
        if (_useControllerTooltipGlyphs)
            DrawControllerGlyph(b, new Rectangle(textX + (int)Math.Ceiling(editSize.X) + iconGap,
                editY + (lineHeight - iconSize) / 2, iconSize, iconSize), isXButton: true);

        var toggleY = editY + lineHeight + lineGap;
        b.DrawString(Game1.smallFont, toggleText, new Vector2(textX, toggleY), Game1.textColor);
        if (_useControllerTooltipGlyphs)
            DrawControllerGlyph(b, new Rectangle(textX + (int)Math.Ceiling(toggleSize.X) + iconGap,
                toggleY + (lineHeight - iconSize) / 2, iconSize, iconSize), isYButton: true);
    }

    private static bool IsControllerButton(SButton button)
    {
        var name = button.ToString();
        return name.StartsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DPad", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Thumbstick", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Trigger", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Shoulder", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawControllerGlyph(SpriteBatch b, Rectangle destination, bool isAButton = false, bool isXButton = false, bool isYButton = false)
    {
        var source = isAButton
            ? new Rectangle(16, 16, 16, 16)
            : isXButton
                ? new Rectangle(32, 16, 16, 16)
                : isYButton
                    ? new Rectangle(64, 16, 16, 16)
                    : new Rectangle(48, 16, 16, 16);
        b.Draw(Game1.mouseCursors, destination, source, Color.White);
    }

}

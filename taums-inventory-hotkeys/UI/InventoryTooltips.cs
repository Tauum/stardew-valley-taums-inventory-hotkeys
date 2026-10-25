using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void DrawTabTooltip(SpriteBatch b)
    {
        if (_chestDragActive)
            return;
        if (_controllerAddHotkeyFocused)
        {
            DrawAddHotkeyTooltip(b);
            return;
        }

        var x = Game1.getMouseX();
        var y = Game1.getMouseY();
        if (_config.EnableProfileButtons && (_profilePreviousBounds.Contains(x, y) || _profileNextBounds.Contains(x, y)))
        {
            IClickableMenu.drawHoverText(b, $"Hotkey Profile ({GetProfilePositionText()})", Game1.smallFont);
            return;
        }
        if (_plusTabBounds.Contains(x, y))
        {
            DrawAddHotkeyTooltip(b);
            return;
        }
        if (!_hotkeysVisible)
            return;

        foreach (var entry in _customTabBounds)
        {
            if (!entry.bounds.Contains(x, y)) continue;
            var tab = _config.Tabs[entry.index];
            DrawHotkeyTooltip(b, entry.bounds, tab.Name, tab.Description);
            return;
        }
    }

    private static void DrawHotkeyTooltip(SpriteBatch b, Rectangle anchor, string title, string? description)
    {
        const int padding = 16;
        const int maxTextWidth = 320;
        const int gap = 6;
        var viewport = Game1.uiViewport;
        var titleText = Game1.parseText(title, Game1.dialogueFont, maxTextWidth);
        var descriptionText = string.IsNullOrWhiteSpace(description)
            ? ""
            : Game1.parseText(description, Game1.smallFont, maxTextWidth);
        var titleSize = Game1.dialogueFont.MeasureString(titleText);
        var descriptionSize = descriptionText.Length == 0
            ? Vector2.Zero
            : Game1.smallFont.MeasureString(descriptionText);
        var width = Math.Min(viewport.Width - 16,
            (int)Math.Ceiling(Math.Max(titleSize.X, descriptionSize.X)) + padding * 2);
        var height = padding * 2 + (int)Math.Ceiling(titleSize.Y)
            + (descriptionText.Length == 0 ? 0 : gap + (int)Math.Ceiling(descriptionSize.Y));
        var x = Math.Clamp(anchor.Center.X - width / 2, viewport.X + 8,
            viewport.X + viewport.Width - width - 8);
        var y = Math.Max(viewport.Y + 8, anchor.Top - height - 8);

        IClickableMenu.drawTextureBox(b, x, y, width, height, Color.White);
        b.DrawString(Game1.dialogueFont, titleText, new Vector2(x + padding, y + padding), Game1.textColor);
        if (descriptionText.Length > 0)
            b.DrawString(Game1.smallFont, descriptionText,
                new Vector2(x + padding, y + padding + titleSize.Y + gap), Game1.textColor);
    }

    private void DrawAddHotkeyTooltip(SpriteBatch b)
    {
        (string Label, KeybindList?[] Bindings)[] lines =
        {
            ("Create / Activate:", new[] { _config.ActivateButton, _config.ActivateMouse }),
            ("Edit hotkey:", new[] { _config.EditButton, _config.EditMouse }),
            ($"{(_hotkeysVisible ? "Hide" : "Show")} hotkeys:", new[] { _config.ToggleButton, _config.ToggleMouse }),
            ("Mod settings:", new[] { _config.OpenSettings })
        };
        const int padding = 12;
        const int lineGap = 4;
        var lineHeight = Game1.smallFont.LineSpacing;
        var labelWidth = lines.Max(line => Game1.smallFont.MeasureString(line.Label).X);
        var bindingsWidth = lines.Max(line => MeasureBindings(line.Bindings));
        var boxWidth = (int)Math.Ceiling(padding * 2 + labelWidth + 10 + bindingsWidth);
        boxWidth = Math.Min(boxWidth, Game1.uiViewport.Width - 16);
        var boxHeight = padding * 2 + lineHeight * (lines.Length + 1) + lineGap * lines.Length;

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
        b.DrawString(Game1.smallFont, "Add hotkey", new Vector2(x + padding, y + padding), Game1.textColor);
        var labelX = x + padding;
        var bindingsX = labelX + (int)Math.Ceiling(labelWidth) + 10;
        var firstY = y + padding + lineHeight + lineGap;
        for (int i = 0; i < lines.Length; i++)
        {
            var lineY = firstY + i * (lineHeight + lineGap);
            b.DrawString(Game1.smallFont, lines[i].Label, new Vector2(labelX, lineY), Game1.textColor);
            DrawBindings(b, lines[i].Bindings, bindingsX, lineY);
        }
    }

    private float MeasureBindings(IEnumerable<KeybindList?> bindings)
    {
        var alternatives = bindings.Where(binding => binding?.IsBound == true)
            .SelectMany(binding => binding!.Keybinds).DistinctBy(binding => binding.ToString()).ToArray();
        if (alternatives.Length == 0)
            return Game1.smallFont.MeasureString("Unbound").X;
        var width = 0f;
        for (int i = 0; i < alternatives.Length; i++)
        {
            if (i > 0)
                width += Game1.smallFont.MeasureString(" / ").X;
            var buttons = alternatives[i].Buttons.ToArray();
            for (int j = 0; j < buttons.Length; j++)
            {
                if (j > 0)
                    width += Game1.smallFont.MeasureString(" + ").X;
                width += IsFaceButton(buttons[j]) ? 26 : Game1.smallFont.MeasureString(FormatButton(buttons[j])).X;
            }
        }
        return width;
    }

    private void DrawBindings(SpriteBatch b, IEnumerable<KeybindList?> bindings, int x, int y)
    {
        var alternatives = bindings.Where(binding => binding?.IsBound == true)
            .SelectMany(binding => binding!.Keybinds).DistinctBy(binding => binding.ToString()).ToArray();
        if (alternatives.Length == 0)
        {
            b.DrawString(Game1.smallFont, "Unbound", new Vector2(x, y), Game1.textColor);
            return;
        }

        float cursorX = x;
        for (int i = 0; i < alternatives.Length; i++)
        {
            if (i > 0)
                DrawBindingSeparator(b, ref cursorX, y, " / ");
            var buttons = alternatives[i].Buttons.ToArray();
            for (int j = 0; j < buttons.Length; j++)
            {
                if (j > 0)
                    DrawBindingSeparator(b, ref cursorX, y, " + ");
                if (IsFaceButton(buttons[j]))
                {
                    DrawFaceButtonGlyph(b, buttons[j], new Rectangle((int)cursorX, y + 1, 22, 22));
                    cursorX += 26;
                }
                else
                {
                    var text = FormatButton(buttons[j]);
                    b.DrawString(Game1.smallFont, text, new Vector2(cursorX, y), Game1.textColor);
                    cursorX += Game1.smallFont.MeasureString(text).X;
                }
            }
        }
    }

    private void DrawBindingSeparator(SpriteBatch b, ref float cursorX, int y, string text)
    {
        b.DrawString(Game1.smallFont, text, new Vector2(cursorX, y), Game1.textColor);
        cursorX += Game1.smallFont.MeasureString(text).X;
    }

    private static bool IsFaceButton(SButton button) => button is SButton.ControllerA or SButton.ControllerB
        or SButton.ControllerX or SButton.ControllerY;

    private string FormatButton(SButton button) => button.ToString() switch
    {
        "MouseLeft" => "Left click",
        "MouseRight" => "Right click",
        "MouseMiddle" => "Middle click",
        _ when IsFaceButton(button) => GetControllerButtonDisplay(button),
        _ => button.ToString()
    };

    private string GetControllerButtonDisplay(SButton button)
    {
        return button switch
        {
            SButton.ControllerA => "A",
            SButton.ControllerB => "B",
            SButton.ControllerX => "X",
            _ => "Y"
        };
    }

    private void DrawFaceButtonGlyph(SpriteBatch b, SButton button, Rectangle bounds)
    {
        var source = button switch
        {
            SButton.ControllerA => new Rectangle(16, 16, 16, 16),
            SButton.ControllerX => new Rectangle(32, 16, 16, 16),
            SButton.ControllerY => new Rectangle(64, 16, 16, 16),
            _ => new Rectangle(48, 16, 16, 16)
        };
        b.Draw(Game1.mouseCursors, bounds, source, Color.White);
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

}

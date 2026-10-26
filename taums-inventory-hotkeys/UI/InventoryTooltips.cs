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
        _mouseHoverStateUpdated = true;
        _mouseHoveringPlusTab = false;
        _mouseHoveredHotkeyIndex = null;
        _mouseHoveredProfileButton = 0;
        if (_chestDragActive)
            return;
        if (_controllerAddHotkeyFocused)
        {
            DrawAddHotkeyTooltip(b);
            return;
        }

        var x = Game1.getMouseX();
        var y = Game1.getMouseY();
        if (_config.EnableProfileButtons && _profilePreviousBounds.Contains(x, y))
        {
            _mouseHoveredProfileButton = -1;
            IClickableMenu.drawHoverText(b, $"Hotkey Profile ({GetProfilePositionText()})", Game1.smallFont);
            return;
        }
        if (_config.EnableProfileButtons && _profileNextBounds.Contains(x, y))
        {
            _mouseHoveredProfileButton = 1;
            IClickableMenu.drawHoverText(b, $"Hotkey Profile ({GetProfilePositionText()})", Game1.smallFont);
            return;
        }
        if (_plusTabBounds.Contains(x, y))
        {
            _mouseHoveringPlusTab = true;
            DrawAddHotkeyTooltip(b);
            return;
        }
        if (!_hotkeysVisible)
            return;

        foreach (var entry in _customTabBounds)
        {
            if (!entry.bounds.Contains(x, y)) continue;
            _mouseHoveredHotkeyIndex = entry.index;
            var tab = _config.Tabs[entry.index];
            DrawHotkeyTooltip(b, tab.Name, tab.Description);
            return;
        }

        if (!_useControllerTooltipGlyphs || !_hotkeysVisible)
            return;

        if (Game1.activeClickableMenu is not GameMenu gameMenu)
            return;
        var focused = GetInventoryPage(gameMenu)?.currentlySnappedComponent;
        if (focused is null || !TryGetHotkeyComponentIndex(focused, out var focusedIndex)
            || focusedIndex < 0 || focusedIndex >= _config.Tabs.Count)
            return;

        var focusedTab = _config.Tabs[focusedIndex];
        DrawHotkeyTooltip(b, focusedTab.Name, focusedTab.Description);
    }

    private static void DrawHotkeyTooltip(SpriteBatch b, string title, string? description)
    {
        var text = string.IsNullOrWhiteSpace(description) ? title : $"{title}\n{description}";
        IClickableMenu.drawHoverText(b, text, Game1.smallFont);
    }

    private void DrawAddHotkeyTooltip(SpriteBatch b)
    {
        (string Label, KeybindList?[] Bindings)[] lines =
        {
            ("Create:", new[] { _config.ActivateButton, _config.ActivateMouse }),
            ("Edit:", new[] { _config.EditButton, _config.EditMouse }),
            ($"{(_hotkeysVisible ? "Hide" : "Show")}:", new[] { _config.ToggleButton, _config.ToggleMouse })
        };
        var showProfileControls = _config.EnableProfileButtons;
        var tooltipLines = new List<string> { "Add hotkey" };
        tooltipLines.AddRange(lines.Select(line =>
            $"{line.Label} {string.Join(" / ", FormatBindings(line.Bindings))}"));
        if (showProfileControls)
            tooltipLines.Add("Profile L1  R1 / Scroll");
        tooltipLines.Add($"Settings: {string.Join(" / ", FormatBindings(new[] { _config.OpenSettings }))}");
        IClickableMenu.drawHoverText(b, string.Join("\n", tooltipLines), Game1.smallFont);
    }

    private IEnumerable<string> FormatBindings(IEnumerable<KeybindList?> bindings)
    {
        var alternatives = bindings.Where(binding => binding?.IsBound == true)
            .SelectMany(binding => binding!.Keybinds).DistinctBy(binding => binding.ToString()).ToArray();
        if (alternatives.Length == 0)
            return new[] { "Unbound" };

        return alternatives.Select(alternative => string.Join(" + ", alternative.Buttons.Select(button =>
            IsFaceButton(button) ? GetControllerButtonDisplay(button) : FormatButton(button))));
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
        "LeftControl" or "RightControl" => "Ctrl",
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

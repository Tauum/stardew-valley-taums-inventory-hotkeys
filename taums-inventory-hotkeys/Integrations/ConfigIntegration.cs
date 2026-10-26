using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System.Reflection;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void SaveConfig()
    {
        _config.Normalize();
        _layoutDirty = true;
        SaveActiveProfile();
        Helper.WriteConfig(_config);
        // Monitor.Log($"Persisted hotkey _config: count={_config.Tabs.Count}; {string.Join(" | ", _config.Tabs.Select((tab, index) => $"[{index}] '{tab.Name}' iconId={tab.Icon} keybind='{tab.Keybinds}'"))}.", LogLevel.Info);
    }

    private void RegisterGenericModConfigMenu()
    {
        _gmcmActionBounds.Clear();
        _gmcmControlBounds.Clear();
        _gmcmControlActions.Clear();
        _gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_gmcmApi is null)
        {
            // Monitor.Log("Generic Mod Config Menu isn't installed; hotkey shortcuts can be added after installing it.", LogLevel.Trace);
            return;
        }

        // Monitor.Log("Connected to Generic Mod Config Menu API; registering hotkey keybind options.", LogLevel.Info);
        _gmcmApi.Unregister(ModManifest);
        _gmcmApi.Register(ModManifest, ResetConfig, SaveGmcmConfig);
        _selectedHotkeyConfigIndex = Math.Clamp(_selectedHotkeyConfigIndex, 0, Math.Max(0, _config.Tabs.Count - 1));
        _gmcmApi.AddTextOption(ModManifest,
            () => _config.ActiveProfile.ToString(),
            SelectProfileFromConfigMenu,
            () => "Active profile",
            () => "Choose a saved hotkey profile. Reopen settings after switching to refresh the shortcut list.",
            allowedValues: GetCreatedProfileNumbers().Select(index => index.ToString()).ToArray(),
            formatAllowedValue: value => $"Profile {value}");
        AddProfileActions();
        _gmcmApi.AddBoolOption(ModManifest, () => _config.HideHotkeysGlobally,
            value => { _config.HideHotkeysGlobally = value; _layoutDirty = true; },
            () => "Globally hide", () => "Hide the hotkey tabs and Add hotkey button in the inventory. Off by default.");
        _gmcmApi.AddBoolOption(ModManifest, () => _config.EnableProfileButtons,
            value => { _config.EnableProfileButtons = value; _layoutDirty = true; },
            () => "Profile buttons", () => "Show arrows beside Add hotkey to switch between saved hotkey profiles. On by default.");
        _gmcmApi.AddBoolOption(ModManifest, () => _config.EnableDebugLogs,
            value => _config.EnableDebugLogs = value,
            () => "Debug logs", () => "Write detailed diagnostic messages to the SMAPI console. Off by default.");
        _gmcmApi.AddTextOption(ModManifest,
            () => _config.ProfileSwitcherPosition,
            value => { _config.ProfileSwitcherPosition = value; _layoutDirty = true; },
            () => "Profile switcher position",
            () => "Choose whether profile arrows appear above the Create hotkey icon or beside the chest-style grid.",
            allowedValues: new[] { "AboveAdd", "BesideChestGrid" },
            formatAllowedValue: value => value == "BesideChestGrid" ? "Beside chest grid" : "Above Create icon");
        _gmcmApi.AddSectionTitle(ModManifest, () => "Menu appearance");
        _gmcmApi.AddBoolOption(ModManifest, () => _config.BlurEnabled,
            value => _config.BlurEnabled = value,
            () => "Blur background", () => "Blur the game behind this mod's menus.");
        _gmcmApi.AddNumberOption(ModManifest, () => _config.BlurOpacity,
            value => _config.BlurOpacity = value,
            () => "Blur opacity", () => "Opacity of the blurred background (0% shows the normal background).",
            min: 0, max: 100, interval: 5, formatValue: value => $"{value}%");
        _gmcmApi.AddSectionTitle(ModManifest, () => "Hotkey grid");
        _gmcmApi.AddTextOption(ModManifest,
            () => _config.HotkeyStyle,
            value => { _config.HotkeyStyle = value; _layoutDirty = true; },
            () => "Hotkey style",
            () => "Choose inventory tabs or a centered chest-style grid above the inventory.",
            allowedValues: new[] { "InventoryTabs", "ChestGrid" },
            formatAllowedValue: value => value == "ChestGrid" ? "Chest-style grid" : "Inventory tabs");
        _gmcmApi.AddBoolOption(ModManifest, () => _config.EnableHotkeyMoving,
            value => _config.EnableHotkeyMoving = value,
            () => "Allow moving hotkeys", () => "Allow hotkeys to be dragged to a new order in chest style.");
        _gmcmApi.AddNumberOption(ModManifest, () => _config.Columns,
            value => { _config.Columns = value; _layoutDirty = true; },
            () => "Columns", () => "Maximum hotkeys per row. The chest-style grid narrows for smaller collections; hidden saved hotkeys remain in the shortcut list.",
            min: 2, max: 10, interval: 1);
        _gmcmApi.AddNumberOption(ModManifest, () => _config.Rows,
            value => { _config.Rows = value; _layoutDirty = true; },
            () => "Rows", () => "Displayed row count and chest-grid height (up to four). Changing this never deletes saved hotkeys.",
            min: 1, max: 4, interval: 1);
        _gmcmApi.AddNumberOption(ModManifest, () => _config.ChestGridOffsetX,
            value => { _config.ChestGridOffsetX = value; _layoutDirty = true; },
            () => "Chest grid X offset", () => "Move the chest-style hotkey grid left or right from its centered position.",
            min: -4000, max: 4000, interval: 8);
        _gmcmApi.AddNumberOption(ModManifest, () => _config.ChestGridOffsetY,
            value => { _config.ChestGridOffsetY = value; _layoutDirty = true; },
            () => "Chest grid Y offset", () => "Move the chest-style hotkey grid up or down from its default position.",
            min: -4000, max: 4000, interval: 8);
        _gmcmApi.AddSectionTitle(ModManifest, () => "Inventory controls");
        AddCombinedControl("Activate / Add", () => _config.ActivateButton, value => _config.ActivateButton = value,
            () => _config.ActivateMouse, value => _config.ActivateMouse = value);
        AddCombinedControl("Edit", () => _config.EditButton, value => _config.EditButton = value,
            () => _config.EditMouse, value => _config.EditMouse = value);
        AddCombinedControl("Show / Hide", () => _config.ToggleButton, value => _config.ToggleButton = value,
            () => _config.ToggleMouse, value => _config.ToggleMouse = value);
        AddSettingsControl();
        _gmcmApi.AddParagraph(ModManifest, () =>
            "Highlight Add hotkey to use Activate / Add, Show / Hide, or Open mod settings. " +
            "Highlight a saved hotkey to activate or edit it. For mouse bindings, hover over the matching tab first. " +
            "In chest style, when moving is enabled, hold left click and drag a hotkey to reorder it.");
    }

    private void AddCombinedControl(string label, Func<KeybindList> getKeyboardAndController,
        Action<KeybindList> setKeyboardAndController, Func<KeybindList> getMouse, Action<KeybindList> setMouse)
    {
        var leftId = $"{label}:left";
        var rightId = $"{label}:right";
        KeybindList LeftBindings() => new(getKeyboardAndController().Keybinds
            .Where(binding => !binding.Buttons.Any(IsControllerButton))
            .Concat(getMouse().Keybinds).ToArray());
        KeybindList RightBindings() => new(getKeyboardAndController().Keybinds
            .Where(binding => binding.Buttons.Any(IsControllerButton)).ToArray());

        _gmcmControlActions[leftId] = () => OpenKeybindOverlay(LeftBindings(), $"{label}: keyboard / mouse", value =>
        {
            setMouse(new KeybindList(value.Keybinds.Where(binding => binding.Buttons.Any(button =>
                button.ToString().StartsWith("Mouse", StringComparison.Ordinal))).ToArray()));
            var keyboard = value.Keybinds.Where(binding => !binding.Buttons.Any(button =>
                button.ToString().StartsWith("Mouse", StringComparison.Ordinal)));
            setKeyboardAndController(new KeybindList(keyboard.Concat(RightBindings().Keybinds).ToArray()));
        });
        _gmcmControlActions[rightId] = () => OpenKeybindOverlay(RightBindings(), $"{label}: controller", value =>
        {
            var keyboard = getKeyboardAndController().Keybinds.Where(binding => !binding.Buttons.Any(IsControllerButton));
            setKeyboardAndController(new KeybindList(keyboard.Concat(value.Keybinds).ToArray()));
        });

        _gmcmApi!.AddComplexOption(ModManifest, () => label, (batch, position) =>
        {
            var x = (int)position.X;
            var y = (int)position.Y;
            var left = new Rectangle(x, y, 220, 60);
            var right = new Rectangle(x + 234, y, 220, 60);
            DrawConfigKeybindButton(batch, left, LeftBindings());
            DrawConfigKeybindButton(batch, right, RightBindings(), controller: true);
            _gmcmControlBounds[leftId] = left;
            _gmcmControlBounds[rightId] = right;
        }, tooltip: () => "Left: keyboard / mouse. Right: controller. Select either binding to edit it.",
            height: () => 70, fieldId: label);
    }

    private void AddSettingsControl()
    {
        const string leftId = "Open mod settings:left";
        const string rightId = "Open mod settings:right";
        KeybindList LeftBindings() => new(_config.OpenSettings.Keybinds
            .Where(binding => !binding.Buttons.Any(IsControllerButton)).ToArray());
        KeybindList RightBindings() => new(_config.OpenSettings.Keybinds
            .Where(binding => binding.Buttons.Any(IsControllerButton)).ToArray());

        _gmcmControlActions[leftId] = () => OpenKeybindOverlay(LeftBindings(), "Open mod settings: keyboard / mouse",
            value => _config.OpenSettings = new KeybindList(value.Keybinds
                .Where(binding => !binding.Buttons.Any(IsControllerButton))
                .Concat(RightBindings().Keybinds).ToArray()));
        _gmcmControlActions[rightId] = () => OpenKeybindOverlay(RightBindings(), "Open mod settings: controller",
            value => _config.OpenSettings = new KeybindList(LeftBindings().Keybinds
                .Concat(value.Keybinds.Where(binding => binding.Buttons.Any(IsControllerButton))).ToArray()));

        _gmcmApi!.AddComplexOption(ModManifest, () => "Open mod settings", (batch, position) =>
        {
            var x = (int)position.X;
            var y = (int)position.Y;
            var left = new Rectangle(x, y, 220, 60);
            var right = new Rectangle(x + 234, y, 220, 60);
            DrawConfigKeybindButton(batch, left, LeftBindings());
            DrawConfigKeybindButton(batch, right, RightBindings(), controller: true);
            _gmcmControlBounds[leftId] = left;
            _gmcmControlBounds[rightId] = right;
        }, tooltip: () => "Select the left keyboard / mouse button or right controller button to edit it. Open settings while Add hotkey is highlighted or hovered.",
            height: () => 70, fieldId: "open-mod-settings");
    }

    private void DrawConfigKeybindButton(SpriteBatch batch, Rectangle bounds, KeybindList bindings, bool controller = false)
    {
        if (!controller)
        {
            DrawSettingsButton(batch, bounds, bindings.IsBound ? FormatSettingsBindings(bindings) : "",
                Game1.smallFont, 24);
            return;
        }

        DrawSettingsButton(batch, bounds, "", Game1.smallFont, 24);
        if (!bindings.IsBound)
            return;

        var tokens = new List<(SButton? Button, string Text, int Width)>();
        foreach (var keybind in bindings.Keybinds)
        {
            if (tokens.Count > 0)
                tokens.Add((null, "/", (int)Game1.smallFont.MeasureString("/").X));
            var buttons = keybind.Buttons.ToArray();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i > 0)
                    tokens.Add((null, "+", (int)Game1.smallFont.MeasureString("+").X));
                var button = buttons[i];
                var text = IsFaceButton(button) ? "" : button.ToString();
                tokens.Add((IsFaceButton(button) ? button : null, text,
                    IsFaceButton(button) ? 32 : (int)Game1.smallFont.MeasureString(text).X));
            }
        }

        const int tokenGap = 7;
        int totalWidth = tokens.Sum(token => token.Width) + Math.Max(0, tokens.Count - 1) * tokenGap;
        float cursorX = bounds.Center.X - totalWidth / 2f;
        foreach (var token in tokens)
        {
            if (token.Button is SButton faceButton)
            {
                DrawFaceButtonGlyph(batch, faceButton,
                    new Rectangle((int)cursorX, bounds.Center.Y - 16, 32, 32));
            }
            else if (!string.IsNullOrEmpty(token.Text))
            {
                var textSize = Game1.smallFont.MeasureString(token.Text);
                batch.DrawString(Game1.smallFont, token.Text,
                    new Vector2(cursorX, bounds.Center.Y - textSize.Y / 2f), Game1.textColor);
            }
            cursorX += token.Width + tokenGap;
        }
    }

    private string FormatSettingsBindings(KeybindList bindings) => string.Join(", ",
        bindings.Keybinds.Select(keybind => string.Join(" + ", keybind.Buttons.Select(button => button.ToString() switch
        {
            "MouseLeft" => "Left click",
            "MouseRight" => "Right click",
            "MouseMiddle" => "Middle click",
            _ when IsFaceButton(button) => GetControllerButtonDisplay(button),
            _ => button.ToString()
        }))));

    private void AddProfileActions()
    {
        _gmcmApi!.AddComplexOption(ModManifest, () => "Profile actions", (batch, position) =>
        {
            var x = (int)position.X;
            var y = (int)position.Y;
            // Use most of the row width so GMCM doesn't elide these labels.
            var createBounds = new Rectangle(x + 50, y, 260, 60);
            var deleteBounds = new Rectangle(createBounds.Right + 12, y, 260, 60);
            DrawConfigActionButton(batch, createBounds, "Create profile");
            DrawConfigActionButton(batch, deleteBounds, "Delete active profile");
            _gmcmActionBounds["create-profile"] = createBounds;
            _gmcmActionBounds["delete-profile"] = deleteBounds;
        }, height: () => 70, fieldId: "profile-actions");
    }

    private static void DrawConfigActionButton(SpriteBatch batch, Rectangle bounds, string label)
    {
        DrawSettingsButton(batch, bounds, label, Game1.dialogueFont, 16);
    }

    private static void DrawSettingsButton(SpriteBatch batch, Rectangle bounds, string label, SpriteFont font, int horizontalPadding)
    {
        // Match the Edit keybind button in Add Hotkey: vanilla cursor-sheet 9-slice,
        // wheat hover tint, dialogue-style shadowed text, and its vertical offset.
        var hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(batch, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height, hovered ? Color.Wheat : Color.White, 4f);
        var fitted = FitText(label, font, bounds.Width - horizontalPadding);
        var textPosition = new Vector2(bounds.Center.X, bounds.Center.Y + 4) - font.MeasureString(fitted) / 2f;
        Utility.drawTextWithShadow(batch, fitted, font, textPosition, Game1.textColor, 1f, -1f, -1, -1, 0f);
    }

    private bool HandleGmcmActionClick(ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft || Game1.activeClickableMenu is not IClickableMenu menu
            || !IsGmcmConfigMenu(menu))
            return false;

        // GMCM's open dropdown is drawn over rows below it. Let it consume the
        // click before any of our Edit/Delete buttons underneath can respond.
        if (!_gmcmDropdownTypeResolved)
        {
            _gmcmDropdownType = menu.GetType().Assembly.GetTypes().FirstOrDefault(type => type.Name == "Dropdown");
            _gmcmDropdownTypeResolved = true;
        }
        var point = e.Cursor.GetScaledScreenPixels().ToPoint();
        var gamePoint = new Point(Game1.getMouseX(), Game1.getMouseY());
        if (IsClickInsideGmcmDropdown(point, gamePoint))
            return false;

        foreach (var (id, bounds) in _gmcmControlBounds.ToArray())
        {
            if ((!bounds.Contains(point) && !bounds.Contains(gamePoint))
                || !_gmcmControlActions.TryGetValue(id, out var open))
                continue;
            Helper.Input.Suppress(e.Button);
            open();
            return true;
        }
        foreach (var (id, bounds) in _gmcmActionBounds.ToArray())
        {
            if (!bounds.Contains(point) && !bounds.Contains(gamePoint))
                continue;
            Helper.Input.Suppress(e.Button);
            SaveGmcmConfig();
            if (id == "create-profile")
            {
                CreateProfileFromConfigMenu();
                return true;
            }
            if (id == "delete-profile")
            {
                DeleteActiveProfileFromConfigMenu();
                return true;
            }
        }
        return false;
    }

    private bool IsClickInsideGmcmDropdown(Point firstPoint, Point secondPoint)
    {
        if (_gmcmDropdownType is null)
            return false;

        const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        object? dropdown = _gmcmDropdownType.GetField("ActiveDropdown", staticFlags)?.GetValue(null)
            ?? _gmcmDropdownType.GetProperty("ActiveDropdown", staticFlags)?.GetValue(null);
        if (dropdown is null)
            return false;

        for (var type = dropdown.GetType(); type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType == typeof(Rectangle) && field.GetValue(dropdown) is Rectangle bounds
                    && (bounds.Contains(firstPoint) || bounds.Contains(secondPoint)))
                    return true;
            }
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (property.PropertyType == typeof(Rectangle)
                    && property.GetIndexParameters().Length == 0
                    && property.GetValue(dropdown) is Rectangle bounds
                    && (bounds.Contains(firstPoint) || bounds.Contains(secondPoint)))
                    return true;
            }
        }
        return false;
    }

    private void ResetConfig()
    {
        _config = new HubConfig();
        _config.Normalize();
        _layoutDirty = true;
    }

    private void SaveGmcmConfig()
    {
        SaveConfig();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        ClearChestDrag();
        _gmcmControllerNavigationActive = false;
        _waitForSettingsChordRelease = false;
        _gmcmVirtualCursorInitialized = false;
        RemoveAddHotkeyFromSnapGraph(_snapGraphMenu);
        _layoutDirty = true;
        _layoutMenu = null;
        _layoutPage = null;
        _editorParent = null;
        _editorOpenedFromConfig = false;
        _pendingKeybind = null;
        _gmcmKeybindOverlay = null;
        _capturingDraftKeybind = false;
        _confirmingDelete = false;
        _gmcmActionBounds.Clear();
        _gmcmControlBounds.Clear();
    }

}

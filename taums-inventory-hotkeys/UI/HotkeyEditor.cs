using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System.Collections;
using System.Reflection;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private void OpenEditor(IClickableMenu parent, int tabIndex = -1)
    {
        if (tabIndex < 0 && _config.Tabs.Count >= MaxCustomTabs)
        {
            ShowGridFullMessage();
            // Monitor.Log($"Blocked add-hotkey editor: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
            return;
        }

        _emojiCatalog.EnsureLoaded();
        _editingTabIndex = tabIndex;
        _confirmingDelete = false;
        if (tabIndex >= 0 && tabIndex < _config.Tabs.Count)
        {
            var selected = _config.Tabs[tabIndex];
            _draftIcon = selected.Icon;
            _draftKeybind = selected.Keybinds;
            _draftName = selected.Name;
            _draftDescription = selected.Description;
            // Monitor.Log($"Opened hotkey editor for [{tabIndex}] '{_draftName}'.", LogLevel.Info);
        }
        else
        {
            _editingTabIndex = -1;
            _draftIcon = _emojiCatalog.Count == 0 ? 0 : _config.Tabs.Count % _emojiCatalog.Count;
            _draftKeybind = KeybindList.Parse("");
            _draftName = "New hotkey";
            _draftDescription = "";
            // Monitor.Log($"Opened add-hotkey editor. Initial draft: name='{_draftName}', iconId={_draftIcon}, keybind='{_draftKeybind}'.", LogLevel.Info);
        }
        _nameEditFocused = false;
        _namePrefillSelected = true;
        _editorParent = parent;
        _editorOpenedFromConfig = parent is not GameMenu;
        if (parent is GameMenu gameMenu)
            EnsureLayout(gameMenu);
        else
            RepositionEditorControls(parent, useViewport: true);
        Game1.activeClickableMenu = new InventoryTabEditorMenu(this, parent);
        Game1.playSound("openBox");
        LogDebug($"User action: opened {(tabIndex >= 0 ? "edit" : "create")} hotkey editor{(tabIndex >= 0 && tabIndex < _config.Tabs.Count ? $" for '{_config.Tabs[tabIndex].Name}'" : "")}, source={(parent is GameMenu ? "inventory" : "settings") }.");
    }

    private void OpenNativeNameInput(InventoryTabEditorMenu editor)
    {
        // Monitor.Log($"Opening Stardew name input for hotkey draft '{_draftName}'.", LogLevel.Info);
        var namingMenu = new HotkeyNamingMenu(name =>
        {
            _draftName = string.IsNullOrWhiteSpace(name) ? "New hotkey" : name.Trim();
            _namePrefillSelected = false;
            _nameEditFocused = true;
            Game1.activeClickableMenu = editor;
            editor.FocusNameInput();
            // Monitor.Log($"Stardew name input accepted '{_draftName}'.", LogLevel.Info);
        }, "Hotkey name", _draftName);
        Game1.activeClickableMenu = namingMenu;
        if (_useControllerTooltipGlyphs || Game1.options.gamepadControls)
            namingMenu.snapToDefaultClickableComponent();
    }

    private void OpenNativeDescriptionInput(InventoryTabEditorMenu editor)
    {
        var namingMenu = new HotkeyNamingMenu(value =>
        {
            _draftDescription = value.Trim();
            Game1.activeClickableMenu = editor;
            editor.FocusDescriptionInput();
        }, "Hotkey description", _draftDescription);
        Game1.activeClickableMenu = namingMenu;
        if (_useControllerTooltipGlyphs || Game1.options.gamepadControls)
            namingMenu.snapToDefaultClickableComponent();
    }

    private static bool TryGetEditorDirection(SButton button, out int horizontal, out int vertical)
    {
        (horizontal, vertical) = button switch
        {
            SButton.DPadLeft or SButton.LeftThumbstickLeft or SButton.Left => (-1, 0),
            SButton.DPadRight or SButton.LeftThumbstickRight or SButton.Right => (1, 0),
            SButton.DPadUp or SButton.LeftThumbstickUp or SButton.Up => (0, -1),
            SButton.DPadDown or SButton.LeftThumbstickDown or SButton.Down => (0, 1),
            _ => (0, 0)
        };
        return horizontal != 0 || vertical != 0;
    }

    private void CloseEditor(bool deleted = false)
    {
        var parent = _editorParent;
        var editedIndex = _editingTabIndex;
        _nameEditFocused = false;
        _confirmingDelete = false;
        _capturingDraftKeybind = false;
        _gmcmKeybindOverlay = null;
        Game1.activeClickableMenu = parent;
        _editorParent = null;
        _editingTabIndex = -1;

        if (parent is null)
            return;

        if (_editorOpenedFromConfig)
        {
            RegisterGenericModConfigMenu();
            _gmcmApi?.OpenModMenu(ModManifest);
            _editorOpenedFromConfig = false;
            return;
        }

        if (parent is not GameMenu gameMenu)
            return;
        EnsureLayout(gameMenu);
        if (deleted)
        {
            FocusInventoryTab(gameMenu);
            _pendingEditorReturnFocus = -1;
        }
        else if (editedIndex >= 0 && editedIndex < _config.Tabs.Count && _hotkeysVisible)
        {
            FocusHotkey(gameMenu, editedIndex);
            _pendingEditorReturnFocus = editedIndex;
        }
    }

    private void HandleEditorClick(int x, int y)
    {
        if (_confirmingDelete)
        {
            if (_deleteConfirmYesBounds.Contains(x, y))
                ConfirmDelete();
            else if (_deleteConfirmNoBounds.Contains(x, y))
                CancelDeleteConfirmation();
            return;
        }

        if (!_editorNameHitBounds.Contains(x, y))
            _nameEditFocused = false;

        if (_editorIconPreviousBounds.Contains(x, y)) CycleDraftIcon(-1);
        else if (_editorIconNextBounds.Contains(x, y)) CycleDraftIcon(1);
        else if (_editorIconDiceBounds.Contains(x, y))
        {
            _emojiCatalog.EnsureLoaded();
            if (_emojiCatalog.Count > 0)
                _draftIcon = Random.Shared.Next(_emojiCatalog.Count);
            LogDebug($"User action: randomized icon for hotkey draft '{_draftName}' to icon {_draftIcon}.");
        }
        else if (_editorKeybindHitBounds.Contains(x, y))
        {
            // Monitor.Log($"Clicked Assign keybind for draft hotkey '{_draftName}'. Current keybind='{_draftKeybind}'.", LogLevel.Info);
            OpenDraftKeybindEditor();
        }
        else if (_editorNameHitBounds.Contains(x, y))
        {
            if (Game1.activeClickableMenu is not InventoryTabEditorMenu editor)
                return;

            editor.FocusNameInput();
            OpenNativeNameInput(editor);
        }
        else if (_editorDescriptionHitBounds.Contains(x, y))
        {
            if (Game1.activeClickableMenu is not InventoryTabEditorMenu editor)
                return;
            OpenNativeDescriptionInput(editor);
        }
        else if (_editorCreateBounds.Contains(x, y))
        {
            SaveDraftHotkey();
        }
        else if (_editorCancelBounds.Contains(x, y))
        {
            // Monitor.Log($"Clicked Cancel for hotkey editor; draft discarded: name='{_draftName}', iconId={_draftIcon}, keybind='{_draftKeybind}'.", LogLevel.Info);
            var fromConfig = _editorOpenedFromConfig;
            CloseEditor();
            if (!fromConfig)
                RegisterGenericModConfigMenu();
        }
        else if (_editingTabIndex >= 0 && _editorDeleteBounds.Contains(x, y))
        {
            if (Game1.activeClickableMenu is not InventoryTabEditorMenu editor)
                return;

            _confirmingDelete = true;
            LogDebug($"User action: opened delete confirmation for hotkey '{_draftName}'.");
            var confirmation = new DeleteConfirmationMenu(this, editor);
            Game1.activeClickableMenu = confirmation;
            confirmation.FocusCancel();
        }
    }

    private void SaveDraftHotkey()
    {
        if (_editingTabIndex < 0 && _config.Tabs.Count >= MaxCustomTabs)
        {
            ShowGridFullMessage();
            // Monitor.Log($"Blocked hotkey creation: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
            CloseEditor();
            return;
        }

        // Monitor.Log($"Clicked {(_editingTabIndex >= 0 ? "Save edited hotkey" : "Create/OK")}. Pending values: name='{_draftName}', iconId={_draftIcon}, keybind='{_draftKeybind}'.", LogLevel.Info);
        bool updating = _editingTabIndex >= 0 && _editingTabIndex < _config.Tabs.Count;
        if (_editingTabIndex >= 0 && _editingTabIndex < _config.Tabs.Count)
            _config.Tabs[_editingTabIndex] = new HubTab { Name = _draftName, Description = _draftDescription, Icon = _draftIcon, Keybinds = _draftKeybind };
        else
            _config.Tabs.Add(new HubTab { Name = _draftName, Description = _draftDescription, Icon = _draftIcon, Keybinds = _draftKeybind });
        if (!updating)
            _selectedHotkeyConfigIndex = _config.Tabs.Count - 1;
        _config.Normalize();
        // Monitor.Log($"Hotkey save: before={previousCount}, after={_config.Tabs.Count}, capacity={MaxCustomTabs}.", LogLevel.Info);
        SaveConfig();
        LogDebug($"User action: {(updating ? "updated" : "created")} hotkey '{_draftName}', icon={_draftIcon}, keybind='{_draftKeybind}', descriptionPresent={!string.IsNullOrWhiteSpace(_draftDescription)}.");
        ShowHotkeyEvent($"Hotkey {(updating ? "updated" : "created")}: {_draftName} ({_draftKeybind})");
        if (!updating && _config.Tabs.Count > GridCapacity)
            Game1.addHUDMessage(new HUDMessage("Saved. Increase grid rows or columns to display this hotkey in the inventory."));
        if (!_editorOpenedFromConfig)
            RegisterGenericModConfigMenu();
        CloseEditor();
    }

    private void CancelDeleteConfirmation()
    {
        _confirmingDelete = false;

        if (Game1.activeClickableMenu is not DeleteConfirmationMenu menu)
            return;

        Game1.activeClickableMenu = menu.Editor;
        menu.Editor.FocusDeleteButton();
        LogDebug($"User action: cancelled deleting hotkey '{_draftName}'.");
    }

    private void ConfirmDelete()
    {
        if (_editingTabIndex >= 0 && _editingTabIndex < _config.Tabs.Count)
        {
            var deletedName = _config.Tabs[_editingTabIndex].Name;
            _config.Tabs.RemoveAt(_editingTabIndex);
            _selectedHotkeyConfigIndex = Math.Clamp(_selectedHotkeyConfigIndex, 0, Math.Max(0, _config.Tabs.Count - 1));
            // var deletedName = _config.Tabs[_editingTabIndex].Name;
            // Monitor.Log($"Deleted hotkey [{_editingTabIndex}] '{deletedName}'.", LogLevel.Info);
            SaveConfig();
            LogDebug($"User action: deleted hotkey '{deletedName}'.");
            if (!_editorOpenedFromConfig)
                RegisterGenericModConfigMenu();
        }
        CloseEditor(deleted: true);
    }

    private bool IsEditorInteractivePoint(Point point)
    {
        if (_confirmingDelete)
            return _deleteConfirmYesBounds.Contains(point) || _deleteConfirmNoBounds.Contains(point);

        return _editorIconPreviousBounds.Contains(point)
            || _editorIconNextBounds.Contains(point)
            || _editorIconDiceBounds.Contains(point)
            || _editorKeybindHitBounds.Contains(point)
            || _editorNameHitBounds.Contains(point)
            || _editorDescriptionHitBounds.Contains(point)
            || _editorCreateBounds.Contains(point)
            || _editorCancelBounds.Contains(point)
            || (_editingTabIndex >= 0 && _editorDeleteBounds.Contains(point));
    }

    private void CycleDraftIcon(int delta)
    {
        _emojiCatalog.EnsureLoaded();
        if (_emojiCatalog.Count == 0)
            return;
        _draftIcon = (_draftIcon + delta + _emojiCatalog.Count) % _emojiCatalog.Count;
        LogDebug($"User action: changed draft icon for '{_draftName}' to {_draftIcon}.");
    }

    private void OpenDraftKeybindEditor()
    {
        OpenKeybindOverlay(_draftKeybind, "Inventory hotkey", value => _draftKeybind = value);
    }

    private void OpenKeybindOverlay(KeybindList initialBindings, string title, Action<KeybindList> saveBindings)
    {
        _gmcmKeybindOverlayType ??= AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("GenericModConfigMenu.Framework.Overlays.KeybindOverlay", throwOnError: false))
            .FirstOrDefault(type => type is not null);

        Type[] constructorTypes = { typeof(Keybind[]), typeof(bool), typeof(string), typeof(Action<Keybind[]>) };
        var constructor = _gmcmKeybindOverlayType?.GetConstructor(constructorTypes);

        if (constructor is null)
        {
            Monitor.Log(
                $"GMCM overlay unavailable. " +
                $"Type found: {_gmcmKeybindOverlayType is not null}. " +
                $"Constructor found: {constructor is not null}.",
                LogLevel.Warn
            );

            if (Game1.activeClickableMenu is InventoryTabEditorMenu)
                _capturingDraftKeybind = true;
            return;
        }

        const BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        _gmcmDrawMethod = _gmcmKeybindOverlayType!.GetMethod("Draw", publicInstance);
        _gmcmClickMethod = _gmcmKeybindOverlayType.GetMethod("OnLeftClick", publicInstance);
        _gmcmButtonsChangedMethod = _gmcmKeybindOverlayType.GetMethod("OnButtonsChanged", publicInstance);
        _gmcmKeybindEditField = _gmcmKeybindOverlayType.GetField("KeybindEdit", privateInstance);
        _gmcmButtonsField = _gmcmKeybindOverlayType.GetField("Buttons", privateInstance);
        _gmcmFinishedProperty = _gmcmKeybindOverlayType.GetProperty("IsFinished", publicInstance);

        Action<Keybind[]> save = keybinds =>
        {
            var serialized = string.Join(", ", keybinds.Select(keybind => keybind.ToString()));
            saveBindings(KeybindList.Parse(serialized));
            // Monitor.Log($"GMCM keybind editor accepted selection. Raw='{serialized}', parsed='{_draftKeybind}', bound={_draftKeybind.IsBound}.", LogLevel.Info);
        };
        Keybind[] initial = initialBindings.Keybinds.ToArray();
        _gmcmKeybindOverlay = constructor.Invoke(new object?[] { initial, false, title, save });
        _gmcmFocusedButtonName = initial.Length > 0 ? "Remove 0" : "Add";
        _skipGmcmControllerAOnButtonsChanged = false;
        _capturingDraftKeybind = false;
        // Monitor.Log($"Opened GMCM keybind editor for '{_draftName}'. Initial keybind='{_draftKeybind}'.", LogLevel.Info);
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (e.Pressed.Any(IsControllerButton))
            _useControllerTooltipGlyphs = true;
        else if (e.Pressed.Any())
            _useControllerTooltipGlyphs = false;

        foreach (SButton button in e.Pressed)
        {
            if (!_simulatedButtonsAwaitingChangedEvent.Remove(button))
                continue;

            // var activeMenu = Game1.activeClickableMenu?.GetType().FullName ?? "<none>";
            // Monitor.Log($"Simulated key reached ButtonsChanged: {button}; active menu={activeMenu}; SMAPI state={Helper.Input.GetState(button)}.", LogLevel.Info);
        }

        if (Game1.activeClickableMenu is not InventoryTabEditorMenu
            && (Game1.activeClickableMenu is not IClickableMenu menu || !IsGmcmConfigMenu(menu)))
            return;
        if (_gmcmKeybindOverlay is null)
            return;

        if (_skipGmcmControllerAOnButtonsChanged)
        {
            _skipGmcmControllerAOnButtonsChanged = false;
            if (e.Pressed.Contains(SButton.ControllerA))
                return;
        }

        if (!IsGmcmCapturingKeybind())
            return;

        // GMCM handles keyboard combinations and controller buttons here.
        InvokeGmcmOverlay("OnButtonsChanged", e);

        foreach (var button in e.Pressed)
            Helper.Input.Suppress(button);
    }

    private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
    {
        Point cursor = new(Game1.getMouseX(), Game1.getMouseY());
        UpdateChestDrag(cursor);
        if (_pendingControllerCursorPosition is Point snappedPosition && cursor == snappedPosition)
        {
            _pendingControllerCursorPosition = null;
            return;
        }
        _pendingControllerCursorPosition = null;
        if (_snappingControllerCursor)
            return;
        _useControllerTooltipGlyphs = false;
    }

    private void InvokeGmcmOverlay(string methodName, params object?[] args)
    {
        if (_gmcmKeybindOverlay is null)
            return;

        var method = methodName switch
        {
            "Draw" => _gmcmDrawMethod,
            "OnLeftClick" => _gmcmClickMethod,
            "OnButtonsChanged" => _gmcmButtonsChangedMethod,
            _ => null
        };
        method?.Invoke(_gmcmKeybindOverlay, args);
    }

    private bool IsGmcmCapturingKeybind() => _gmcmKeybindOverlay is not null
        && _gmcmKeybindEditField?.GetValue(_gmcmKeybindOverlay) is not null;

    private List<ClickableTextureComponent> GetGmcmButtons()
    {
        var buttons = _gmcmButtonsField?.GetValue(_gmcmKeybindOverlay);
        return buttons is IEnumerable list ? list.OfType<ClickableTextureComponent>().ToList() : new List<ClickableTextureComponent>();
    }

    private void MoveGmcmFocus(int horizontal, int vertical)
    {
        var buttons = GetGmcmButtons();
        var listButtons = buttons.Where(button => button.name is not ("OK" or "Clear")).ToList();
        if (buttons.Count == 0)
            return;

        var listIndex = listButtons.FindIndex(button => button.name == _gmcmFocusedButtonName);
        _gmcmFocusedButtonName = (horizontal, vertical) switch
        {
            ({ } h, _) when h != 0 => _gmcmFocusedButtonName switch
            {
                "OK" when h > 0 => "Clear",
                "Clear" when h < 0 => "OK",
                _ => _gmcmFocusedButtonName
            },

            (_, > 0) => listIndex >= 0 && listIndex + 1 < listButtons.Count
                ? listButtons[listIndex + 1].name
                : "OK",

            (_, < 0) => listIndex > 0
                ? listButtons[listIndex - 1].name
                : listIndex == 0
                    ? listButtons[0].name
                    : listButtons.LastOrDefault()?.name ?? "OK",

            _ => _gmcmFocusedButtonName
        };

    }

    private void ActivateGmcmFocusedButton()
    {
        var button = GetGmcmButtons().FirstOrDefault(candidate => candidate.name == _gmcmFocusedButtonName);
        if (button is null)
            return;

        // Monitor.Log($"Controller activated GMCM keybind control '{button.name}'.", LogLevel.Trace);
        InvokeGmcmOverlay("OnLeftClick", button.bounds.Center.X, button.bounds.Center.Y);
        if (button.name.StartsWith("Remove", StringComparison.Ordinal))
            _gmcmFocusedButtonName = "Add";
    }

    private void DrawGmcmControllerFocus(SpriteBatch b)
    {
        if (IsGmcmCapturingKeybind() || !_useControllerTooltipGlyphs)
            return;

        var button = GetGmcmButtons().FirstOrDefault(candidate => candidate.name == _gmcmFocusedButtonName);
        if (button is null)
            return;

        var bounds = button.bounds;
        const int border = 3;
        var color = Color.Red;
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Y - border, bounds.Width + border * 2, border), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Bottom, bounds.Width + border * 2, border), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Y, border, bounds.Height), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.Right, bounds.Y, border, bounds.Height), color);
    }

    private void DrawEditor(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, _editorBounds.X, _editorBounds.Y, _editorBounds.Width, _editorBounds.Height, Color.White);
        var title = _editingTabIndex >= 0 ? "Edit hotkey" : "Add a hotkey";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        b.DrawString(Game1.dialogueFont, title, new Vector2(_editorBounds.Center.X - titleSize.X / 2, _editorBounds.Y + 28), Color.SaddleBrown);
        const string instructions = "Set a name and icon, then choose a shortcut key.\nClicking the hotkey sends that key to the game.";
        b.DrawString(Game1.smallFont, instructions,
            new Vector2(_editorBounds.X + 32, _editorBounds.Y + 92), Color.SaddleBrown);

        DrawEditorName(b);
        DrawEditorDescription(b);
        DrawEditorIcon(b);

        DrawEditorKeybind(b);

        DrawConfirmationButton(b, _editorCreateBounds, isOk: true);
        DrawConfirmationButton(b, _editorCancelBounds, isOk: false);
        if (_editingTabIndex >= 0)
            DrawTrashButton(b, _editorDeleteBounds);
        Point cursor = new(Game1.getMouseX(), Game1.getMouseY());
        if (!_confirmingDelete)
        {
            var hoverText = cursor switch
            {
                _ when _editorCreateBounds.Contains(cursor) => "Save",
                _ when _editorCancelBounds.Contains(cursor) => "Cancel",
                _ when _editingTabIndex >= 0 && _editorDeleteBounds.Contains(cursor) => "Delete hotkey",
                _ => null
            };

            if (hoverText is not null)
                IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont);
        }

        if (!_confirmingDelete)
            return;

        DrawDeleteConfirmation(b);
    }

    private void DrawEditorName(SpriteBatch b)
    {
        b.DrawString(Game1.dialogueFont, "Name",
            new Vector2(_editorBounds.X + 40, _editorNameBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        IClickableMenu.drawTextureBox(b, _editorNameBounds.X, _editorNameBounds.Y, _editorNameBounds.Width, _editorNameBounds.Height,
            _nameEditFocused ? new Color(255, 245, 195) : Color.White);
        var visibleName = FitText(_draftName, Game1.smallFont, _editorNameBounds.Width - 24);
        Vector2 namePosition = new(_editorNameBounds.X + 18, _editorNameBounds.Center.Y - Game1.smallFont.LineSpacing / 2);
        if (_nameEditFocused && _namePrefillSelected)
        {
            var selectionWidth = (int)Game1.smallFont.MeasureString(visibleName).X;
            b.Draw(Game1.staminaRect,
                new Rectangle((int)namePosition.X - 2, (int)namePosition.Y, selectionWidth + 4, Game1.smallFont.LineSpacing),
                new Color(74, 105, 145, 190));
        }
        b.DrawString(Game1.smallFont, visibleName, namePosition,
            _nameEditFocused && _namePrefillSelected ? Color.White : Color.SaddleBrown);
        if (_nameEditFocused)
        {
            var caretX = (int)(namePosition.X + Game1.smallFont.MeasureString(visibleName).X + 2);
            var caretY = _editorNameBounds.Center.Y - 14;
            b.Draw(Game1.staminaRect, new Rectangle(caretX, caretY, 3, 28), Color.SaddleBrown);
        }
    }

    private void DrawEditorDescription(SpriteBatch b)
    {
        b.DrawString(Game1.dialogueFont, "Description",
            new Vector2(_editorBounds.X + 40, _editorDescriptionBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        IClickableMenu.drawTextureBox(b, _editorDescriptionBounds.X, _editorDescriptionBounds.Y,
            _editorDescriptionBounds.Width, _editorDescriptionBounds.Height, Color.White);
        bool empty = string.IsNullOrWhiteSpace(_draftDescription);
        string value = empty ? "Click to add a description (optional)" : _draftDescription;
        var fitted = FitText(value, Game1.smallFont, _editorDescriptionBounds.Width - 24);
        b.DrawString(Game1.smallFont, fitted,
            new Vector2(_editorDescriptionBounds.X + 18, _editorDescriptionBounds.Center.Y - Game1.smallFont.LineSpacing / 2),
            empty ? Color.Gray : Color.SaddleBrown);
    }

    private void DrawEditorIcon(SpriteBatch b)
    {
        b.DrawString(Game1.dialogueFont, "Select icon",
            new Vector2(_editorBounds.X + 40, _editorIconBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        _emojiCatalog.DrawIcon(b, _draftIcon, _editorIconBounds, maxSize: 64);
        DrawCarouselArrow(b, _editorIconPreviousBounds, right: false);
        DrawCarouselArrow(b, _editorIconNextBounds, right: true);
        DrawDiceIcon(b, _editorIconDiceBounds);
        _emojiCatalog.EnsureLoaded();
        var iconCounter = _emojiCatalog.Count > 0 ? $"{Math.Clamp(_draftIcon, 0, _emojiCatalog.Count - 1) + 1}/{_emojiCatalog.Count}" : "0/0";
        var iconCounterSize = Game1.smallFont.MeasureString(iconCounter);
        b.DrawString(Game1.smallFont, iconCounter,
            new Vector2(_editorIconCounterBounds.Right - iconCounterSize.X,
                _editorIconCounterBounds.Center.Y - iconCounterSize.Y / 2f),
            Color.SaddleBrown);
        if (_editorIconDiceBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
            IClickableMenu.drawHoverText(b, "Random icon", Game1.smallFont);
    }

    private void DrawEditorKeybind(SpriteBatch b)
    {
        b.DrawString(Game1.dialogueFont, "Assign keybind",
            new Vector2(_editorBounds.X + 40, _editorKeybindBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        // Match ExitPage.draw exactly: same cursor-sheet 9-slice, hover fill,
        // dialogue font, text color, shadow, and vertical text offset.
        var keybindHovered = _editorKeybindBounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            _editorKeybindBounds.X, _editorKeybindBounds.Y, _editorKeybindBounds.Width, _editorKeybindBounds.Height,
            keybindHovered ? Color.Wheat : Color.White, 4f);
        var keybindLabel = _draftKeybind.IsBound ? _draftKeybind.ToString() : "Edit";
        Utility.drawTextWithShadow(b, keybindLabel, Game1.dialogueFont,
            new Vector2(_editorKeybindBounds.Center.X, _editorKeybindBounds.Center.Y + 4)
            - Game1.dialogueFont.MeasureString(keybindLabel) / 2f,
            Game1.textColor, 1f, -1f, -1, -1, 0f);
    }

    private void DrawDeleteConfirmation(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, _deleteConfirmBounds.X, _deleteConfirmBounds.Y,
            _deleteConfirmBounds.Width, _deleteConfirmBounds.Height, Color.White);

        var prompt = $"Delete '{_draftName}'?";
        var promptSize = Game1.dialogueFont.MeasureString(prompt);

        b.DrawString(Game1.dialogueFont, prompt,
            new Vector2(_deleteConfirmBounds.Center.X - promptSize.X / 2, _deleteConfirmBounds.Y + 36),
            Color.SaddleBrown);

        DrawTextButton(b, _deleteConfirmYesBounds, "Delete");
        DrawTextButton(b, _deleteConfirmNoBounds, "Cancel");
    }

    private static void DrawTextButton(SpriteBatch b, Rectangle bounds, string text)
    {
        var hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height, hovered ? Color.Wheat : Color.White, 4f);
        var textSize = Game1.smallFont.MeasureString(text);
        b.DrawString(Game1.smallFont, text,
            new Vector2(bounds.Center.X - textSize.X / 2, bounds.Center.Y - textSize.Y / 2), Color.SaddleBrown);
    }

    private static void DrawTrashButton(SpriteBatch b, Rectangle bounds)
    {
        // Use the same standard tile scale as the native save/cancel icons,
        // with Stardew's inventory trash-can sprite.
        Rectangle source = new(564, 102, 16, 16);
        var scale = Math.Min(2.5f, Math.Min((bounds.Width - 4) / (float)source.Width, (bounds.Height - 4) / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors,
            new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private static void DrawCarouselArrow(SpriteBatch b, Rectangle bounds, bool right)
    {
        // Reuse the game's own tab-pagination arrows for native carousel styling.
        var source = right ? new Rectangle(365, 495, 12, 11) : new Rectangle(352, 495, 12, 11);
        var scale = Math.Min(3.2f, Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private static void DrawConfirmationButton(SpriteBatch b, Rectangle bounds, bool isOk)
    {
        var tileIndex = isOk ? 46 : 47;
        Rectangle source = Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, tileIndex, -1, -1);
        var scale = Math.Min(2.5f, Math.Min((bounds.Width - 4) / (float)source.Width, (bounds.Height - 4) / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors,
            new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private static string FitText(string text, SpriteFont font, float maxWidth)
    {
        if (font.MeasureString(text).X <= maxWidth)
            return text;

        const string ellipsis = "…";
        if (font.MeasureString(ellipsis).X > maxWidth)
            return "";

        int low = 0;
        int high = text.Length;
        while (low < high)
        {
            int middle = low + (high - low + 1) / 2;
            if (middle < text.Length && middle > 0
                && char.IsHighSurrogate(text[middle - 1]) && char.IsLowSurrogate(text[middle]))
                middle--;
            if (font.MeasureString(text[..middle] + ellipsis).X <= maxWidth)
                low = middle;
            else
                high = middle - 1;
        }

        if (low < text.Length && low > 0
            && char.IsHighSurrogate(text[low - 1]) && char.IsLowSurrogate(text[low]))
            low--;
        return text[..low] + ellipsis;
    }

    private static void DrawDiceIcon(SpriteBatch b, Rectangle bounds)
    {
        // Draw only the vanilla randomize-dice sprite; the surrounding rectangle
        // remains an invisible click target for the name randomizer.
        Rectangle source = new(381, 361, 10, 10);
        var scale = Math.Min(5f, Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2), source, Color.White,
            0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private void DrawKeybindCapture(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, _keybindCaptureBounds.X, _keybindCaptureBounds.Y,
            _keybindCaptureBounds.Width, _keybindCaptureBounds.Height, Color.White);
        const string prompt = "Press a key for this hotkey (Esc to cancel)";
        var promptSize = Game1.dialogueFont.MeasureString(prompt);
        b.DrawString(Game1.dialogueFont, prompt,
            new Vector2(_keybindCaptureBounds.Center.X - promptSize.X / 2, _keybindCaptureBounds.Y + 44), Color.SaddleBrown);
        var current = _draftKeybind.IsBound ? _draftKeybind.ToString() : "No key assigned";
        var currentSize = Game1.smallFont.MeasureString(current);
        b.DrawString(Game1.smallFont, current,
            new Vector2(_keybindCaptureBounds.Center.X - currentSize.X / 2, _keybindCaptureBounds.Y + 112), Color.SaddleBrown);
    }

}

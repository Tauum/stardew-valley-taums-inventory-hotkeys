using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private bool TryBeginChestDrag(ButtonPressedEventArgs e, GameMenu menu)
    {
        if (!UseChestGrid || !_config.EnableHotkeyMoving || !_hotkeysVisible || !IsInventoryPage(menu))
            return false;

        EnsureLayout(menu);
        var point = e.Cursor.GetScaledScreenPixels().ToPoint();
        foreach (var (bounds, index) in _customTabBounds)
        {
            if (!bounds.Contains(point))
                continue;

            _chestDragSourceIndex = index;
            _chestDragTargetIndex = index;
            _chestDragStart = point;
            _chestDragCursor = point;
            _chestDragActive = false;
            _controllerAddHotkeyFocused = false;
            Helper.Input.Suppress(e.Button);
            return true;
        }
        return false;
    }

    private void UpdateChestDrag(Point cursor)
    {
        if (_chestDragSourceIndex is null)
            return;
        _chestDragCursor = cursor;
        var dx = cursor.X - _chestDragStart.X;
        var dy = cursor.Y - _chestDragStart.Y;
        if (!_chestDragActive && dx * dx + dy * dy >= 64)
        {
            _chestDragActive = true;
            LogDebug($"User action: started moving hotkey {_chestDragSourceIndex!.Value + 1} by drag.");
        }
        if (_chestDragActive)
            _chestDragTargetIndex = GetChestDropIndex(cursor);
    }

    private int? GetChestDropIndex(Point point)
    {
        if (!UseChestGrid || !_config.EnableHotkeyMoving || _customTabBounds.Count == 0)
            return null;
        for (var index = 0; index < _customTabBounds.Count; index++)
        {
            if (_customTabBounds[index].bounds.Contains(point))
                return index;
        }
        return null;
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        // Suppress() raises an early ButtonReleased for the game's input state.
        // The mouse can still be physically held, so wait until suppression ends.
        if (e.Button != SButton.MouseLeft || _chestDragSourceIndex is null
            || Helper.Input.IsSuppressed(SButton.MouseLeft))
            return;

        FinishChestDrag();
    }

    private void FinishChestDrag()
    {
        if (_chestDragSourceIndex is not int source)
            return;

        UpdateChestDrag(new Point(Game1.getMouseX(), Game1.getMouseY()));
        var wasDragged = _chestDragActive;
        var target = _chestDragTargetIndex;
        ClearChestDrag();

        if (Game1.activeClickableMenu is not GameMenu menu || !IsInventoryPage(menu)
            || !UseChestGrid || !_config.EnableHotkeyMoving || !_hotkeysVisible || _config.HideHotkeysGlobally
            || source >= _config.Tabs.Count)
            return;

        if (wasDragged)
        {
            if (target is int destination && destination != source)
                MoveChestHotkey(source, destination);
            return;
        }

        // The press was held back while we checked for a drag. Preserve the
        // player's configured left-click action when they simply click a slot.
        if (MatchesAction(_config.EditMouse, SButton.MouseLeft))
            OpenEditor(menu, source);
        else if (MatchesAction(_config.ActivateMouse, SButton.MouseLeft))
            ActivateHotkey(source);
    }

    private void MoveChestHotkey(int source, int destination)
    {
        if (source < 0 || source >= _config.Tabs.Count || destination < 0
            || destination >= _customTabBounds.Count)
            return;

        var selected = _config.Tabs.ElementAtOrDefault(_selectedHotkeyConfigIndex);
        var moved = _config.Tabs[source];
        _config.Tabs.RemoveAt(source);
        _config.Tabs.Insert(destination, moved);
        if (selected is not null)
            _selectedHotkeyConfigIndex = _config.Tabs.IndexOf(selected);
        SaveConfig();
        LogDebug($"User action: moved hotkey '{moved.Name}' from position {source + 1} to {destination + 1}.");
        Game1.playSound("dwop");
        RegisterGenericModConfigMenu();
        ShowHotkeyEvent($"Hotkey moved: {moved.Name} to position {destination + 1}");
    }

    private void ClearChestDrag()
    {
        _chestDragSourceIndex = null;
        _chestDragTargetIndex = null;
        _chestDragActive = false;
    }
}

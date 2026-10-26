using StardewModdingAPI.Utilities;
using Taums_inventory_hotkeys;

public sealed class HubConfig
{
    public int Columns { get; set; } = 10;
    public int Rows { get; set; } = 4;
    public int ChestGridOffsetX { get; set; }
    public int ChestGridOffsetY { get; set; }
    public string HotkeyStyle { get; set; } = "InventoryTabs";
    public bool HideHotkeysGlobally { get; set; }
    public bool EnableProfileButtons { get; set; } = true;
    public bool EnableHotkeyMoving { get; set; } = true;
    public bool EnableDebugLogs { get; set; }
    public int ActiveProfile { get; set; } = 1;
    public List<int> ProfileNumbers { get; set; } = new();
    public string ProfileSwitcherPosition { get; set; } = "AboveAdd";

    // Controller / keyboard bindings
    public KeybindList ActivateButton { get; set; } =
        KeybindList.Parse("ControllerA");

    public KeybindList EditButton { get; set; } =
        KeybindList.Parse("ControllerX");

    public KeybindList ToggleButton { get; set; } =
        KeybindList.Parse("ControllerY");

    // Mouse bindings
    public KeybindList ActivateMouse { get; set; } =
        KeybindList.Parse("MouseLeft");

    public KeybindList EditMouse { get; set; } =
        KeybindList.Parse("MouseRight");

    public KeybindList ToggleMouse { get; set; } =
        KeybindList.Parse("MouseMiddle");

    public KeybindList OpenSettings { get; set; } =
        KeybindList.Parse("LeftControl + MouseMiddle");

    public bool BlurEnabled { get; set; } = true;
    public int BlurOpacity { get; set; } = 45;

    [System.Text.Json.Serialization.JsonIgnore]
    public List<HubTab> Tabs { get; set; } = new();

    public void Normalize()
    {
        Columns = Math.Clamp(Columns, 2, 10);
        Rows = Math.Clamp(Rows, 1, 4);
        ChestGridOffsetX = Math.Clamp(ChestGridOffsetX, -1000, 1000);
        ChestGridOffsetY = Math.Clamp(ChestGridOffsetY, -1000, 1000);
        ActiveProfile = Math.Max(1, ActiveProfile);
        ProfileNumbers = (ProfileNumbers ?? new List<int>()).Where(number => number > 0)
            .Append(ActiveProfile).Distinct().OrderBy(number => number).ToList();

        if (HotkeyStyle is not ("InventoryTabs" or "ChestGrid"))
            HotkeyStyle = "InventoryTabs";
        if (ProfileSwitcherPosition is not ("AboveAdd" or "BesideChestGrid"))
            ProfileSwitcherPosition = "AboveAdd";

        BlurOpacity = Math.Clamp(BlurOpacity, 0, 100);

        // Restore defaults only when bindings are null.
        ActivateButton ??= KeybindList.Parse("ControllerA");
        if (ActivateButton.Keybinds.Any(binding => binding.Buttons.Contains(StardewModdingAPI.SButton.Enter)))
        {
            ActivateButton = new KeybindList(ActivateButton.Keybinds
                .Where(binding => !binding.Buttons.Contains(StardewModdingAPI.SButton.Enter)).ToArray());
        }
        EditButton ??= KeybindList.Parse("ControllerX");
        ToggleButton ??= KeybindList.Parse("ControllerY");

        ActivateMouse ??= KeybindList.Parse("MouseLeft");
        EditMouse ??= KeybindList.Parse("MouseRight");
        ToggleMouse ??= KeybindList.Parse("MouseMiddle");
        OpenSettings ??= KeybindList.Parse("LeftControl + MouseMiddle");

        Tabs ??= new List<HubTab>();

        // Grid size controls only how many tabs are shown,
        // never how many are saved.
        Tabs = Tabs.Where(tab => tab is not null).ToList();

        foreach (HubTab tab in Tabs)
        {
            tab.Name = string.IsNullOrWhiteSpace(tab.Name)
                ? "New hotkey"
                : tab.Name.Trim();
            tab.Description ??= "";
            tab.Description = tab.Description.Trim();

            tab.Icon = Math.Clamp(tab.Icon, 0, 100000);
            tab.Keybinds ??= KeybindList.Parse("");
        }
    }
}

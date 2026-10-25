using StardewModdingAPI.Utilities;

namespace Taums_menu_supreme;

public sealed class HubConfig
{
    private const int MaxHotkeys = 40;

    public int Columns { get; set; } = 10;
    public int Rows { get; set; } = 4;
    public List<HubTab> Tabs { get; set; } = new();

    public void Normalize()
    {
        Columns = Math.Clamp(Columns, 2, 10);
        Rows = Math.Clamp(Rows, 1, 4);
        Tabs ??= new List<HubTab>();
        // Keep older configs with smaller Columns/Rows values from silently
        // truncating the new ten-wide rows; four rows support up to 40 tabs.
        Tabs = Tabs.Where(tab => tab is not null).Take(MaxHotkeys).ToList();
        foreach (HubTab tab in Tabs)
        {
            tab.Name = string.IsNullOrWhiteSpace(tab.Name) ? "New hotkey" : tab.Name.Trim();
            tab.Icon = Math.Clamp(tab.Icon, 0, 100000);
            tab.Keybinds ??= KeybindList.Parse("");
        }
    }
}

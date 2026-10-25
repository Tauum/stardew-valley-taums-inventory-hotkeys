namespace Taums_inventory_hotkeys;

public sealed class HotkeyProfile
{
    public bool IsCreated { get; set; } = true;
    public int? Columns { get; set; }
    public int? Rows { get; set; }
    public List<HubTab> Tabs { get; set; } = new();
}

using StardewModdingAPI.Utilities;

namespace Taums_inventory_hotkeys;

public sealed class HubTab
{
    public string Name { get; set; } = "New hotkey";
    public string Description { get; set; } = "";
    public int Icon { get; set; }
    public KeybindList Keybinds { get; set; } = KeybindList.Parse("");
}

using StardewModdingAPI.Utilities;

namespace Taums_menu_supreme;

public sealed class HubTab
{
    public string Name { get; set; } = "New hotkey";
    public int Icon { get; set; }
    public KeybindList Keybinds { get; set; } = KeybindList.Parse("");
}

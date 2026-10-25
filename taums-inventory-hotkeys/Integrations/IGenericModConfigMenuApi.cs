using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Taums_inventory_hotkeys;

// Must be a top-level public interface: SMAPI checks Type.IsPublic when it
// creates mod API proxies, and rejects even a public nested interface.
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void Unregister(IManifest mod);
    void OpenModMenu(IManifest mod);
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string?>? tooltip = null);
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue,
        Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue,
        Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null,
        int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
    void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue,
        Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue,
        Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null,
        Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    void AddParagraph(IManifest mod, Func<string> text);
    void AddComplexOption(IManifest mod, Func<string> name, Action<SpriteBatch, Vector2> draw,
        Func<string>? tooltip = null, Action? beforeMenuOpened = null, Action? beforeSave = null,
        Action? afterSave = null, Action? beforeReset = null, Action? afterReset = null,
        Action? beforeMenuClosed = null, Func<int>? height = null, string? fieldId = null);
}

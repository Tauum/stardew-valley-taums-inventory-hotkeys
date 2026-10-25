using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace Taums_menu_supreme;

// Must be a top-level public interface: SMAPI checks Type.IsPublic when it
// creates mod API proxies, and rejects even a public nested interface.
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void Unregister(IManifest mod);
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string?>? tooltip = null);
    void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue,
        Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
}

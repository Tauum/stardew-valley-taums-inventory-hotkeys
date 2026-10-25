using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;

namespace Taums_menu_supreme;

public sealed partial class ModEntry
{
    private void SaveConfig()
    {
        _config.Normalize();
        _layoutDirty = true;
        Helper.WriteConfig(_config);
        // Monitor.Log($"Persisted hotkey _config: count={_config.Tabs.Count}; {string.Join(" | ", _config.Tabs.Select((tab, index) => $"[{index}] '{tab.Name}' iconId={tab.Icon} keybind='{tab.Keybinds}'"))}.", LogLevel.Info);
    }

    private void RegisterGenericModConfigMenu()
    {
        _gmcmDraftKeybinds.Clear();
        _gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_gmcmApi is null)
        {
            // Monitor.Log("Generic Mod Config Menu isn't installed; hotkey shortcuts can be added after installing it.", LogLevel.Trace);
            return;
        }

        // Monitor.Log("Connected to Generic Mod Config Menu API; registering hotkey keybind options.", LogLevel.Info);
        _gmcmApi.Unregister(ModManifest);
        _gmcmApi.Register(ModManifest, ResetConfig, SaveGmcmConfig);
        _gmcmApi.AddSectionTitle(ModManifest, () => "Hotkey shortcuts",
            () => "Assign shortcuts using Generic Mod Config Menu's standard keybinding editor.");

        for (var i = 0; i < _config.Tabs.Count; i++)
        {
            var index = i;
            _gmcmApi.AddKeybindList(ModManifest,
                () => _gmcmDraftKeybinds.TryGetValue(index, out var draft) ? draft
                    : index < _config.Tabs.Count ? _config.Tabs[index].Keybinds : KeybindList.Parse(""),
                value =>
                {
                    if (index >= _config.Tabs.Count)
                        return;
                    _gmcmDraftKeybinds[index] = value;
                    // Monitor.Log($"GMCM saved keybind for hotkey [{index}] '{_config.Tabs[index].Name}': '{value}'.", LogLevel.Info);
                },
                () => index < _config.Tabs.Count ? _config.Tabs[index].Name : $"Hotkey {index + 1}",
                () => "Send this hotkey's assigned key to the game when clicked.");
        }
    }

    private void ResetConfig()
    {
        _gmcmDraftKeybinds.Clear();
        _config = new HubConfig();
        _config.Normalize();
        _layoutDirty = true;
    }

    private void SaveGmcmConfig()
    {
        foreach (var (index, keybind) in _gmcmDraftKeybinds)
        {
            if (index < _config.Tabs.Count)
                _config.Tabs[index].Keybinds = keybind;
        }
        _gmcmDraftKeybinds.Clear();
        SaveConfig();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        RemoveAddHotkeyFromSnapGraph(_snapGraphMenu);
        _layoutDirty = true;
        _layoutMenu = null;
        _layoutPage = null;
        _editorParent = null;
        _pendingKeybind = null;
        _gmcmKeybindOverlay = null;
        _capturingDraftKeybind = false;
        _confirmingDelete = false;
        _gmcmDraftKeybinds.Clear();
    }

}

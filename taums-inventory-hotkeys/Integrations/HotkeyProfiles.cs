using StardewModdingAPI;
using StardewValley;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private static string ProfilePath(int number) => $"profiles/hotkeys-profile-{number}.json";
    private static string LegacyProfilePath(int number) => $"hotkeys-profile-{number}.json";

    private HotkeyProfile? ReadProfile(int number)
    {
        return Helper.Data.ReadJsonFile<HotkeyProfile>(ProfilePath(number))
            ?? Helper.Data.ReadJsonFile<HotkeyProfile>(LegacyProfilePath(number));
    }

    private void EnsureProfileDirectory()
    {
        Directory.CreateDirectory(Path.Combine(Helper.DirectoryPath, "profiles"));
    }

    private List<int> GetCreatedProfileNumbers()
    {
        var profiles = new List<int>();
        var profileDirectory = Path.Combine(Helper.DirectoryPath, "profiles");
        var packagedProfiles = Directory.Exists(profileDirectory)
            ? Directory.EnumerateFiles(profileDirectory, "hotkeys-profile-*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Select(name => name is not null && name.StartsWith("hotkeys-profile-", StringComparison.Ordinal)
                    && int.TryParse(name["hotkeys-profile-".Length..], out var number) ? number : 0)
            : Enumerable.Empty<int>();
        var candidates = Enumerable.Range(1, 6).Concat(packagedProfiles)
            .Concat(_config.ProfileNumbers ?? new List<int>())
            .Append(_config.ActiveProfile).Where(number => number > 0).Distinct().OrderBy(number => number);
        foreach (int number in candidates)
        {
            var profile = ReadProfile(number);
            if (profile?.IsCreated == true)
                profiles.Add(number);
        }
        _config.ProfileNumbers = profiles;
        return profiles;
    }

    private bool LoadActiveProfile(bool migrateLegacy = false)
    {
        try
        {
            var profile = ReadProfile(_config.ActiveProfile);
            if (profile is null)
            {
                // Preserve existing config.json hotkeys as the first profile on upgrade.
                if (migrateLegacy && _config.ActiveProfile == 1)
                {
                    if (!SaveActiveProfile())
                        return false;
                }
                else
                    _config.Tabs = new List<HubTab>();
            }
            else if (!profile.IsCreated)
            {
                _config.Tabs = new List<HubTab>();
                return false;
            }
            else
            {
                if (profile.Columns is int columns)
                    _config.Columns = columns;
                if (profile.Rows is int rows)
                    _config.Rows = rows;
                _config.Tabs = profile.Tabs ?? new List<HubTab>();
            }
            _config.Normalize();
            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Could not load hotkey profile {_config.ActiveProfile}: {ex}", LogLevel.Error);
            return false;
        }
    }

    private bool SaveActiveProfile()
    {
        try
        {
            EnsureProfileDirectory();
            Helper.Data.WriteJsonFile(ProfilePath(_config.ActiveProfile), new HotkeyProfile
            {
                IsCreated = true,
                Columns = _config.Columns,
                Rows = _config.Rows,
                Tabs = _config.Tabs
            });
            _config.ProfileNumbers ??= new List<int>();
            if (!_config.ProfileNumbers.Contains(_config.ActiveProfile))
                _config.ProfileNumbers.Add(_config.ActiveProfile);
            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Could not save hotkey profile {_config.ActiveProfile}: {ex}", LogLevel.Error);
            return false;
        }
    }

    private void SwitchProfile(int direction)
    {
        if (!_config.EnableProfileButtons || direction == 0)
            return;
        _config.Normalize();
        if (!SaveActiveProfile())
        {
            Game1.addHUDMessage(new HUDMessage("Could not save the current hotkey profile.", HUDMessage.error_type));
            return;
        }
        var previousProfile = _config.ActiveProfile;
        var previousTabs = _config.Tabs;
        var previousColumns = _config.Columns;
        var previousRows = _config.Rows;
        var profiles = GetCreatedProfileNumbers();
        if (profiles.Count < 2)
            return;
        var currentIndex = profiles.IndexOf(_config.ActiveProfile);
        _config.ActiveProfile = profiles[(currentIndex + direction + profiles.Count) % profiles.Count];
        LogDebug($"User action: switched profile {previousProfile} -> {_config.ActiveProfile}.");
        if (!LoadActiveProfile())
        {
            _config.ActiveProfile = previousProfile;
            _config.Columns = previousColumns;
            _config.Rows = previousRows;
            _config.Tabs = previousTabs;
            Game1.addHUDMessage(new HUDMessage("Could not load the next hotkey profile.", HUDMessage.error_type));
            return;
        }
        _selectedHotkeyConfigIndex = 0;
        ClearChestDrag();
        _layoutDirty = true;
        Helper.WriteConfig(_config);
        RegisterGenericModConfigMenu();
        Game1.playSound("shwip");
        Game1.addHUDMessage(new HUDMessage(GetProfileDisplayText("Hotkey profile")));
    }

    private void SelectProfileFromConfigMenu(string value)
    {
        if (!int.TryParse(value, out var profileNumber) || !GetCreatedProfileNumbers().Contains(profileNumber)
            || profileNumber == _config.ActiveProfile)
            return;

        SaveConfig();
        var previousProfile = _config.ActiveProfile;
        var previousTabs = _config.Tabs;
        var previousColumns = _config.Columns;
        var previousRows = _config.Rows;
        _config.ActiveProfile = profileNumber;
        LogDebug($"User action: selected profile {profileNumber} in settings.");
        if (!LoadActiveProfile())
        {
            _config.ActiveProfile = previousProfile;
            _config.Tabs = previousTabs;
            _config.Columns = previousColumns;
            _config.Rows = previousRows;
            return;
        }

        _selectedHotkeyConfigIndex = 0;
        _layoutDirty = true;
        Helper.WriteConfig(_config);
        RegisterGenericModConfigMenu();
        Game1.playSound("shwip");
        Game1.addHUDMessage(new HUDMessage(GetProfileDisplayText("Hotkey profile")));
    }

    private void CreateProfileFromConfigMenu()
    {
        SaveConfig();
        var currentProfiles = GetCreatedProfileNumbers();
        if (currentProfiles.Count > 0 && currentProfiles.Max() == int.MaxValue)
        {
            Game1.addHUDMessage(new HUDMessage("Could not create another profile because the profile number limit was reached.", HUDMessage.error_type));
            return;
        }

        _config.ActiveProfile = currentProfiles.Count == 0 ? 1 : currentProfiles.Max() + 1;
        LogDebug($"User action: created profile {_config.ActiveProfile}.");
        _config.Tabs = new List<HubTab>();
        _config.Columns = 10;
        _config.Rows = 4;
        SaveActiveProfile();
        Helper.WriteConfig(_config);
        _selectedHotkeyConfigIndex = 0;
        _layoutDirty = true;
        RegisterGenericModConfigMenu();
        _gmcmApi?.OpenModMenu(ModManifest);
        Game1.playSound("coin");
        Game1.addHUDMessage(new HUDMessage(GetProfileDisplayText("Created hotkey profile")));
    }

    private void DeleteActiveProfileFromConfigMenu()
    {
        var profiles = GetCreatedProfileNumbers();
        if (profiles.Count <= 1)
        {
            Game1.addHUDMessage(new HUDMessage("You need to keep at least one hotkey profile.", HUDMessage.error_type));
            return;
        }
        int deletedProfile = _config.ActiveProfile;
        LogDebug($"User action: deleted profile {deletedProfile}.");
        EnsureProfileDirectory();
        Helper.Data.WriteJsonFile(ProfilePath(deletedProfile), new HotkeyProfile { IsCreated = false });
        _config.ProfileNumbers ??= new List<int>();
        _config.ProfileNumbers.Remove(deletedProfile);
        _config.ActiveProfile = profiles.First(number => number != deletedProfile);
        LoadActiveProfile();
        Helper.WriteConfig(_config);
        _selectedHotkeyConfigIndex = 0;
        _layoutDirty = true;
        RegisterGenericModConfigMenu();
        _gmcmApi?.OpenModMenu(ModManifest);
        Game1.playSound("trashcan");
        Game1.addHUDMessage(new HUDMessage($"Deleted hotkey profile {deletedProfile}; switched to profile {_config.ActiveProfile}"));
    }

    private string GetProfileDisplayText(string prefix)
    {
        return $"{prefix} {GetProfilePositionText()}";
    }

    private string GetProfilePositionText()
    {
        var profiles = GetCreatedProfileNumbers();
        return $"{profiles.IndexOf(_config.ActiveProfile) + 1}/{profiles.Count}";
    }
}

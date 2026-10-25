# Inventory hotkey prototype

Open the normal inventory with Escape. The add-hotkey button is drawn beside
the native inventory controls. Clicking it opens an editor overlay on the
inventory frame; choose a chat emoji, enter a name, assign a keybind, then
create the hotkey. Created hotkeys are shown in rows above the native inventory
controls and saved in the mod's `config.json`. Clicking one closes the inventory
and sends its configured key to the game after a short delay.

## Build

```powershell
dotnet build .\Taums-menu-supreme\Taums-menu-supreme.csproj --configuration Debug
```

Automatic deployment is disabled. Close the game, then copy the built DLL and
manifest into the game's Mods folder before launching SMAPI.

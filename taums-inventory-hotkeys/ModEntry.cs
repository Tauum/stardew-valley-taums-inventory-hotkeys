using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Taums_inventory_hotkeys;

/// <summary>Creates the configurable Inventory hub and owns its menu lifecycle.</summary>
public sealed partial class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<HubConfig>();
        _config.Normalize();
        LoadActiveProfile();
        _useControllerTooltipGlyphs = Game1.options.gamepadControls;
        Monitor.Log($"Taum's Inventory Hotkeys initialized. Game {Game1.version}; SMAPI {Constants.ApiVersion}.", LogLevel.Info);
        Monitor.Log($"Runtime assembly: '{GetType().Assembly.Location}'. Custom hotkeys supported by this build: 40.", LogLevel.Info);
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Input.CursorMoved += OnCursorMoved;
        helper.Events.Input.MouseWheelScrolled += OnMouseWheelScrolled;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenuTooltip;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Display.RenderingActiveMenu += OnRenderingActiveMenu;
        helper.Events.Display.MenuChanged += OnMenuChangedForBlur;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // SMAPI only allows API lookup after every mod has finished Entry.
        RegisterGenericModConfigMenu();
    }

}

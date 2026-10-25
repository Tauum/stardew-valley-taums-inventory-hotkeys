using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System.Reflection;
using System.Collections;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private bool IsGmcmConfigMenu(IClickableMenu menu)
    {
        if (menu.GetType().GetProperty("Manifest", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(menu) is IManifest manifest
            && manifest.UniqueID == ModManifest.UniqueID)
            return true;

        return menu.GetType().Assembly.GetName().Name == "GenericModConfigMenu"
            && menu.GetType().Name is ("SpecificModConfigMenu" or "ModConfigMenu");
    }

    private static object? GetGmcmConfigKeybindOverlay(IClickableMenu menu) =>
        menu.GetType().GetField("ActiveKeybindOverlay", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(menu);

    private static List<ClickableTextureComponent> GetGmcmConfigOverlayButtons(object overlay) =>
        overlay.GetType().GetField("Buttons", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(overlay)
            is IEnumerable buttons ? buttons.OfType<ClickableTextureComponent>().ToList() : new();

    private bool HandleGmcmConfigControllerButton(ButtonPressedEventArgs e)
    {
        if (!_gmcmControllerNavigationActive)
        {
            if (Game1.activeClickableMenu is not IClickableMenu currentMenu || !IsGmcmConfigMenu(currentMenu)
                || currentMenu.GetType().GetProperty("Manifest", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(currentMenu) is not IManifest manifest
                || manifest.UniqueID != ModManifest.UniqueID)
                return false;
            _gmcmControllerNavigationActive = true;
            _gmcmVirtualCursorInitialized = false;
        }

        if (Game1.activeClickableMenu is not IClickableMenu menu || !IsGmcmConfigMenu(menu))
        {
            _gmcmControllerNavigationActive = false;
            _waitForSettingsChordRelease = false;
            _gmcmVirtualCursorInitialized = false;
            return false;
        }

        if (!_gmcmVirtualCursorInitialized && GetGmcmFocusTargets(menu).Count < 3)
        {
            Game1.setMousePosition(Game1.uiViewport.Width / 2, Game1.uiViewport.Height / 2);
            _gmcmVirtualCursorInitialized = true;
        }

        if (_waitForSettingsChordRelease)
        {
            if (Helper.Input.IsDown(SButton.ControllerA) || Helper.Input.IsDown(SButton.ControllerX))
            {
                if (IsControllerButton(e.Button))
                    Helper.Input.Suppress(e.Button);
                return IsControllerButton(e.Button);
            }
            _waitForSettingsChordRelease = false;
        }

        if (GetGmcmConfigKeybindOverlay(menu) is { } overlay)
            return HandleGmcmConfigOverlayButton(e, menu, overlay);

        int horizontal = e.Button switch
        {
            SButton.DPadLeft or SButton.LeftThumbstickLeft or SButton.RightThumbstickLeft => -1,
            SButton.DPadRight or SButton.LeftThumbstickRight or SButton.RightThumbstickRight => 1,
            _ => 0
        };
        int vertical = e.Button switch
        {
            SButton.DPadUp or SButton.LeftThumbstickUp or SButton.RightThumbstickUp => -1,
            SButton.DPadDown or SButton.LeftThumbstickDown or SButton.RightThumbstickDown => 1,
            _ => 0
        };

        if (horizontal != 0 || vertical != 0)
        {
            MoveGmcmConfigFocus(menu, horizontal, vertical);
            Helper.Input.Suppress(e.Button);
            return true;
        }
        if (e.Button == SButton.ControllerA)
        {
            FocusGmcmConfigMenu(menu);
            Helper.Input.Press(SButton.MouseLeft);
            Helper.Input.Suppress(e.Button);
            return true;
        }
        if (e.Button is SButton.LeftShoulder or SButton.RightShoulder)
        {
            menu.receiveScrollWheelAction(e.Button == SButton.RightShoulder ? -120 : 120);
            Helper.Input.Suppress(e.Button);
            return true;
        }
        if (e.Button == SButton.ControllerB)
        {
            menu.receiveKeyPress(Keys.Escape);
            Helper.Input.Suppress(e.Button);
            return true;
        }
        return false;
    }

    private bool HandleGmcmConfigOverlayButton(ButtonPressedEventArgs e, IClickableMenu menu, object overlay)
    {
        // Once the prompt is recording a binding, pass the actual controller
        // input through to GMCM unchanged.
        if (overlay.GetType().GetField("KeybindEdit", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(overlay) is not null)
            return false;

        List<ClickableTextureComponent> buttons = GetGmcmConfigOverlayButtons(overlay);
        if (buttons.Count == 0)
            return false;

        ClickableTextureComponent current = buttons.FirstOrDefault(button => button.name == _gmcmConfigOverlayFocusedName)
            ?? buttons.FirstOrDefault(button => button.name == "Add") ?? buttons[0];
        _gmcmConfigOverlayFocusedName = current.name;
        int horizontal = e.Button switch
        {
            SButton.DPadLeft or SButton.LeftThumbstickLeft => -1,
            SButton.DPadRight or SButton.LeftThumbstickRight => 1,
            _ => 0
        };
        int vertical = e.Button switch
        {
            SButton.DPadUp or SButton.LeftThumbstickUp => -1,
            SButton.DPadDown or SButton.LeftThumbstickDown => 1,
            _ => 0
        };
        if (horizontal != 0 || vertical != 0)
        {
            Point origin = current.bounds.Center;
            ClickableTextureComponent? next = buttons.Where(button => !ReferenceEquals(button, current))
                .Select(button => new
                {
                    Button = button,
                    Along = horizontal != 0
                        ? (button.bounds.Center.X - origin.X) * horizontal
                        : (button.bounds.Center.Y - origin.Y) * vertical,
                    Across = horizontal != 0
                        ? Math.Abs(button.bounds.Center.Y - origin.Y)
                        : Math.Abs(button.bounds.Center.X - origin.X)
                })
                .Where(candidate => candidate.Along > 0)
                .OrderBy(candidate => candidate.Along + candidate.Across * 3)
                .Select(candidate => candidate.Button)
                .FirstOrDefault();
            if (next is not null)
                _gmcmConfigOverlayFocusedName = next.name;
            Helper.Input.Suppress(e.Button);
            return true;
        }
        if (e.Button == SButton.ControllerA)
        {
            menu.receiveLeftClick(current.bounds.Center.X, current.bounds.Center.Y);
            Helper.Input.Suppress(e.Button);
            return true;
        }
        if (e.Button == SButton.ControllerB && buttons.FirstOrDefault(button => button.name == "OK") is { } ok)
        {
            menu.receiveLeftClick(ok.bounds.Center.X, ok.bounds.Center.Y);
            Helper.Input.Suppress(e.Button);
            return true;
        }
        return false;
    }

    private static List<ClickableComponent> GetGmcmFocusTargets(IClickableMenu menu)
    {
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        return menu.allClickableComponents?
            .Where(component => component.visible && component.bounds.Width > 0 && component.bounds.Height > 0
                && component.bounds.Intersects(viewport))
            .ToList() ?? new List<ClickableComponent>();
    }

    private static void SnapGmcmConfigFocus(IClickableMenu menu, ClickableComponent component)
    {
        menu.currentlySnappedComponent = component;
        SnapCursorMethod?.Invoke(menu, null);
    }

    private static void FocusGmcmConfigMenu(IClickableMenu menu)
    {
        if (GetGmcmFocusTargets(menu).Count < 3)
            return;

        if (menu.currentlySnappedComponent is { visible: true } current
            && GetGmcmFocusTargets(menu).Contains(current))
            return;

        menu.populateClickableComponentList();
        menu.snapToDefaultClickableComponent();
        if (menu.currentlySnappedComponent is null)
        {
            ClickableComponent? first = GetGmcmFocusTargets(menu)
                .OrderBy(component => component.bounds.Top)
                .ThenBy(component => component.bounds.Left)
                .FirstOrDefault();
            if (first is not null)
                SnapGmcmConfigFocus(menu, first);
        }
    }

    private static void MoveGmcmConfigFocus(IClickableMenu menu, int horizontal, int vertical)
    {
        FocusGmcmConfigMenu(menu);
        List<ClickableComponent> targets = GetGmcmFocusTargets(menu);
        if (targets.Count < 3)
        {
            // GMCM 1.16 doesn't expose its option rows as snap components.
            // Move its normal cursor instead, so every widget remains reachable.
            const int step = 64;
            Game1.setMousePosition(
                Math.Clamp(Game1.getMouseX() + horizontal * step, 8, Game1.uiViewport.Width - 8),
                Math.Clamp(Game1.getMouseY() + vertical * step, 8, Game1.uiViewport.Height - 8));
            return;
        }
        ClickableComponent? current = menu.currentlySnappedComponent;
        if (current is null)
            return;

        Point origin = current.bounds.Center;
        ClickableComponent? next = targets
            .Where(component => !ReferenceEquals(component, current))
            .Select(component => new
            {
                Component = component,
                Along = horizontal != 0
                    ? (component.bounds.Center.X - origin.X) * horizontal
                    : (component.bounds.Center.Y - origin.Y) * vertical,
                Across = horizontal != 0
                    ? Math.Abs(component.bounds.Center.Y - origin.Y)
                    : Math.Abs(component.bounds.Center.X - origin.X)
            })
            .Where(candidate => candidate.Along > 0)
            .OrderBy(candidate => candidate.Along + candidate.Across * 3)
            .Select(candidate => candidate.Component)
            .FirstOrDefault();

        if (next is null && vertical != 0)
        {
            menu.receiveScrollWheelAction(vertical > 0 ? -120 : 120);
            menu.populateClickableComponentList();
            next = GetGmcmFocusTargets(menu)
                .OrderBy(component => Math.Abs(component.bounds.Center.X - origin.X))
                .ThenBy(component => vertical > 0 ? -component.bounds.Center.Y : component.bounds.Center.Y)
                .FirstOrDefault();
        }

        if (next is not null)
            SnapGmcmConfigFocus(menu, next);
    }

    private void DrawGmcmConfigFocus(SpriteBatch b)
    {
        if (!_gmcmControllerNavigationActive || Game1.activeClickableMenu is not IClickableMenu menu
            || !IsGmcmConfigMenu(menu))
            return;

        Rectangle bounds;
        if (GetGmcmConfigKeybindOverlay(menu) is { } overlay)
        {
            if (overlay.GetType().GetField("KeybindEdit", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(overlay) is not null)
                return;
            ClickableTextureComponent? button = GetGmcmConfigOverlayButtons(overlay)
                .FirstOrDefault(candidate => candidate.name == _gmcmConfigOverlayFocusedName);
            if (button is null)
                return;
            bounds = button.bounds;
        }
        else
        {
            if (GetGmcmFocusTargets(menu).Count < 3
                || menu.currentlySnappedComponent is not { visible: true } component)
                return;
            bounds = component.bounds;
        }

        const int border = 3;
        Color color = Color.Red;
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Y - border, bounds.Width + border * 2, border), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Bottom, bounds.Width + border * 2, border), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Y, border, bounds.Height), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.Right, bounds.Y, border, bounds.Height), color);
    }
}

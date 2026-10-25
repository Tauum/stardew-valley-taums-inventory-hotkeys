using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System.Collections;
using System.Reflection;

namespace Taums_menu_supreme;

/// <summary>Creates the configurable Inventory hub and owns its menu lifecycle.</summary>
public sealed class ModEntry : Mod
{
    private const int MaxCustomTabs = 40;
    // Stardew's chat emoji picker exposes IDs 0 through 195. The texture atlas
    // has additional unused cells, so scanning its alpha pixels adds entries
    // which the actual picker never offers (and shifts every later ID).
    private const int ChatEmojiCount = 196;

    private HubConfig _config = new();
    private GameMenu? _editorParent;
    private Rectangle _plusTabBounds;
    private ClickableComponent? _controllerAddHotkeyComponent;

    private bool _controllerAddHotkeyFocused;
    private bool _hotkeysVisible = true;
    private int? _pendingEditorReturnFocus;
    private bool _snappingControllerCursor;
    private Point? _pendingControllerCursorPosition;
    private bool _useControllerTooltipGlyphs;
    private GameMenu? _snapGraphMenu;
    private IClickableMenu? _snapGraphInventoryPage;
    private ClickableComponent? _snapGraphInventoryTab;
    private bool _addedInventoryTabToClickableSet;
    private int _originalInventoryTabLeftNeighborId;
    private const int AddHotkeySnapId = 987654;
    private const int EditorNameSnapId = 987655;
    private const int EditorIconPreviousSnapId = 987656;
    private const int EditorIconNextSnapId = 987657;
    private const int EditorIconDiceSnapId = 987658;
    private const int EditorKeybindSnapId = 987659;
    private const int EditorCancelSnapId = 987660;
    private const int EditorCreateSnapId = 987661;
    private const int EditorDeleteSnapId = 987662;
    private const int DeleteConfirmYesSnapId = 987663;
    private const int DeleteConfirmNoSnapId = 987664;
    private readonly List<(Rectangle bounds, int index)> _customTabBounds = new();
    private readonly Dictionary<int, ClickableComponent> _customHotkeyComponents = new();

    private Rectangle _editorBounds;
    private Rectangle _editorIconBounds;
    private Rectangle _editorIconCarouselBounds;
    private Rectangle _editorIconPreviousBounds;
    private Rectangle _editorIconNextBounds;
    private Rectangle _editorIconCounterBounds;
    private Rectangle _editorIconDiceBounds;
    private Rectangle _editorNameBounds;
    private Rectangle _editorNameHitBounds;
    private Rectangle _editorKeybindBounds;
    private Rectangle _editorKeybindHitBounds;
    private Rectangle _editorCreateBounds;
    private Rectangle _editorCancelBounds;
    private Rectangle _editorDeleteBounds;
    private Rectangle _deleteConfirmBounds;
    private Rectangle _deleteConfirmYesBounds;
    private Rectangle _deleteConfirmNoBounds;

    private int _draftIcon;
    private int _editingTabIndex = -1;
    private bool _confirmingDelete;
    private KeybindList _draftKeybind = KeybindList.Parse("");
    private string _draftName = "New hotkey";
    private bool _nameEditFocused;
    private bool _namePrefillSelected = true;
    private bool _capturingDraftKeybind;
    private Rectangle _keybindCaptureBounds;
    private object? _gmcmKeybindOverlay;
    private Type? _gmcmKeybindOverlayType;
    private string _gmcmFocusedButtonName = "Add";
    private bool _skipGmcmControllerAOnButtonsChanged;
    private Texture2D? _emojiTexture;
    private readonly List<int> _emojiSpriteIndices = new();
    private readonly HashSet<SButton> _simulatedButtonsAwaitingDelivery = new();
    private readonly Dictionary<SButton, int> _simulatedButtonsAwaitingChangedEvent = new();
    private PendingKeybind? _pendingKeybind;
    private int _emojiCount;
    private IGenericModConfigMenuApi? _gmcmApi;
    private static readonly FieldInfo[] GameMenuFields = typeof(GameMenu).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? CurrentTabField = typeof(GameMenu).GetField("currentTab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo? CurrentTabProperty = typeof(GameMenu).GetProperty("currentTab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<HubConfig>();
        _config.Normalize();
        _useControllerTooltipGlyphs = Game1.options.gamepadControls;
        Monitor.Log($"Taum's Inventory Hotkeys initialized. Game {Game1.version}; SMAPI {Constants.ApiVersion}.", LogLevel.Info);
        Monitor.Log($"Runtime assembly: '{GetType().Assembly.Location}'. Custom hotkeys supported by this build: 40.", LogLevel.Info);
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Input.CursorMoved += OnCursorMoved;
        helper.Events.Input.MouseWheelScrolled += OnMouseWheelScrolled;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // SMAPI only allows API lookup after every mod has finished Entry.
        RegisterGenericModConfigMenu();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        _useControllerTooltipGlyphs = IsControllerButton(e.Button);
        // if (_simulatedButtonsAwaitingDelivery.Remove(e.Button))
            // Monitor.Log($"SMAPI delivered simulated key as ButtonPressed: {e.Button}.", LogLevel.Info);

        if (!Context.IsWorldReady)
            return;

        if (Game1.activeClickableMenu is DeleteConfirmationMenu deleteMenu)
        {
            if (TryGetEditorDirection(e.Button, out var deleteHorizontal, out var deleteVertical))
                deleteMenu.MoveFocus(deleteHorizontal, deleteVertical);
            else if (e.Button is SButton.ControllerA or SButton.Enter or SButton.Space)
                deleteMenu.ActivateFocused();
            else if (e.Button is SButton.ControllerB or SButton.B or SButton.Escape)
                CancelDeleteConfirmation();
            else if (e.Button == SButton.MouseLeft)
            {
                var click = e.Cursor.GetScaledScreenPixels().ToPoint();
                deleteMenu.receiveLeftClick(click.X, click.Y);
            }

            Helper.Input.Suppress(e.Button);
            return;
        }

        if (Game1.activeClickableMenu is HotkeyNamingMenu namingMenu
            && (e.Button is SButton.ControllerB or SButton.Escape))
        {
            namingMenu.Cancel();
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (Game1.activeClickableMenu is InventoryTabEditorMenu)
        {
            if (Game1.activeClickableMenu is InventoryTabEditorMenu navigatingEditor
                && _gmcmKeybindOverlay is null
                && !_capturingDraftKeybind
                && TryGetEditorDirection(e.Button, out var horizontal, out var vertical))
            {
                navigatingEditor.MoveFocus(horizontal, vertical);
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (e.Button == SButton.ControllerA
                && Game1.activeClickableMenu is InventoryTabEditorMenu nameEditor
                && _gmcmKeybindOverlay is null
                && !_capturingDraftKeybind)
            {
                nameEditor.ActivateFocused();
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (_gmcmKeybindOverlay is not null)
            {
                if (e.Button is SButton.Escape or SButton.ControllerB or SButton.B)
                {
                    // Monitor.Log($"Cancelled GMCM keybind capture with '{e.Button}'; retained draft keybind='{_draftKeybind}'.", LogLevel.Info);
                    _gmcmKeybindOverlay = null;
                    Helper.Input.Suppress(e.Button);
                    return;
                }

                if (!IsGmcmCapturingKeybind())
                {
                    if (TryGetEditorDirection(e.Button, out var overlayHorizontal, out var overlayVertical))
                    {
                        MoveGmcmFocus(overlayHorizontal, overlayVertical);
                        Helper.Input.Suppress(e.Button);
                        return;
                    }
                    if (e.Button == SButton.ControllerA)
                    {
                        ActivateGmcmFocusedButton();
                        _skipGmcmControllerAOnButtonsChanged = true;
                        Helper.Input.Suppress(e.Button);
                        return;
                    }
                }

                if (e.Button == SButton.MouseLeft)
                {
                    Point overlayCursor = e.Cursor.GetScaledScreenPixels().ToPoint();
                    // Monitor.Log($"Keybind sub-modal click at ({overlayCursor.X},{overlayCursor.Y}); forwarding to GMCM overlay.", LogLevel.Info);
                    InvokeGmcmOverlay("OnLeftClick", overlayCursor.X, overlayCursor.Y);
                }
                else if (e.Button is SButton.ControllerA or SButton.ControllerX or SButton.ControllerY
                    or SButton.DPadUp or SButton.DPadDown or SButton.DPadLeft or SButton.DPadRight
                    or SButton.LeftThumbstickUp or SButton.LeftThumbstickDown or SButton.LeftThumbstickLeft or SButton.LeftThumbstickRight)
                {
                    // GMCM's keybind overlay is a capture prompt: the controller
                    // button becomes the selected binding through ButtonsChanged.
                    // Monitor.Log($"Controller input '{e.Button}' received during GMCM capture; forwarding via ButtonsChanged.", LogLevel.Info);
                }
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (_capturingDraftKeybind)
            {
                if (e.Button is SButton.Escape or SButton.ControllerB or SButton.B)
                {
                    _capturingDraftKeybind = false;
                }
                else if (e.Button != SButton.MouseLeft)
                {
                    _draftKeybind = KeybindList.Parse(e.Button.ToString());
                    _capturingDraftKeybind = false;
                    // Monitor.Log($"Fallback key capture accepted '{e.Button}'; draft keybind is now '{_draftKeybind}'.", LogLevel.Info);
                }
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (e.Button is SButton.Escape or SButton.ControllerB or SButton.B)
            {
                CloseEditor();
            }
            else if (e.Button == SButton.MouseLeft)
            {
                // Use the same scaled screen coordinates as the inventory-tab
                // click handler, but fall back to Game1's menu coordinates when
                // the active UI scale makes the event cursor differ.
                var cursor = e.Cursor.GetScaledScreenPixels().ToPoint();
                if (!IsEditorInteractivePoint(cursor))
                {
                    Point gameCursor = new(Game1.getMouseX(), Game1.getMouseY());
                    if (IsEditorInteractivePoint(gameCursor))
                        cursor = gameCursor;
                }
                HandleEditorClick(cursor.X, cursor.Y);
                Helper.Input.Suppress(e.Button);
                return;
            }
            else if (!_nameEditFocused && e.Button is SButton.Left or SButton.Right)
                CycleDraftIcon(e.Button == SButton.Left ? -1 : 1);
            else if (_nameEditFocused)
                HandleNameTyping(e.Button);

            Helper.Input.Suppress(e.Button);
            return;
        }

        if (Game1.activeClickableMenu is GameMenu menu)
        {
            if (IsInventoryPage(menu))
            {
                if ((e.Button is SButton.ControllerY or SButton.MouseMiddle)
                    && IsAddHotkeyHighlighted(menu, e.Button == SButton.MouseMiddle))
                {
                    SetHotkeysVisible(!_hotkeysVisible);
                    Helper.Input.Suppress(e.Button);
                    return;
                }

                var inventoryPage = GetInventoryPage(menu);
                var focusedComponent = inventoryPage?.currentlySnappedComponent;
                if (TryGetEditorDirection(e.Button, out var horizontal, out var vertical))
                {
                    if (TryGetHotkeyComponentIndex(focusedComponent, out var focusedHotkeyIndex))
                    {
                        if (horizontal != 0)
                        {
                            var destination = focusedHotkeyIndex + horizontal;
                            if (destination >= 0 && destination < _config.Tabs.Count && destination / 10 == focusedHotkeyIndex / 10)
                                FocusHotkey(menu, destination);
                        }
                        else if (vertical < 0 && focusedHotkeyIndex + 10 < _config.Tabs.Count)
                            FocusHotkey(menu, focusedHotkeyIndex + 10);
                        else if (vertical > 0 && focusedHotkeyIndex >= 10)
                            FocusHotkey(menu, focusedHotkeyIndex - 10);
                        else if (vertical > 0)
                            FocusNearestNativeTab(menu, focusedComponent!.bounds.Center.X);
                        Helper.Input.Suppress(e.Button);
                        return;
                    }

                    if (_hotkeysVisible && vertical < 0 && TryGetNativeTabIndex(menu, focusedComponent, out _)
                        && TryGetHotkeyNearestToTab(menu, focusedComponent!, out var hotkeyIndex))
                    {
                        FocusHotkey(menu, hotkeyIndex);
                        Helper.Input.Suppress(e.Button);
                        return;
                    }

                    if (_hotkeysVisible && vertical > 0 && ReferenceEquals(focusedComponent, _controllerAddHotkeyComponent)
                        && _customTabBounds.Count > 0)
                    {
                        FocusHotkey(menu, 0);
                        Helper.Input.Suppress(e.Button);
                        return;
                    }
                }

                var isLeftInput = e.Button is SButton.DPadLeft or SButton.LeftThumbstickLeft or SButton.Left;
                // if (isLeftInput)
                // {
                //     var page = GetInventoryPage(menu);
                //     var pageFocus = page?.currentlySnappedComponent;
                //     var menuFocus = menu.currentlySnappedComponent;
                //     Monitor.Log($"Inventory left input '{e.Button}': pageFocus='{pageFocus?.name}'/{pageFocus?.myID} {pageFocus?.bounds}, menuFocus='{menuFocus?.name}'/{menuFocus?.myID} {menuFocus?.bounds}, inventoryTab='{GetNativeInventoryTab(menu)?.name}'/{GetNativeInventoryTab(menu)?.myID} {GetNativeInventoryTab(menu)?.bounds}.", LogLevel.Info);
                // }
                if (isLeftInput && IsInventoryTabFocused(menu))
                {
                    FocusAddHotkey(menu);
                    Helper.Input.Suppress(e.Button);
                    return;
                }

                if ((_controllerAddHotkeyFocused
                    || (_controllerAddHotkeyComponent is not null
                        && GetInventoryPage(menu)?.currentlySnappedComponent == _controllerAddHotkeyComponent))
                    && e.Button == SButton.ControllerA)
                {
                    _controllerAddHotkeyFocused = false;
                    if (_config.Tabs.Count >= MaxCustomTabs)
                    {
                        Game1.addHUDMessage(new HUDMessage("You can't add another hotkey. All four rows are full.", HUDMessage.error_type));
                    }
                    else
                    {
                        OpenEditor(menu);
                    }
                    Helper.Input.Suppress(e.Button);
                    return;
                }

                if (_hotkeysVisible && e.Button == SButton.ControllerA
                    && TryGetHotkeyComponentIndex(inventoryPage?.currentlySnappedComponent, out int activatedHotkeyIndex))
                {
                    ActivateHotkey(activatedHotkeyIndex);
                    Helper.Input.Suppress(e.Button);
                    return;
                }

                if (_hotkeysVisible && e.Button == SButton.ControllerX
                    && TryGetHotkeyComponentIndex(inventoryPage?.currentlySnappedComponent, out int editedHotkeyIndex))
                {
                    OpenEditor(menu, editedHotkeyIndex);
                    Helper.Input.Suppress(e.Button);
                    return;
                }
            }

            if (e.Button is SButton.MouseLeft or SButton.MouseRight)
            {
                _controllerAddHotkeyFocused = false;
                var cursor = e.Cursor.GetScaledScreenPixels().ToPoint();
                var x = cursor.X;
                var y = cursor.Y;
                if (e.Button == SButton.MouseLeft && _plusTabBounds.Contains(x, y))
                {
                    if (_config.Tabs.Count >= MaxCustomTabs)
                    {
                        Game1.addHUDMessage(new HUDMessage("You can't add another hotkey. All four rows are full.", HUDMessage.error_type));
                        // Monitor.Log($"Blocked add-hotkey request: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
                        Helper.Input.Suppress(e.Button);
                        return;
                    }
                    OpenEditor(menu);
                    Helper.Input.Suppress(e.Button);
                    return;
                }
                if (!_hotkeysVisible)
                    return;

                foreach (var tab in _customTabBounds)
                {
                    if (!tab.bounds.Contains(x, y)) continue;
                    if (e.Button == SButton.MouseRight)
                    {
                        OpenEditor(menu, tab.index);
                        Helper.Input.Suppress(e.Button);
                        return;
                    }

                    ActivateHotkey(tab.index);
                    Helper.Input.Suppress(e.Button);
                    return;
                }
            }
        }
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not GameMenu gameMenu)
            return;

        RepositionTabs(gameMenu);
        if (!IsInventoryPage(gameMenu))
        {
            _pendingEditorReturnFocus = null;
            RemoveAddHotkeyFromSnapGraph(gameMenu);
            _customTabBounds.Clear();
            _plusTabBounds = Rectangle.Empty;
            return;
        }
        AddHotkeyToSnapGraph(gameMenu, GetNativeTabBounds(gameMenu, _plusTabBounds.Y));
        if (_pendingEditorReturnFocus is int returnFocus)
        {
            _pendingEditorReturnFocus = null;
            if (returnFocus < 0)
                FocusInventoryTab(gameMenu);
            else
                FocusHotkey(gameMenu, returnFocus);
        }
        _controllerAddHotkeyFocused = _controllerAddHotkeyComponent is not null
            && GetInventoryPage(gameMenu)?.currentlySnappedComponent == _controllerAddHotkeyComponent;
        if (_hotkeysVisible)
            DrawCustomTabs(e.SpriteBatch);
        RedrawNativeTabs(e.SpriteBatch, gameMenu);
        DrawPlusTab(e.SpriteBatch);
        DrawTabTooltip(e.SpriteBatch);
    }

    private static bool IsInventoryPage(GameMenu menu)
    {
        // GameMenu's first page is Inventory. Read the live selected tab so
        // the custom row only exists while that page is active.
        if (CurrentTabField?.GetValue(menu) is int tabIndex)
            return tabIndex == 0;

        if (CurrentTabProperty?.GetValue(menu) is int propertyTabIndex)
            return propertyTabIndex == 0;

        // Keep the controls hidden if the game changes its internal page API;
        // showing them on the wrong page is more confusing than hiding them.
        return false;
    }

    private static IClickableMenu? GetInventoryPage(GameMenu menu)
    {
        // Don't depend on the backing field name: Stardew's internal page
        // collection has changed between game builds. Find the inventory page
        // among the menu's collections, using the current tab index as fallback.
        var currentTab = CurrentTabField?.GetValue(menu) as int? ?? 0;
        foreach (var field in GameMenuFields)
        {
            var value = field.GetValue(menu);
            if (value is IClickableMenu direct && direct.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
                return direct;
            if (value is not IEnumerable collection || value is string)
                continue;

            var pages = collection.OfType<IClickableMenu>().ToList();
            if (pages.Count == 0)
                continue;
            var inventory = pages.FirstOrDefault(page =>
                page.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase));
            if (inventory is not null)
                return inventory;
            if (currentTab >= 0 && currentTab < pages.Count)
                return pages[currentTab];
        }
        return null;
    }

    private bool IsInventoryTabFocused(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var snapped = page?.currentlySnappedComponent ?? menu.currentlySnappedComponent;
        if (snapped is null)
            return false;

        var tab = GetNativeInventoryTab(menu);
        var pageFocus = page?.currentlySnappedComponent;
        var menuFocus = menu.currentlySnappedComponent;
        bool IsInventoryTab(ClickableComponent? component) => component is not null
            && (ReferenceEquals(component, tab)
                || (tab is not null && component.myID == tab.myID)
                || component.name.Equals("inventory", StringComparison.OrdinalIgnoreCase)
                || (tab is not null && component.bounds == tab.bounds));
        var focused = IsInventoryTab(pageFocus) || IsInventoryTab(menuFocus);
        // if (focused)
            // Monitor.Log($"Controller-left from Inventory focus detected (page='{pageFocus?.name}'/{pageFocus?.myID}, menu='{menuFocus?.name}'/{menuFocus?.myID}, tab='{tab?.name}'/{tab?.myID}).", LogLevel.Info);
        return focused;
    }

    private static ClickableComponent? GetNativeInventoryTab(GameMenu menu)
    {
        // GameMenu's tabs list is the authoritative source. Geometry-based
        // matching is ambiguous because page controls can share those bounds.
        var tabsField = GameMenuFields.FirstOrDefault(field => field.Name == "tabs");
        if (tabsField?.GetValue(menu) is IList tabs && tabs.Count > 0)
            return tabs[0] as ClickableComponent;
        return null;
    }

    private void FocusAddHotkey(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        if (page is null)
        {
            // Monitor.Log("Could not route controller focus to Add hotkey: Inventory page is missing.", LogLevel.Warn);
            return;
        }

        var inventoryTab = GetNativeInventoryTab(menu);
        if (_plusTabBounds.IsEmpty && inventoryTab is not null)
            _plusTabBounds = new Rectangle(inventoryTab.bounds.Left - inventoryTab.bounds.Width + 28, inventoryTab.bounds.Y + 6, inventoryTab.bounds.Width, inventoryTab.bounds.Height);
        _controllerAddHotkeyComponent ??= new ClickableComponent(_plusTabBounds, "Add hotkey")
        {
            myID = AddHotkeySnapId,
            rightNeighborID = inventoryTab?.myID ?? ClickableComponent.SNAP_AUTOMATIC,
            leftNeighborID = inventoryTab?.myID ?? ClickableComponent.SNAP_AUTOMATIC
        };
        _controllerAddHotkeyComponent.bounds = _plusTabBounds;
        if (page.allClickableComponents is not null && !page.allClickableComponents.Contains(_controllerAddHotkeyComponent))
            page.allClickableComponents.Add(_controllerAddHotkeyComponent);
        page.currentlySnappedComponent = _controllerAddHotkeyComponent;
        menu.currentlySnappedComponent = _controllerAddHotkeyComponent;
        _controllerAddHotkeyFocused = true;

        // Use the game's standard snap cursor method so the visual cursor and
        // hover tooltip follow the component for both keyboard and controller.
        var snapCursor = typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        SnapControllerCursor(page, snapCursor);
        _useControllerTooltipGlyphs = true;
        // Monitor.Log($"Controller focus moved directly to Add hotkey (page={page.GetType().Name}, pageComponents={page.allClickableComponents?.Count ?? -1}, id={AddHotkeySnapId}, bounds={_plusTabBounds}).", LogLevel.Info);
    }

    private void FocusHotkey(GameMenu menu, int index)
    {
        var page = GetInventoryPage(menu);
        if (page is null || !_customHotkeyComponents.TryGetValue(index, out ClickableComponent? component))
            return;

        page.currentlySnappedComponent = component;
        menu.currentlySnappedComponent = component;
        _controllerAddHotkeyFocused = false;
        var snapCursor = typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        SnapControllerCursor(page, snapCursor);
        // Monitor.Log($"Controller focus moved to hotkey [{index}] '{_config.Tabs[index].Name}' at {component.bounds}.", LogLevel.Trace);
    }

    private bool TryGetHotkeyComponentIndex(ClickableComponent? component, out int index)
    {
        index = -1;
        if (component is null)
            return false;

        foreach ((int hotkeyIndex, ClickableComponent hotkeyComponent) in _customHotkeyComponents)
        {
            if (!ReferenceEquals(component, hotkeyComponent) && component.myID != hotkeyComponent.myID)
                continue;
            index = hotkeyIndex;
            return index >= 0 && index < _config.Tabs.Count;
        }
        return false;
    }

    private static bool TryGetNativeTabIndex(GameMenu menu, ClickableComponent? component, out int index)
    {
        index = -1;
        if (component is null || GetNativeTabComponents(menu) is not IList tabs)
            return false;

        for (var i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] is not ClickableComponent tab)
                continue;
            if (!ReferenceEquals(component, tab) && component.myID != tab.myID)
                continue;
            index = i;
            return true;
        }
        return false;
    }

    private bool TryGetHotkeyNearestToTab(GameMenu menu, ClickableComponent tab, out int index)
    {
        index = -1;
        if (_customTabBounds.Count == 0 || !TryGetNativeTabIndex(menu, tab, out _))
            return false;

        var firstRowCount = Math.Min(10, _customTabBounds.Count);
        index = _customTabBounds.Take(firstRowCount)
            .OrderBy(entry => Math.Abs(entry.bounds.Center.X - tab.bounds.Center.X))
            .First().index;
        return true;
    }

    private void FocusNearestNativeTab(GameMenu menu, int x)
    {
        var page = GetInventoryPage(menu);
        var tab = (GetNativeTabComponents(menu) as IList)?.OfType<ClickableComponent>()
            .Where(component => component.visible)
            .OrderBy(component => Math.Abs(component.bounds.Center.X - x))
            .FirstOrDefault();
        if (page is null || tab is null)
            return;

        page.currentlySnappedComponent = tab;
        menu.currentlySnappedComponent = tab;
        _controllerAddHotkeyFocused = false;
        var snapCursor = typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        SnapControllerCursor(page, snapCursor);
    }

    private void FocusInventoryTab(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        var tab = GetNativeInventoryTab(menu);
        if (page is null || tab is null)
            return;

        page.currentlySnappedComponent = tab;
        menu.currentlySnappedComponent = tab;
        _controllerAddHotkeyFocused = false;
        var snapCursor = typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        SnapControllerCursor(page, snapCursor);
    }

    private bool IsAddHotkeyHighlighted(GameMenu menu, bool mouseInput)
    {
        var focused = GetInventoryPage(menu)?.currentlySnappedComponent;
        if (!mouseInput && _controllerAddHotkeyComponent is not null
            && (ReferenceEquals(focused, _controllerAddHotkeyComponent) || focused?.myID == AddHotkeySnapId))
            return true;

        return mouseInput && _plusTabBounds.Contains(Game1.getMouseX(), Game1.getMouseY());
    }

    private void SnapControllerCursor(IClickableMenu page, MethodInfo? snapCursor)
    {
        _pendingControllerCursorPosition = page.currentlySnappedComponent?.bounds.Center;
        _snappingControllerCursor = true;
        try { snapCursor?.Invoke(page, null); }
        finally { _snappingControllerCursor = false; }
    }

    private void SetHotkeysVisible(bool visible)
    {
        _hotkeysVisible = visible;
        // Monitor.Log($"{(visible ? "Showing" : "Hiding")} custom hotkeys via {(_useControllerTooltipGlyphs ? "controller Y" : "middle mouse button")}.", LogLevel.Info);
    }

    private void ActivateHotkey(int index)
    {
        if (index < 0 || index >= _config.Tabs.Count)
            return;

        var selectedTab = _config.Tabs[index];
        // Monitor.Log($"Hotkey activated: index={index}, name='{selectedTab.Name}', keybind='{selectedTab.Keybinds}', bound={selectedTab.Keybinds.IsBound}.", LogLevel.Info);

        if (!selectedTab.Keybinds.IsBound)
        {
            // Monitor.Log($"Hotkey '{selectedTab.Name}' has no assigned keybind; no key was sent.", LogLevel.Info);
            return;
        }

        _pendingKeybind = new PendingKeybind(selectedTab.Keybinds, 2);
        Game1.exitActiveMenu();
        // Monitor.Log($"Closed inventory for hotkey '{selectedTab.Name}'; will dispatch '{selectedTab.Keybinds}' after a short delay.", LogLevel.Info);
    }


    private void RepositionTabs(GameMenu menu)
    {
        // Place the add-hotkey button directly beside the native Inventory tab.
        var tabY = menu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY + 64;
        List<Rectangle> nativeTabs = GetNativeTabBounds(menu, tabY);
        Rectangle? leftmostNativeTab = nativeTabs.Count > 0 ? nativeTabs[0] : null;
        var rowWidth = leftmostNativeTab?.Width ?? 64;
        var rowHeight = leftmostNativeTab?.Height ?? 64;
        var nativeLeft = leftmostNativeTab?.Left ?? menu.xPositionOnScreen + 48;
        int[] nativeColumnAnchors = nativeTabs.Select(bounds => bounds.Left).Distinct().OrderBy(x => x).ToArray();
        // Align each custom-tab column to the actual inventory item slots shown
        // below the tab strip. Fall back to native tab anchors if the game
        // changes its inventory page internals.
        int[] inventoryColumnAnchors = GetInventoryColumnAnchors(menu, tabY);
        int[] columnAnchors = inventoryColumnAnchors.Length >= 10 ? inventoryColumnAnchors : nativeColumnAnchors;
        var columnSpacing = GetColumnSpacing(columnAnchors, rowWidth);
        _plusTabBounds = leftmostNativeTab is Rectangle nativeTab
            ? new Rectangle(nativeTab.Left - rowWidth + 28, nativeTab.Y + 6, rowWidth, nativeTab.Height)
            : new Rectangle(nativeLeft - rowWidth + 24, tabY + 4, rowWidth, rowWidth);
        // The custom row matches the native 64px tabs and overlaps the native
        // row by four pixels, like a stacked tab strip. It is drawn first so
        // the native tabs cover its lower edge.
        var customY = tabY - rowHeight + 4;
        _customTabBounds.Clear();
        const int maxPerRow = 10;
        for (var i = 0; i < _config.Tabs.Count; i++)
        {
            var row = i / maxPerRow;
            var col = i % maxPerRow;
            // Reuse the exact X positions from the inventory slot columns;
            // extrapolate only if fewer than ten anchors were exposed.
            var columnLeft = col < columnAnchors.Length
                ? columnAnchors[col]
                : columnAnchors.Length > 0
                    ? columnAnchors[^1] + (col - columnAnchors.Length + 1) * columnSpacing
                    : nativeLeft + col * columnSpacing;
            // Inventory slot anchors sit a few pixels left of the native tab art.
            var bounds = new Rectangle(columnLeft + 8, customY - row * rowHeight, rowWidth, rowHeight);
            _customTabBounds.Add((bounds, i));
        }
        UpdateHotkeySnapComponents(menu);
        var editorWidth = Math.Min(760, menu.width - 48);
        const int confirmationSize = 52;
        const int confirmationGap = 10;
        var editorHeight = Math.Min(680, menu.height - 32);
        _editorBounds = new Rectangle(menu.xPositionOnScreen + (menu.width - editorWidth) / 2,
            menu.yPositionOnScreen + (menu.height - editorHeight) / 2, editorWidth, editorHeight);
        _editorNameBounds = new Rectangle(_editorBounds.X + 184, _editorBounds.Y + 164, _editorBounds.Width - 360, 72);
        _editorNameHitBounds = new Rectangle(_editorNameBounds.X - 48, _editorNameBounds.Y - 56,
            _editorNameBounds.Width + 96, _editorNameBounds.Height + 112);
        _editorIconCarouselBounds = new Rectangle(_editorBounds.X + 32, _editorBounds.Y + 276, _editorBounds.Width - 64, 124);
        // Keep the carousel controls on the right half of the row and reserve
        // clear space at the far right for its position counter.
        var iconGroupCenterX = _editorIconCarouselBounds.Center.X + 8;
        var iconRowCenterY = _editorIconCarouselBounds.Y + 78;
        _editorIconPreviousBounds = new Rectangle(iconGroupCenterX - 104, iconRowCenterY - 24, 48, 48);
        _editorIconBounds = new Rectangle(iconGroupCenterX - 48, iconRowCenterY - 44, 96, 88);
        _editorIconNextBounds = new Rectangle(iconGroupCenterX + 56, iconRowCenterY - 24, 48, 48);
        _editorIconCounterBounds = new Rectangle(_editorIconNextBounds.Right + 12, iconRowCenterY - 30, 80, 60);
        _editorIconDiceBounds = new Rectangle(_editorIconCounterBounds.Right + 8, iconRowCenterY - 22, 44, 44);
        _editorKeybindBounds = new Rectangle(_editorBounds.Right - 360, _editorBounds.Y + 456, 208, 96);
        _editorKeybindHitBounds = _editorKeybindBounds;
        _keybindCaptureBounds = new Rectangle(_editorBounds.Center.X - 280, _editorBounds.Center.Y - 100, 560, 200);
        var confirmationY = _editorBounds.Bottom - confirmationSize - 16;
        var confirmationX = _editorBounds.Right - confirmationSize - 13;
        _editorCreateBounds = new Rectangle(confirmationX, confirmationY, confirmationSize, confirmationSize);
        _editorCancelBounds = new Rectangle(_editorCreateBounds.X - confirmationGap - confirmationSize, confirmationY, confirmationSize, confirmationSize);
        _editorDeleteBounds = new Rectangle(_editorCancelBounds.X - confirmationGap - confirmationSize,
            confirmationY, confirmationSize, confirmationSize);
        _deleteConfirmBounds = new Rectangle(_editorBounds.Center.X - 260, _editorBounds.Center.Y - 110, 520, 220);
        const int deleteButtonWidth = 104;
        const int deleteButtonGap = 12;
        var deleteButtonsWidth = deleteButtonWidth * 2 + deleteButtonGap;
        var deleteButtonsLeft = _deleteConfirmBounds.Center.X - deleteButtonsWidth / 2;
        var deleteButtonsY = _deleteConfirmBounds.Bottom - 76;
        _deleteConfirmYesBounds = new Rectangle(deleteButtonsLeft, deleteButtonsY, deleteButtonWidth, 48);
        _deleteConfirmNoBounds = new Rectangle(deleteButtonsLeft + deleteButtonWidth + deleteButtonGap,
            deleteButtonsY, deleteButtonWidth, 48);

    }

    private void DrawPlusTab(SpriteBatch b)
    {
        DrawVanillaTabBackground(b, _plusTabBounds);
        // Use the vanilla Energy HUD cap from LooseSprites/Cursors. The old
        // source rectangle was a harvesting mouse cursor, which looked like a
        // pointer placed on top of the tab icon.
        Rectangle energySource = new(256, 408, 12, 16);
        const int iconWidth = 30;
        const int iconHeight = 40;
        Rectangle iconBounds = new(_plusTabBounds.Center.X - iconWidth / 2, _plusTabBounds.Center.Y - iconHeight / 2, iconWidth, iconHeight);
        b.Draw(Game1.mouseCursors, iconBounds, energySource, Color.White);
    }

    private void DrawCustomTabs(SpriteBatch b)
    {
        foreach (var entry in _customTabBounds)
        {
            DrawVanillaTabBackground(b, entry.bounds);
            // The native tab art has a slightly top-heavy silhouette; lower
            // the centered emoji a few pixels to match the native item-icon balance.
            Rectangle iconBounds = new(entry.bounds.X, entry.bounds.Y + 4, entry.bounds.Width, entry.bounds.Height);
            DrawEmojiIcon(b, _config.Tabs[entry.index].Icon, iconBounds, maxSize: 38);
        }
    }

    private static void DrawVanillaTabBackground(SpriteBatch b, Rectangle bounds)
    {
        b.Draw(Game1.mouseCursors, bounds, new Rectangle(16, 368, 16, 16), Color.White);
    }

    private void OpenEditor(GameMenu parent, int tabIndex = -1)
    {
        if (tabIndex < 0 && _config.Tabs.Count >= MaxCustomTabs)
        {
            Game1.addHUDMessage(new HUDMessage("You can't add another hotkey. All four rows are full.", HUDMessage.error_type));
            // Monitor.Log($"Blocked add-hotkey editor: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
            return;
        }

        EnsureEmojiIcons();
        _editingTabIndex = tabIndex;
        _confirmingDelete = false;
        if (tabIndex >= 0 && tabIndex < _config.Tabs.Count)
        {
            var selected = _config.Tabs[tabIndex];
            _draftIcon = selected.Icon;
            _draftKeybind = selected.Keybinds;
            _draftName = selected.Name;
            // Monitor.Log($"Opened hotkey editor for [{tabIndex}] '{_draftName}'.", LogLevel.Info);
        }
        else
        {
            _editingTabIndex = -1;
            _draftIcon = _emojiCount == 0 ? 0 : _config.Tabs.Count % _emojiCount;
            _draftKeybind = KeybindList.Parse("");
            _draftName = "New hotkey";
            // Monitor.Log($"Opened add-hotkey editor. Initial draft: name='{_draftName}', iconId={_draftIcon}, keybind='{_draftKeybind}'.", LogLevel.Info);
        }
        _nameEditFocused = true;
        _namePrefillSelected = true;
        _editorParent = parent;
        RepositionTabs(parent);
        Game1.activeClickableMenu = new InventoryTabEditorMenu(this, parent);
    }

    private void OpenNativeNameInput(InventoryTabEditorMenu editor)
    {
        // Monitor.Log($"Opening Stardew name input for hotkey draft '{_draftName}'.", LogLevel.Info);
        // Game1.activeClickableMenu = new HotkeyNamingMenu(editor, name =>
        Game1.activeClickableMenu = new NamingMenu(name =>
        {
            _draftName = string.IsNullOrWhiteSpace(name) ? "New hotkey" : name.Trim();
            _namePrefillSelected = false;
            _nameEditFocused = true;
            Game1.activeClickableMenu = editor;
            editor.FocusNameInput();
            // Monitor.Log($"Stardew name input accepted '{_draftName}'.", LogLevel.Info);
        // }, _draftName);
        }, "Hotkey name", _draftName);
    }

    private static bool TryGetEditorDirection(SButton button, out int horizontal, out int vertical)
    {
        horizontal = vertical = 0;
        if (button is SButton.DPadLeft or SButton.LeftThumbstickLeft or SButton.Left) horizontal = -1;
        else if (button is SButton.DPadRight or SButton.LeftThumbstickRight or SButton.Right) horizontal = 1;
        else if (button is SButton.DPadUp or SButton.LeftThumbstickUp or SButton.Up) vertical = -1;
        else if (button is SButton.DPadDown or SButton.LeftThumbstickDown or SButton.Down) vertical = 1;
        return horizontal != 0 || vertical != 0;
    }

    private void CloseEditor(bool deleted = false)
    {
        var parent = _editorParent;
        var editedIndex = _editingTabIndex;
        _nameEditFocused = false;
        _confirmingDelete = false;
        Game1.activeClickableMenu = parent;
        _editorParent = null;
        _editingTabIndex = -1;

        if (parent is null)
            return;

        RepositionTabs(parent);
        if (deleted)
        {
            FocusInventoryTab(parent);
            _pendingEditorReturnFocus = -1;
        }
        else if (editedIndex >= 0 && editedIndex < _config.Tabs.Count && _hotkeysVisible)
        {
            FocusHotkey(parent, editedIndex);
            _pendingEditorReturnFocus = editedIndex;
        }
    }

    private void HandleEditorClick(int x, int y)
    {
        if (_confirmingDelete)
        {
            if (_deleteConfirmYesBounds.Contains(x, y))
                ConfirmDelete();
            else if (_deleteConfirmNoBounds.Contains(x, y))
                CancelDeleteConfirmation();
            return;
        }

        if (!_editorNameHitBounds.Contains(x, y))
            _nameEditFocused = false;

        if (_editorIconPreviousBounds.Contains(x, y)) CycleDraftIcon(-1);
        else if (_editorIconNextBounds.Contains(x, y)) CycleDraftIcon(1);
        else if (_editorIconDiceBounds.Contains(x, y))
        {
            EnsureEmojiIcons();
            if (_emojiCount > 0)
                _draftIcon = Random.Shared.Next(_emojiCount);
        }
        else if (_editorKeybindHitBounds.Contains(x, y))
        {
            // Monitor.Log($"Clicked Assign keybind for draft hotkey '{_draftName}'. Current keybind='{_draftKeybind}'.", LogLevel.Info);
            OpenDraftKeybindEditor();
        }
        else if (_editorNameHitBounds.Contains(x, y))
        {
            _nameEditFocused = true;

            if (Game1.activeClickableMenu is not InventoryTabEditorMenu editor)
                return;

            editor.FocusNameInput();

            if (Helper.Input.IsDown(SButton.ControllerA)
                || (Game1.options.gamepadControls && _useControllerTooltipGlyphs))
                OpenNativeNameInput(editor);
        }
        else if (_editorCreateBounds.Contains(x, y))
        {
            if (_editingTabIndex < 0 && _config.Tabs.Count >= MaxCustomTabs)
            {
                Game1.addHUDMessage(new HUDMessage("You can't add another hotkey. All four rows are full.", HUDMessage.error_type));
                // Monitor.Log($"Blocked hotkey creation: maximum capacity ({MaxCustomTabs}) reached.", LogLevel.Info);
                CloseEditor();
                return;
            }

            // Monitor.Log($"Clicked {(_editingTabIndex >= 0 ? "Save edited hotkey" : "Create/OK")}. Pending values: name='{_draftName}', iconId={_draftIcon}, keybind='{_draftKeybind}'.", LogLevel.Info);
            var previousCount = _config.Tabs.Count;
            if (_editingTabIndex >= 0 && _editingTabIndex < _config.Tabs.Count)
                _config.Tabs[_editingTabIndex] = new HubTab { Name = _draftName, Icon = _draftIcon, Keybinds = _draftKeybind };
            else
                _config.Tabs.Add(new HubTab { Name = _draftName, Icon = _draftIcon, Keybinds = _draftKeybind });
            _config.Normalize();
            // Monitor.Log($"Hotkey save: before={previousCount}, after={_config.Tabs.Count}, capacity={MaxCustomTabs}.", LogLevel.Info);
            SaveConfig();
            RegisterGenericModConfigMenu();
            CloseEditor();
        }
        else if (_editorCancelBounds.Contains(x, y))
        {
            // Monitor.Log($"Clicked Cancel for hotkey editor; draft discarded: name='{_draftName}', iconId={_draftIcon}, keybind='{_draftKeybind}'.", LogLevel.Info);
            CloseEditor();
            RegisterGenericModConfigMenu();
        }
        else if (_editingTabIndex >= 0 && _editorDeleteBounds.Contains(x, y))
        {
            if (Game1.activeClickableMenu is not InventoryTabEditorMenu editor)
                return;

            _confirmingDelete = true;
            var confirmation = new DeleteConfirmationMenu(this, editor);
            Game1.activeClickableMenu = confirmation;
            confirmation.FocusCancel();
        }
    }

    private void CancelDeleteConfirmation()
    {
        _confirmingDelete = false;

        if (Game1.activeClickableMenu is not DeleteConfirmationMenu menu)
            return;

        Game1.activeClickableMenu = menu.Editor;
        menu.Editor.FocusDeleteButton();
    }

    private void ConfirmDelete()
    {
        if (_editingTabIndex >= 0 && _editingTabIndex < _config.Tabs.Count)
        {
            _config.Tabs.RemoveAt(_editingTabIndex);
            // var deletedName = _config.Tabs[_editingTabIndex].Name;
            // Monitor.Log($"Deleted hotkey [{_editingTabIndex}] '{deletedName}'.", LogLevel.Info);
            SaveConfig();
            RegisterGenericModConfigMenu();
        }
        CloseEditor(deleted: true);
    }

    private bool IsEditorInteractivePoint(Point point)
    {
        if (_confirmingDelete)
            return _deleteConfirmYesBounds.Contains(point) || _deleteConfirmNoBounds.Contains(point);

        return _editorIconPreviousBounds.Contains(point)
            || _editorIconNextBounds.Contains(point)
            || _editorIconDiceBounds.Contains(point)
            || _editorKeybindHitBounds.Contains(point)
            || _editorNameHitBounds.Contains(point)
            || _editorCreateBounds.Contains(point)
            || _editorCancelBounds.Contains(point)
            || (_editingTabIndex >= 0 && _editorDeleteBounds.Contains(point));
    }

    private void CycleDraftIcon(int delta)
    {
        EnsureEmojiIcons();
        if (_emojiCount == 0)
            return;
        _draftIcon = (_draftIcon + delta + _emojiCount) % _emojiCount;
    }

    private void OpenDraftKeybindEditor()
    {
        _gmcmKeybindOverlayType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("GenericModConfigMenu.Framework.Overlays.KeybindOverlay", throwOnError: false))
            .FirstOrDefault(type => type is not null);

        Type[] constructorTypes = { typeof(Keybind[]), typeof(bool), typeof(string), typeof(Action<Keybind[]>) };
        var constructor = _gmcmKeybindOverlayType?.GetConstructor(constructorTypes);

        if (constructor is null)
        {
            Monitor.Log(
                $"GMCM overlay unavailable. " +
                $"Type found: {_gmcmKeybindOverlayType is not null}. " +
                $"Constructor found: {constructor is not null}.",
                LogLevel.Warn
            );

            _capturingDraftKeybind = true;
            return;
        }

        Action<Keybind[]> save = keybinds =>
        {
            var serialized = string.Join(", ", keybinds.Select(keybind => keybind.ToString()));
            _draftKeybind = KeybindList.Parse(serialized);
            // Monitor.Log($"GMCM keybind editor accepted selection. Raw='{serialized}', parsed='{_draftKeybind}', bound={_draftKeybind.IsBound}.", LogLevel.Info);
        };
        Keybind[] initial = _draftKeybind.Keybinds.ToArray();
        _gmcmKeybindOverlay = constructor.Invoke(new object?[] { initial, false, "Inventory hotkey", save });
        _gmcmFocusedButtonName = initial.Length > 0 ? "Remove 0" : "Add";
        _skipGmcmControllerAOnButtonsChanged = false;
        _capturingDraftKeybind = false;
        // Monitor.Log($"Opened GMCM keybind editor for '{_draftName}'. Initial keybind='{_draftKeybind}'.", LogLevel.Info);
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (e.Pressed.Any(IsControllerButton))
            _useControllerTooltipGlyphs = true;
        else if (e.Pressed.Any())
            _useControllerTooltipGlyphs = false;

        foreach (SButton button in e.Pressed)
        {
            if (!_simulatedButtonsAwaitingChangedEvent.Remove(button))
                continue;

            // var activeMenu = Game1.activeClickableMenu?.GetType().FullName ?? "<none>";
            // Monitor.Log($"Simulated key reached ButtonsChanged: {button}; active menu={activeMenu}; SMAPI state={Helper.Input.GetState(button)}.", LogLevel.Info);
        }

        if (Game1.activeClickableMenu is not InventoryTabEditorMenu || _gmcmKeybindOverlay is null)
            return;

        if (_skipGmcmControllerAOnButtonsChanged)
        {
            _skipGmcmControllerAOnButtonsChanged = false;
            if (e.Pressed.Contains(SButton.ControllerA))
                return;
        }

        if (!IsGmcmCapturingKeybind())
            return;

        // GMCM handles keyboard combinations and controller buttons here.
        InvokeGmcmOverlay("OnButtonsChanged", e);

        foreach (var button in e.Pressed)
            Helper.Input.Suppress(button);
    }

    private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
    {
        Point cursor = new(Game1.getMouseX(), Game1.getMouseY());
        if (_pendingControllerCursorPosition is Point snappedPosition && cursor == snappedPosition)
        {
            _pendingControllerCursorPosition = null;
            return;
        }
        _pendingControllerCursorPosition = null;
        if (_snappingControllerCursor)
            return;
        _useControllerTooltipGlyphs = false;
    }

    private void InvokeGmcmOverlay(string methodName, params object?[] args)
    {
        if (_gmcmKeybindOverlay is null || _gmcmKeybindOverlayType is null)
            return;

        var method = _gmcmKeybindOverlayType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        method?.Invoke(_gmcmKeybindOverlay, args);
    }

    private bool IsGmcmCapturingKeybind() => _gmcmKeybindOverlay is not null
        && _gmcmKeybindOverlayType?.GetField("KeybindEdit", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_gmcmKeybindOverlay) is not null;

    private List<ClickableTextureComponent> GetGmcmButtons()
    {
        var buttons = _gmcmKeybindOverlayType?.GetField("Buttons", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_gmcmKeybindOverlay);
        return buttons is IEnumerable list ? list.OfType<ClickableTextureComponent>().ToList() : new List<ClickableTextureComponent>();
    }

    private void MoveGmcmFocus(int horizontal, int vertical)
    {
        var buttons = GetGmcmButtons();
        var listButtons = buttons.Where(button => button.name is not ("OK" or "Clear")).ToList();
        if (buttons.Count == 0)
            return;

        var listIndex = listButtons.FindIndex(button => button.name == _gmcmFocusedButtonName);
        _gmcmFocusedButtonName = (horizontal, vertical) switch
        {
            ({ } h, _) when h != 0 => _gmcmFocusedButtonName switch
            {
                "OK" when h > 0 => "Clear",
                "Clear" when h < 0 => "OK",
                _ => _gmcmFocusedButtonName
            },

            (_, > 0) => listIndex >= 0 && listIndex + 1 < listButtons.Count
                ? listButtons[listIndex + 1].name
                : "OK",

            (_, < 0) => listIndex > 0
                ? listButtons[listIndex - 1].name
                : listIndex == 0
                    ? listButtons[0].name
                    : listButtons.LastOrDefault()?.name ?? "OK",

            _ => _gmcmFocusedButtonName
        };

    }

    private void ActivateGmcmFocusedButton()
    {
        var button = GetGmcmButtons().FirstOrDefault(candidate => candidate.name == _gmcmFocusedButtonName);
        if (button is null)
            return;

        // Monitor.Log($"Controller activated GMCM keybind control '{button.name}'.", LogLevel.Trace);
        InvokeGmcmOverlay("OnLeftClick", button.bounds.Center.X, button.bounds.Center.Y);
        if (button.name.StartsWith("Remove", StringComparison.Ordinal))
            _gmcmFocusedButtonName = "Add";
    }

    private void DrawGmcmControllerFocus(SpriteBatch b)
    {
        if (IsGmcmCapturingKeybind() || !_useControllerTooltipGlyphs)
            return;

        var button = GetGmcmButtons().FirstOrDefault(candidate => candidate.name == _gmcmFocusedButtonName);
        if (button is null)
            return;

        var bounds = button.bounds;
        const int border = 3;
        var color = Color.Red;
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Y - border, bounds.Width + border * 2, border), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Bottom, bounds.Width + border * 2, border), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X - border, bounds.Y, border, bounds.Height), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.Right, bounds.Y, border, bounds.Height), color);
    }

    private void HandleNameTyping(SButton button)
    {
        switch (button)
        {
            case SButton.Enter or SButton.ControllerA:
                _nameEditFocused = false;
                return;

            case SButton.Back:
                if (_namePrefillSelected)
                {
                    _draftName = "";
                    _namePrefillSelected = false;
                    return;
                }

                if (_draftName.Length > 0)
                    _draftName = _draftName[..^1];
                return;

            case SButton.Space:
                if (_namePrefillSelected)
                {
                    _draftName = "";
                    _namePrefillSelected = false;
                    return;
                }

                if (_draftName.Length < 24 && !_draftName.EndsWith(' '))
                    _draftName += " ";
                return;

            default:
                var key = button.ToString();

                if (key.Length != 1 || !char.IsLetterOrDigit(key[0]))
                    return;

                if (_namePrefillSelected)
                {
                    _draftName = "";
                    _namePrefillSelected = false;
                }

                if (_draftName.Length < 24)
                    _draftName += key;
                break;
        }
    }

    private void DrawEditor(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, _editorBounds.X, _editorBounds.Y, _editorBounds.Width, _editorBounds.Height, Color.White);
        var title = _editingTabIndex >= 0 ? "Edit hotkey" : "Add a hotkey";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        b.DrawString(Game1.dialogueFont, title, new Vector2(_editorBounds.Center.X - titleSize.X / 2, _editorBounds.Y + 28), Color.SaddleBrown);
        const string instructions = "Set a name and icon, then choose a shortcut key.\nClicking the hotkey sends that key to the game.";
        b.DrawString(Game1.smallFont, instructions,
            new Vector2(_editorBounds.X + 32, _editorBounds.Y + 92), Color.SaddleBrown);

        b.DrawString(Game1.dialogueFont, "Name",
            new Vector2(_editorBounds.X + 40, _editorNameBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        IClickableMenu.drawTextureBox(b, _editorNameBounds.X, _editorNameBounds.Y, _editorNameBounds.Width, _editorNameBounds.Height,
            _nameEditFocused ? new Color(255, 245, 195) : Color.White);
        var visibleName = FitText(_draftName, Game1.smallFont, _editorNameBounds.Width - 24);
        Vector2 namePosition = new(_editorNameBounds.X + 18, _editorNameBounds.Center.Y - Game1.smallFont.LineSpacing / 2);
        if (_nameEditFocused && _namePrefillSelected)
        {
            var selectionWidth = (int)Game1.smallFont.MeasureString(visibleName).X;
            b.Draw(Game1.staminaRect,
                new Rectangle((int)namePosition.X - 2, (int)namePosition.Y, selectionWidth + 4, Game1.smallFont.LineSpacing),
                new Color(74, 105, 145, 190));
        }
        b.DrawString(Game1.smallFont, visibleName, namePosition,
            _nameEditFocused && _namePrefillSelected ? Color.White : Color.SaddleBrown);
        if (_nameEditFocused)
        {
            var caretX = (int)(namePosition.X + Game1.smallFont.MeasureString(visibleName).X + 2);
            var caretY = _editorNameBounds.Center.Y - 14;
            b.Draw(Game1.staminaRect, new Rectangle(caretX, caretY, 3, 28), Color.SaddleBrown);
        }
        b.DrawString(Game1.dialogueFont, "Select icon",
            new Vector2(_editorBounds.X + 40, _editorIconBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        DrawEmojiIcon(b, _draftIcon, _editorIconBounds, maxSize: 64);
        DrawCarouselArrow(b, _editorIconPreviousBounds, right: false);
        DrawCarouselArrow(b, _editorIconNextBounds, right: true);
        DrawDiceIcon(b, _editorIconDiceBounds);
        EnsureEmojiIcons();
        var iconCounter = _emojiCount > 0 ? $"{Math.Clamp(_draftIcon, 0, _emojiCount - 1) + 1}/{_emojiCount}" : "0/0";
        var iconCounterSize = Game1.smallFont.MeasureString(iconCounter);
        b.DrawString(Game1.smallFont, iconCounter,
            new Vector2(_editorIconCounterBounds.Right - iconCounterSize.X,
                _editorIconCounterBounds.Center.Y - iconCounterSize.Y / 2f),
            Color.SaddleBrown);
        if (_editorIconDiceBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
            IClickableMenu.drawHoverText(b, "Random icon", Game1.smallFont);

        b.DrawString(Game1.dialogueFont, "Assign keybind",
            new Vector2(_editorBounds.X + 40, _editorKeybindBounds.Center.Y - Game1.dialogueFont.LineSpacing / 2), Color.SaddleBrown);
        // Match ExitPage.draw exactly: same cursor-sheet 9-slice, hover fill,
        // dialogue font, text color, shadow, and vertical text offset.
        var keybindHovered = _editorKeybindBounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            _editorKeybindBounds.X, _editorKeybindBounds.Y, _editorKeybindBounds.Width, _editorKeybindBounds.Height,
            keybindHovered ? Color.Wheat : Color.White, 4f);
        var keybindLabel = _draftKeybind.IsBound ? _draftKeybind.ToString() : "Edit";
        Utility.drawTextWithShadow(b, keybindLabel, Game1.dialogueFont,
            new Vector2(_editorKeybindBounds.Center.X, _editorKeybindBounds.Center.Y + 4)
            - Game1.dialogueFont.MeasureString(keybindLabel) / 2f,
            Game1.textColor, 1f, -1f, -1, -1, 0f);

        DrawConfirmationButton(b, _editorCreateBounds, isOk: true);
        DrawConfirmationButton(b, _editorCancelBounds, isOk: false);
        if (_editingTabIndex >= 0)
            DrawTrashButton(b, _editorDeleteBounds);
        Point cursor = new(Game1.getMouseX(), Game1.getMouseY());
        if (!_confirmingDelete)
        {
            var hoverText = cursor switch
            {
                _ when _editorCreateBounds.Contains(cursor) => "Save",
                _ when _editorCancelBounds.Contains(cursor) => "Cancel",
                _ when _editingTabIndex >= 0 && _editorDeleteBounds.Contains(cursor) => "Delete hotkey",
                _ => null
            };

            if (hoverText is not null)
                IClickableMenu.drawHoverText(b, hoverText, Game1.smallFont);
        }

        if (!_confirmingDelete)
            return;

        IClickableMenu.drawTextureBox(b, _deleteConfirmBounds.X, _deleteConfirmBounds.Y,
            _deleteConfirmBounds.Width, _deleteConfirmBounds.Height, Color.White);

        var prompt = $"Delete '{_draftName}'?";
        var promptSize = Game1.dialogueFont.MeasureString(prompt);

        b.DrawString(Game1.dialogueFont, prompt,
            new Vector2(_deleteConfirmBounds.Center.X - promptSize.X / 2, _deleteConfirmBounds.Y + 36),
            Color.SaddleBrown);

        DrawTextButton(b, _deleteConfirmYesBounds, "Delete");
        DrawTextButton(b, _deleteConfirmNoBounds, "Cancel");
    }

    private static void DrawTextButton(SpriteBatch b, Rectangle bounds, string text)
    {
        var hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height, hovered ? Color.Wheat : Color.White, 4f);
        var textSize = Game1.smallFont.MeasureString(text);
        b.DrawString(Game1.smallFont, text,
            new Vector2(bounds.Center.X - textSize.X / 2, bounds.Center.Y - textSize.Y / 2), Color.SaddleBrown);
    }

    private static void DrawTrashButton(SpriteBatch b, Rectangle bounds)
    {
        // Use the same standard tile scale as the native save/cancel icons,
        // with Stardew's inventory trash-can sprite.
        Rectangle source = new(564, 102, 16, 16);
        var scale = Math.Min(2.5f, Math.Min((bounds.Width - 4) / (float)source.Width, (bounds.Height - 4) / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors,
            new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private static void DrawCarouselArrow(SpriteBatch b, Rectangle bounds, bool right)
    {
        // Reuse the game's own tab-pagination arrows for native carousel styling.
        var source = right ? new Rectangle(365, 495, 12, 11) : new Rectangle(352, 495, 12, 11);
        var scale = Math.Min(3.2f, Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private static void DrawConfirmationButton(SpriteBatch b, Rectangle bounds, bool isOk)
    {
        var tileIndex = isOk ? 46 : 47;
        Rectangle source = Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, tileIndex, -1, -1);
        var scale = Math.Min(2.5f, Math.Min((bounds.Width - 4) / (float)source.Width, (bounds.Height - 4) / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors,
            new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2),
            source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private void DrawEmojiIcon(SpriteBatch b, int emojiIndex, Rectangle bounds, int maxSize)
    {
        EnsureEmojiIcons();
        if (_emojiTexture is null || _emojiCount == 0)
            return;

        var safeIndex = Math.Clamp(emojiIndex, 0, _emojiCount - 1);
        var spriteIndex = _emojiSpriteIndices[safeIndex];
        var columns = _emojiTexture.Width / 9;
        Rectangle source = new(spriteIndex % columns * 9, spriteIndex / columns * 9, 9, 9);
        var scale = Math.Min(maxSize / 9f, Math.Min((bounds.Width - 4) / 9f, (bounds.Height - 4) / 9f));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(_emojiTexture, new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f), source,
            Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private static string FitText(string text, SpriteFont font, float maxWidth)
    {
        if (font.MeasureString(text).X <= maxWidth)
            return text;
        while (text.Length > 1 && font.MeasureString(text + "…").X > maxWidth)
            text = text[..^1];
        return text + "…";
    }

    private static void DrawDiceIcon(SpriteBatch b, Rectangle bounds)
    {
        // Draw only the vanilla randomize-dice sprite; the surrounding rectangle
        // remains an invisible click target for the name randomizer.
        Rectangle source = new(381, 361, 10, 10);
        var scale = Math.Min(5f, Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2), source, Color.White,
            0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private void SimulateKeybind(KeybindList keybinds)
    {
        // Use SMAPI's input simulator so the game and mods see this through the
        // same input state and keybind processing as a physical press.
        var binding = keybinds.Keybinds.FirstOrDefault();
        if (binding is null)
        {
            // Monitor.Log($"Cannot press keybind: no alternatives in '{keybinds}'.", LogLevel.Warn);
            return;
        }

        var buttons = binding.Buttons.Distinct().ToArray();
        if (buttons.Length == 0)
        {
            // Monitor.Log($"Cannot press keybind '{binding}': it contains no buttons.", LogLevel.Warn);
            return;
        }

        foreach (var button in buttons)
        {
            _simulatedButtonsAwaitingDelivery.Add(button);
            _simulatedButtonsAwaitingChangedEvent[button] = 3;
            Helper.Input.Press(button);
        }

        // Monitor.Log($"Pressed _configured keybind through SMAPI input: binding='{binding}', buttons=[{string.Join(", ", buttons)}].", LogLevel.Info);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreach (var button in _simulatedButtonsAwaitingChangedEvent.Keys.ToArray())
        {
            var ticksRemaining = _simulatedButtonsAwaitingChangedEvent[button] - 1;
            if (ticksRemaining <= 0)
            {
                _simulatedButtonsAwaitingChangedEvent.Remove(button);
                // Monitor.Log($"Simulated key did not appear in ButtonsChanged within the expected window: {button}.", LogLevel.Warn);
            }
            else
                _simulatedButtonsAwaitingChangedEvent[button] = ticksRemaining;
        }

        if (_pendingKeybind is PendingKeybind action)
        {
            if (Game1.activeClickableMenu is not null)
            {
                // Another menu opened before the delay completed; don't send
                // the shortcut into an unrelated modal.
                _pendingKeybind = null;
                // Monitor.Log("Cancelled delayed hotkey shortcut because a menu is still open.", LogLevel.Warn);
            }
            else if (action.TicksRemaining > 1)
                _pendingKeybind = action with { TicksRemaining = action.TicksRemaining - 1 };
            else
            {
                _pendingKeybind = null;
                // Monitor.Log($"Dispatching delayed hotkey shortcut after inventory closed: '{action.Keybinds}'.", LogLevel.Info);
                SimulateKeybind(action.Keybinds);
            }
        }

        foreach (var button in _simulatedButtonsAwaitingDelivery.ToArray())
        {
            if (!Helper.Input.IsDown(button))
                continue;

            // Monitor.Log($"SMAPI input state reports simulated key down: {button}.", LogLevel.Info);
            _simulatedButtonsAwaitingDelivery.Remove(button);
        }

        if (_gmcmKeybindOverlay is not null
            && _gmcmKeybindOverlayType?.GetProperty("IsFinished")?.GetValue(_gmcmKeybindOverlay) is true)
        {
            // Monitor.Log($"GMCM keybind sub-modal closed. Draft keybind='{_draftKeybind}', bound={_draftKeybind.IsBound}.", LogLevel.Info);
            _gmcmKeybindOverlay = null;
        }
    }

    private void EnsureEmojiIcons()
    {
        if (_emojiTexture is not null)
            return;

        _emojiTexture = Game1.content.Load<Texture2D>("LooseSprites\\emojis");
        var columns = _emojiTexture.Width / 9;
        var rows = _emojiTexture.Height / 9;
        var availableCells = columns * rows;
        _emojiCount = Math.Min(ChatEmojiCount, availableCells);
        _emojiSpriteIndices.AddRange(Enumerable.Range(0, _emojiCount));
        // Monitor.Log($"Loaded chat emoji IDs 0–{_emojiCount - 1} for hotkeys from LooseSprites/emojis ({_emojiTexture.Width}×{_emojiTexture.Height}, {columns}×{rows} cells). The chat picker exposes {ChatEmojiCount} IDs; unused atlas cells are excluded.", LogLevel.Info);
    }

    private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
    {
        _useControllerTooltipGlyphs = false;
        var cursor = e.Position.GetScaledScreenPixels().ToPoint();
        if (_confirmingDelete || Game1.activeClickableMenu is not InventoryTabEditorMenu || !_editorIconCarouselBounds.Contains(cursor))
            return;

        CycleDraftIcon(Math.Sign(e.Delta));
    }

    private void DrawTabTooltip(SpriteBatch b)
    {
        if (!_hotkeysVisible)
            return;

        if (_controllerAddHotkeyFocused)
        {
            DrawAddHotkeyTooltip(b);
            return;
        }

        var x = Game1.getMouseX();
        var y = Game1.getMouseY();
        if (_plusTabBounds.Contains(x, y))
        {
            DrawAddHotkeyTooltip(b);
            return;
        }
        foreach (var entry in _customTabBounds)
        {
            if (!entry.bounds.Contains(x, y)) continue;
            IClickableMenu.drawHoverText(b, _config.Tabs[entry.index].Name, Game1.smallFont);
            return;
        }
    }

    private void AddHotkeyToSnapGraph(GameMenu menu, IReadOnlyList<Rectangle> nativeTabs)
    {
        var inventoryPage = GetInventoryPage(menu);
        if (nativeTabs.Count == 0 || inventoryPage?.allClickableComponents is null)
            return;

        // GameMenu adds the native tab components to every page's clickable
        // list. Search the inventory page, where the actual snap cursor lives.
        var inventoryTab = GetNativeInventoryTab(menu);
        inventoryTab ??= inventoryPage.allClickableComponents
            .FirstOrDefault(component => component.name.Equals("inventory", StringComparison.OrdinalIgnoreCase));
        inventoryTab ??= FindNativeTabComponent(menu, nativeTabs[0]);
        if (inventoryTab is null)
            return;

        if (_snapGraphMenu != menu || _snapGraphInventoryPage != inventoryPage || _snapGraphInventoryTab != inventoryTab)
        {
            RemoveAddHotkeyFromSnapGraph(_snapGraphMenu);
            _snapGraphMenu = menu;
            _snapGraphInventoryPage = inventoryPage;
            _snapGraphInventoryTab = inventoryTab;
            _addedInventoryTabToClickableSet = !inventoryPage.allClickableComponents.Contains(inventoryTab);
            _originalInventoryTabLeftNeighborId = inventoryTab.leftNeighborID;
        }

        _controllerAddHotkeyComponent ??= new ClickableComponent(_plusTabBounds, "Add hotkey");
        _controllerAddHotkeyComponent.bounds = _plusTabBounds;
        _controllerAddHotkeyComponent.myID = AddHotkeySnapId;
        _controllerAddHotkeyComponent.rightNeighborID = inventoryTab.myID;
        _controllerAddHotkeyComponent.leftNeighborID = inventoryTab.myID;
        _controllerAddHotkeyComponent.upNeighborID = ClickableComponent.SNAP_AUTOMATIC;
        _controllerAddHotkeyComponent.downNeighborID = ClickableComponent.SNAP_AUTOMATIC;
        inventoryTab.leftNeighborID = AddHotkeySnapId;
        if (_addedInventoryTabToClickableSet && !inventoryPage.allClickableComponents.Contains(inventoryTab))
            inventoryPage.allClickableComponents.Add(inventoryTab);
        if (!inventoryPage.allClickableComponents.Contains(_controllerAddHotkeyComponent))
            inventoryPage.allClickableComponents.Add(_controllerAddHotkeyComponent);
        UpdateHotkeySnapComponents(menu);
        // Monitor.Log($"Add-hotkey snap graph active: page={inventoryPage.GetType().Name}, tab='{inventoryTab.name}' id={inventoryTab.myID}, tabInPage={inventoryPage.allClickableComponents.Contains(inventoryTab)}, addInPage={inventoryPage.allClickableComponents.Contains(_controllerAddHotkeyComponent)}, leftNeighbor={inventoryTab.leftNeighborID}, addBounds={_plusTabBounds}.", LogLevel.Info);
    }

    private void UpdateHotkeySnapComponents(GameMenu menu)
    {
        var page = GetInventoryPage(menu);
        if (page?.allClickableComponents is null)
            return;

        if (!_hotkeysVisible)
        {
            foreach (var component in _customHotkeyComponents.Values)
                page.allClickableComponents.Remove(component);
            return;
        }

        foreach (var staleIndex in _customHotkeyComponents.Keys.Where(index => index >= _config.Tabs.Count).ToArray())
        {
            page.allClickableComponents.Remove(_customHotkeyComponents[staleIndex]);
            _customHotkeyComponents.Remove(staleIndex);
        }

        foreach ((Rectangle bounds, int index) in _customTabBounds)
        {
            if (!_customHotkeyComponents.TryGetValue(index, out ClickableComponent? component))
            {
                component = new ClickableComponent(bounds, _config.Tabs[index].Name);
                _customHotkeyComponents[index] = component;
            }

            component.name = _config.Tabs[index].Name;
            component.bounds = bounds;
            component.myID = 988000 + index;
            if (!page.allClickableComponents.Contains(component))
                page.allClickableComponents.Add(component);

            var row = index / 10;
            var column = index % 10;
            component.leftNeighborID = column > 0 ? 988000 + index - 1 : ClickableComponent.SNAP_AUTOMATIC;
            component.rightNeighborID = index + 1 < _config.Tabs.Count && (index + 1) / 10 == row
                ? 988000 + index + 1
                : ClickableComponent.SNAP_AUTOMATIC;
            component.upNeighborID = index + 10 < _config.Tabs.Count
                ? 988000 + index + 10
                : ClickableComponent.SNAP_AUTOMATIC;
            component.downNeighborID = row > 0
                ? 988000 + index - 10
                : FindNearestNativeTabId(menu, bounds.Center.X);
        }
    }

    private void DrawAddHotkeyTooltip(SpriteBatch b)
    {
        const string heading = "Add hotkey";
        var createText = _useControllerTooltipGlyphs ? "Create:" : "Create: Left-click";
        var editText = _useControllerTooltipGlyphs ? "Edit hotkeys:" : "Edit hotkeys: Right-click";
        var toggleText = _hotkeysVisible ? "Hide hotkeys:" : "Show hotkeys:";
        if (!_useControllerTooltipGlyphs)
            toggleText += " Middle-click";
        const int padding = 12;
        const int iconSize = 20;
        const int iconGap = 5;
        const int lineGap = 4;

        var headingSize = Game1.smallFont.MeasureString(heading);
        var createSize = Game1.smallFont.MeasureString(createText);
        var editSize = Game1.smallFont.MeasureString(editText);
        var toggleSize = Game1.smallFont.MeasureString(toggleText);
        var glyphWidth = _useControllerTooltipGlyphs ? iconGap + iconSize : 0;
        var contentWidth = (int)Math.Ceiling(Math.Max(headingSize.X,
            Math.Max(createSize.X + glyphWidth, Math.Max(editSize.X + glyphWidth, toggleSize.X + glyphWidth))));
        var lineHeight = Math.Max(Game1.smallFont.LineSpacing, iconSize);
        var boxWidth = contentWidth + padding * 2;
        var boxHeight = padding * 2 + lineHeight * 4 + lineGap * 3;

        var x = Game1.getMouseX() + 16;
        var y = Game1.getMouseY() + 16;
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        if (x + boxWidth > viewport.Right - 8)
            x = Game1.getMouseX() - boxWidth - 16;
        if (y + boxHeight > viewport.Bottom - 8)
            y = Game1.getMouseY() - boxHeight - 16;
        x = Math.Clamp(x, viewport.Left + 8, viewport.Right - boxWidth - 8);
        y = Math.Clamp(y, viewport.Top + 8, viewport.Bottom - boxHeight - 8);

        IClickableMenu.drawTextureBox(b, x, y, boxWidth, boxHeight, Color.White);
        var textX = x + padding;
        var firstY = y + padding;
        b.DrawString(Game1.smallFont, heading, new Vector2(textX, firstY), Game1.textColor);

        var createY = firstY + lineHeight + lineGap;
        b.DrawString(Game1.smallFont, createText, new Vector2(textX, createY), Game1.textColor);
        if (_useControllerTooltipGlyphs)
            DrawControllerGlyph(b, new Rectangle(textX + (int)Math.Ceiling(createSize.X) + iconGap,
                createY + (lineHeight - iconSize) / 2, iconSize, iconSize), isAButton: true);

        var editY = createY + lineHeight + lineGap;
        b.DrawString(Game1.smallFont, editText, new Vector2(textX, editY), Game1.textColor);
        if (_useControllerTooltipGlyphs)
            DrawControllerGlyph(b, new Rectangle(textX + (int)Math.Ceiling(editSize.X) + iconGap,
                editY + (lineHeight - iconSize) / 2, iconSize, iconSize), isXButton: true);

        var toggleY = editY + lineHeight + lineGap;
        b.DrawString(Game1.smallFont, toggleText, new Vector2(textX, toggleY), Game1.textColor);
        if (_useControllerTooltipGlyphs)
            DrawControllerGlyph(b, new Rectangle(textX + (int)Math.Ceiling(toggleSize.X) + iconGap,
                toggleY + (lineHeight - iconSize) / 2, iconSize, iconSize), isYButton: true);
    }

    private static bool IsControllerButton(SButton button)
    {
        var name = button.ToString();
        return name.StartsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DPad", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Thumbstick", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Trigger", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Shoulder", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawControllerGlyph(SpriteBatch b, Rectangle destination, bool isAButton = false, bool isXButton = false, bool isYButton = false)
    {
        var source = isAButton
            ? new Rectangle(16, 16, 16, 16)
            : isXButton
                ? new Rectangle(32, 16, 16, 16)
                : isYButton
                    ? new Rectangle(64, 16, 16, 16)
                    : new Rectangle(48, 16, 16, 16);
        b.Draw(Game1.mouseCursors, destination, source, Color.White);
    }

    private static int FindNearestNativeTabId(GameMenu menu, int x)
    {
        if (GetNativeTabComponents(menu) is not IList tabs)
            return ClickableComponent.SNAP_AUTOMATIC;
        var nearest = tabs.OfType<ClickableComponent>()
            .Where(tab => tab.visible)
            .OrderBy(tab => Math.Abs(tab.bounds.Center.X - x))
            .FirstOrDefault();
        return nearest?.myID ?? ClickableComponent.SNAP_AUTOMATIC;
    }

    private static object? GetNativeTabComponents(GameMenu menu)
    {
        var tabsField = GameMenuFields.FirstOrDefault(field => field.Name == "tabs");
        return tabsField?.GetValue(menu);
    }

    private static ClickableComponent? FindNativeTabComponent(GameMenu menu, Rectangle targetBounds)
    {
        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;
            var match = values.OfType<ClickableComponent>()
                .FirstOrDefault(component => component.bounds == targetBounds);
            if (match is not null)
                return match;
        }
        return null;
    }

    private void RemoveAddHotkeyFromSnapGraph(GameMenu? menu)
    {
        if (_snapGraphMenu is null)
            return;

        if (_snapGraphInventoryTab is not null)
            _snapGraphInventoryTab.leftNeighborID = _originalInventoryTabLeftNeighborId;
        if (_snapGraphInventoryPage?.allClickableComponents is not null && _controllerAddHotkeyComponent is not null)
            _snapGraphInventoryPage.allClickableComponents.Remove(_controllerAddHotkeyComponent);
        if (_snapGraphInventoryPage?.allClickableComponents is not null)
        {
            foreach (var component in _customHotkeyComponents.Values)
                _snapGraphInventoryPage.allClickableComponents.Remove(component);
        }
        _customHotkeyComponents.Clear();
        if (_addedInventoryTabToClickableSet && _snapGraphInventoryPage?.allClickableComponents is not null && _snapGraphInventoryTab is not null)
            _snapGraphInventoryPage.allClickableComponents.Remove(_snapGraphInventoryTab);

        if (menu is not null && menu != _snapGraphMenu)
            return;

        _snapGraphMenu = null;
        _snapGraphInventoryPage = null;
        _snapGraphInventoryTab = null;
        _addedInventoryTabToClickableSet = false;
        _controllerAddHotkeyFocused = false;

    }

    private void SaveConfig()
    {
        _config.Normalize();
        Helper.WriteConfig(_config);
        // Monitor.Log($"Persisted hotkey _config: count={_config.Tabs.Count}; {string.Join(" | ", _config.Tabs.Select((tab, index) => $"[{index}] '{tab.Name}' iconId={tab.Icon} keybind='{tab.Keybinds}'"))}.", LogLevel.Info);
    }

    private void RegisterGenericModConfigMenu(bool includeDraft = false)
    {
        _gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_gmcmApi is null)
        {
            // Monitor.Log("Generic Mod Config Menu isn't installed; hotkey shortcuts can be added after installing it.", LogLevel.Trace);
            return;
        }

        // Monitor.Log("Connected to Generic Mod Config Menu API; registering hotkey keybind options.", LogLevel.Info);
        _gmcmApi.Unregister(ModManifest);
        _gmcmApi.Register(ModManifest, ResetConfig, SaveConfig);
        _gmcmApi.AddSectionTitle(ModManifest, () => "Hotkey shortcuts",
            () => "Assign shortcuts using Generic Mod Config Menu's standard keybinding editor.");

        for (var i = 0; i < _config.Tabs.Count; i++)
        {
            var index = i;
            _gmcmApi.AddKeybindList(ModManifest,
                () => index < _config.Tabs.Count ? _config.Tabs[index].Keybinds : KeybindList.Parse(""),
                value =>
                {
                    if (index >= _config.Tabs.Count)
                        return;
                    _config.Tabs[index].Keybinds = value;
                    Helper.WriteConfig(_config);
                    // Monitor.Log($"GMCM saved keybind for hotkey [{index}] '{_config.Tabs[index].Name}': '{value}'.", LogLevel.Info);
                },
                () => index < _config.Tabs.Count ? _config.Tabs[index].Name : $"Hotkey {index + 1}",
                () => index < _config.Tabs.Count ? "Send this hotkey's assigned key to the game when clicked." : null);
        }

        if (includeDraft)
        {
            _gmcmApi.AddKeybindList(ModManifest,
                () => _draftKeybind,
                value => _draftKeybind = value,
                () => $"New hotkey: {_draftName}",
                () => "Choose the key combination that this hotkey will send when clicked.");
        }
    }

    private void ResetConfig()
    {
        _config = Helper.ReadConfig<HubConfig>();
        _config.Normalize();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        RemoveAddHotkeyFromSnapGraph(_snapGraphMenu);
        _editorParent = null;
        _pendingKeybind = null;
    }

    public sealed class HubConfig
    {
        public int Columns { get; set; } = 10;
        public int Rows { get; set; } = 4;
        public List<HubTab> Tabs { get; set; } = new()
        {
        };

        public void Normalize()
        {
            Columns = Math.Clamp(Columns, 2, 10);
            Rows = Math.Clamp(Rows, 1, 4);
            Tabs ??= new List<HubTab>();
            // Keep older _configs with smaller Columns/Rows values from silently
            // truncating the new ten-wide rows; four rows support up to 40 tabs.
            Tabs = Tabs.Where(tab => tab is not null).Take(MaxCustomTabs).ToList();
            foreach (HubTab tab in Tabs)
            {
                tab.Name = string.IsNullOrWhiteSpace(tab.Name) ? "New hotkey" : tab.Name.Trim();
                tab.Icon = Math.Clamp(tab.Icon, 0, 100000);
                tab.Keybinds ??= KeybindList.Parse("");
            }
        }
    }

    private sealed record PendingKeybind(KeybindList Keybinds, int TicksRemaining);

    private static List<Rectangle> GetNativeTabBounds(GameMenu menu, int tabY)
    {
        List<Rectangle> boundsFound = new();
        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;
            foreach (var value in values)
            {
                if (value is not ClickableTextureComponent component || !component.visible)
                    continue;
                var bounds = component.bounds;
                if (Math.Abs(bounds.Y - tabY) > 4 || bounds.Width < 48 || bounds.Width > 80)
                    continue;
                if (!boundsFound.Contains(bounds))
                    boundsFound.Add(bounds);
            }
        }
        return boundsFound.OrderBy(bounds => bounds.Left).ToList();
    }

    private static int GetColumnSpacing(int[] anchors, int fallback)
    {
        var gaps = anchors.Distinct().OrderBy(x => x)
            .Zip(anchors.Distinct().OrderBy(x => x).Skip(1), (left, right) => right - left)
            .Where(gap => gap is > 0 and <= 96).ToArray();
        return gaps.Length == 0
            ? fallback
            : gaps.GroupBy(gap => gap).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First().Key;
    }

    private static int[] GetInventoryColumnAnchors(GameMenu menu, int tabY)
    {
        var currentTab = CurrentTabField?.GetValue(menu) as int? ?? 0;
        object? inventoryPage = null;
        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;

            var pages = values.OfType<object>().Where(value => value is IClickableMenu).ToList();
            if (pages.Count <= currentTab)
                continue;

            inventoryPage = pages[currentTab];

            if (inventoryPage.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
                break;
        }

        if (inventoryPage is null)
            return Array.Empty<int>();

        List<Rectangle> components = new();
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        CollectPageComponentBounds(inventoryPage, components, visited, depth: 0);

        return components
            .Where(bounds => bounds.Y > tabY + 64 && bounds.Width >= 48 && bounds.Width <= 80)
            .GroupBy(bounds => bounds.Y)
            .Select(row => row.Select(bounds => bounds.Left).Distinct().OrderBy(x => x).ToArray())
            .Where(columns => columns.Length >= 10)
            .OrderByDescending(columns => columns.Length)
            .FirstOrDefault() ?? Array.Empty<int>();
    }

    private static void CollectPageComponentBounds(object? value, List<Rectangle> bounds, HashSet<object> visited, int depth)
    {
        if (value is null || depth > 5 || value is string || value.GetType().IsPrimitive)
            return;

        if (!value.GetType().IsValueType && !visited.Add(value))
            return;

        if (value is ClickableComponent component)
        {
            if (component.visible)
                bounds.Add(component.bounds);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var child in enumerable)
                CollectPageComponentBounds(child, bounds, visited, depth + 1);
            return;
        }

        var type = value.GetType();
        if (type.Namespace?.StartsWith("StardewValley.Menus", StringComparison.Ordinal) != true)
            return;

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsStatic || field.FieldType.IsPrimitive || field.FieldType == typeof(string))
                continue;
            try
            {
                CollectPageComponentBounds(field.GetValue(value), bounds, visited, depth + 1);
            }
            catch (Exception)
            {
                // Ignore inaccessible or transient menu fields.
            }
        }
    }

    private static void RedrawNativeTabs(SpriteBatch b, GameMenu menu)
    {
        var tabY = menu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY + 64;
        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;
            foreach (var value in values)
            {
                if (value is ClickableTextureComponent component
                    && component.visible
                    && component.bounds.Y == tabY
                    && component.bounds.Width >= 48
                    && component.bounds.Width <= 80)
                    component.draw(b);
            }
        }
    }

    public sealed class HubTab
    {
        public string Name { get; set; } = "New hotkey";
        public int Icon { get; set; }
        public KeybindList Keybinds { get; set; } = KeybindList.Parse("");
    }

    private sealed class HotkeyNamingMenu : NamingMenu
    {
        private readonly InventoryTabEditorMenu _editor;
        private readonly ClickableComponent[] _controls;
        private readonly ClickableComponent _cancelButton;
        private int _focusedControl;

        public HotkeyNamingMenu(InventoryTabEditorMenu editor, doneNamingBehavior onDone, string initialName)
            : base(onDone, "Hotkey name", initialName)
        {
            _editor = editor;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(NamingMenu);
            var nameInput = (ClickableComponent)type.GetField("textBoxCC", flags)!.GetValue(this)!;
            var random = (ClickableTextureComponent)type.GetField("randomButton", flags)!.GetValue(this)!;
            var done = (ClickableTextureComponent)type.GetField("doneNamingButton", flags)!.GetValue(this)!;

            // Keep the native text box and controls, with Cancel before OK.
            var size = done.bounds.Width;
            var startX = Math.Min(random.bounds.X, done.bounds.X);
            var rightEdge = Math.Min(Game1.uiViewport.Width - 16, xPositionOnScreen + width - 16);
            var overflow = Math.Max(0, startX + size * 3 + 24 - rightEdge);
            random.bounds.X = startX - overflow;
            done.bounds.Y = random.bounds.Y;
            _cancelButton = new ClickableComponent(new Rectangle(random.bounds.Right + 12, done.bounds.Y, size, done.bounds.Height), "Cancel")
            {
                myID = 987665
            };
            done.bounds.X = _cancelButton.bounds.Right + 12;
            _controls = new ClickableComponent[] { nameInput, random, _cancelButton, done };
            allClickableComponents?.Add(_cancelButton);
            Focus(0);
        }

        public void Cancel()
        {
            Game1.activeClickableMenu = _editor;
            _editor.FocusNameInput();
        }

        private void Focus(int index)
        {
            _focusedControl = Math.Clamp(index, 0, _controls.Length - 1);
            currentlySnappedComponent = _controls[_focusedControl];
            if (Game1.options.SnappyMenus)
                snapCursorToCurrentSnappedComponent();
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (button == Buttons.B)
                Cancel();
            else if (button is Buttons.DPadLeft or Buttons.LeftThumbstickLeft)
                Focus(_focusedControl - 1);
            else if (button is Buttons.DPadRight or Buttons.LeftThumbstickRight)
                Focus(_focusedControl + 1);
            else if (button == Buttons.A)
            {
                if (_focusedControl == 2)
                    Cancel();
                else if (_focusedControl == 0)
                    base.receiveGamePadButton(button);
                else
                    base.receiveLeftClick(_controls[_focusedControl].bounds.Center.X, _controls[_focusedControl].bounds.Center.Y);
            }
            else
                base.receiveGamePadButton(button);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
                Cancel();
            else
                base.receiveKeyPress(key);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_cancelButton.bounds.Contains(x, y))
                Cancel();
            else
                base.receiveLeftClick(x, y, playSound);
        }

        public override void draw(SpriteBatch b)
        {
            base.draw(b);
            b.Draw(Game1.mouseCursors, _cancelButton.bounds,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47), Color.White);
            if (_cancelButton.bounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                IClickableMenu.drawHoverText(b, "Cancel", Game1.smallFont);
        }
    }

    private sealed class InventoryTabEditorMenu : IClickableMenu
    {
        private readonly ModEntry _mod;
        private readonly GameMenu _parent;
        private readonly Dictionary<int, ClickableComponent> _focusComponents = new();

        public InventoryTabEditorMenu(ModEntry mod, GameMenu parent)
            : base(parent.xPositionOnScreen, parent.yPositionOnScreen, parent.width, parent.height)
        {
            _mod = mod;
            _parent = parent;
            allClickableComponents ??= new List<ClickableComponent>();
            AddFocusComponent(EditorNameSnapId, "Hotkey name", mod._editorNameHitBounds,
                left: ClickableComponent.SNAP_AUTOMATIC, right: ClickableComponent.SNAP_AUTOMATIC,
                up: ClickableComponent.SNAP_AUTOMATIC, down: EditorIconPreviousSnapId);
            AddFocusComponent(EditorIconPreviousSnapId, "Previous icon", mod._editorIconPreviousBounds,
                left: ClickableComponent.SNAP_AUTOMATIC, right: EditorIconNextSnapId,
                up: EditorNameSnapId, down: EditorKeybindSnapId);
            AddFocusComponent(EditorIconNextSnapId, "Next icon", mod._editorIconNextBounds,
                left: EditorIconPreviousSnapId, right: EditorIconDiceSnapId,
                up: EditorNameSnapId, down: EditorKeybindSnapId);
            AddFocusComponent(EditorIconDiceSnapId, "Random icon", mod._editorIconDiceBounds,
                left: EditorIconNextSnapId, right: ClickableComponent.SNAP_AUTOMATIC,
                up: EditorNameSnapId, down: EditorKeybindSnapId);
            AddFocusComponent(EditorKeybindSnapId, "Assign keybind", mod._editorKeybindHitBounds,
                left: ClickableComponent.SNAP_AUTOMATIC, right: EditorCreateSnapId,
                up: EditorIconNextSnapId, down: EditorCancelSnapId);
            AddFocusComponent(EditorCancelSnapId, "Cancel", mod._editorCancelBounds,
                left: EditingDeleteId(mod), right: EditorCreateSnapId,
                up: EditorKeybindSnapId, down: ClickableComponent.SNAP_AUTOMATIC);
            AddFocusComponent(EditorCreateSnapId, "Save", mod._editorCreateBounds,
                left: EditorCancelSnapId, right: ClickableComponent.SNAP_AUTOMATIC,
                up: EditorKeybindSnapId, down: ClickableComponent.SNAP_AUTOMATIC);
            if (mod._editingTabIndex >= 0)
                AddFocusComponent(EditorDeleteSnapId, "Delete", mod._editorDeleteBounds,
                    left: ClickableComponent.SNAP_AUTOMATIC, right: EditorCancelSnapId,
                    up: EditorKeybindSnapId, down: ClickableComponent.SNAP_AUTOMATIC);
            snapToDefaultClickableComponent();
            if (!Game1.options.SnappyMenus)
                return;

            try
            {
                snapCursorToCurrentSnappedComponent();
            }
            catch (Exception ex)
            {
                // _mod.// Monitor.Log($"Couldn't move cursor to hotkey name field on editor open: {ex.Message}", LogLevel.Trace);
            }
        }

        public override void snapToDefaultClickableComponent()
        {
            FocusNameInput();
        }

        public ClickableComponent FocusedComponent => currentlySnappedComponent
            ?? _focusComponents[EditorNameSnapId];

        public void FocusNameInput() => Focus(EditorNameSnapId);

        public void ActivateFocused()
        {
            var focused = FocusedComponent;
            if (focused.myID == EditorNameSnapId || _mod._editorNameBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                _mod.OpenNativeNameInput(this);
            else
                _mod.HandleEditorClick(focused.bounds.Center.X, focused.bounds.Center.Y);
        }

        public void FocusDeleteButton() => Focus(EditorDeleteSnapId);

        private void AddFocusComponent(int id, string name, Rectangle bounds, int left, int right, int up, int down)
        {
            var component = new ClickableComponent(bounds, name)
            {
                myID = id,
                leftNeighborID = left,
                rightNeighborID = right,
                upNeighborID = up,
                downNeighborID = down
            };
            _focusComponents[id] = component;
            allClickableComponents.Add(component);
        }

        public void MoveFocus(int horizontal, int vertical)
        {
            var currentId = FocusedComponent.myID;
            if (_mod._confirmingDelete)
            {
                if (horizontal < 0 && currentId == DeleteConfirmNoSnapId)
                    Focus(DeleteConfirmYesSnapId);
                else if (horizontal > 0 && currentId == DeleteConfirmYesSnapId)
                    Focus(DeleteConfirmNoSnapId);
                return;
            }

            var nextId = currentId;
            if (currentId == EditorNameSnapId && vertical > 0)
                nextId = EditorIconPreviousSnapId;
            else if (currentId == EditorIconPreviousSnapId)
            {
                if (horizontal > 0) nextId = EditorIconNextSnapId;
                else if (vertical < 0) nextId = EditorNameSnapId;
                else if (vertical > 0) nextId = EditorKeybindSnapId;
            }
            else if (currentId == EditorIconNextSnapId)
            {
                if (horizontal < 0) nextId = EditorIconPreviousSnapId;
                else if (horizontal > 0) nextId = EditorIconDiceSnapId;
                else if (vertical < 0) nextId = EditorNameSnapId;
                else if (vertical > 0) nextId = EditorKeybindSnapId;
            }
            else if (currentId == EditorIconDiceSnapId)
            {
                if (horizontal < 0) nextId = EditorIconNextSnapId;
                else if (vertical < 0) nextId = EditorNameSnapId;
                else if (vertical > 0) nextId = EditorKeybindSnapId;
            }
            else if (currentId == EditorKeybindSnapId)
            {
                if (vertical < 0) nextId = EditorIconNextSnapId;
                else if (vertical > 0) nextId = EditorCancelSnapId;
                else if (horizontal < 0) nextId = EditorCancelSnapId;
                else if (horizontal > 0) nextId = _focusComponents.ContainsKey(EditorDeleteSnapId) ? EditorDeleteSnapId : EditorCreateSnapId;
            }
            else if (currentId == EditorCancelSnapId)
            {
                if (horizontal < 0 && _focusComponents.ContainsKey(EditorDeleteSnapId)) nextId = EditorDeleteSnapId;
                else if (horizontal > 0) nextId = EditorCreateSnapId;
                else if (vertical < 0) nextId = EditorKeybindSnapId;
            }
            else if (currentId == EditorCreateSnapId)
            {
                if (horizontal < 0) nextId = EditorCancelSnapId;
                else if (vertical < 0) nextId = EditorKeybindSnapId;
            }
            else if (currentId == EditorDeleteSnapId)
            {
                if (horizontal > 0) nextId = EditorCancelSnapId;
                else if (vertical < 0) nextId = EditorKeybindSnapId;
            }

            if (nextId != currentId)
                Focus(nextId);
        }

        private void Focus(int id)
        {
            if (!_focusComponents.TryGetValue(id, out ClickableComponent? component))
                return;
            currentlySnappedComponent = component;
            _mod._nameEditFocused = id == EditorNameSnapId;
            try
            {
                snapCursorToCurrentSnappedComponent();
            }
            catch (Exception ex)
            {
                // _mod.// Monitor.Log($"Couldn't snap controller cursor to editor control '{component.name}': {ex.Message}", LogLevel.Trace);
            }
        }

        private static int EditingDeleteId(ModEntry mod) => mod._editingTabIndex >= 0 ? EditorDeleteSnapId : ClickableComponent.SNAP_AUTOMATIC;

        public override void draw(SpriteBatch b)
        {
            _mod.RepositionTabs(_parent);
            _parent.draw(b);
            if (IsInventoryPage(_parent))
            {
                if (_mod._hotkeysVisible)
                    _mod.DrawCustomTabs(b);
                RedrawNativeTabs(b, _parent);
                _mod.DrawPlusTab(b);
            }
            _mod.DrawEditor(b);
            if (_mod._capturingDraftKeybind)
                _mod.DrawKeybindCapture(b);
            if (_mod._gmcmKeybindOverlay is not null)
            {
                _mod.InvokeGmcmOverlay("Draw", b);
                _mod.DrawGmcmControllerFocus(b);
            }
            // The editor replaces GameMenu as the active menu. Draw the
            // software cursor last so it stays visible above the modal.
            if (!Game1.options.hardwareCursor)
                drawMouse(b, ignore_transparency: true);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            _mod.HandleEditorClick(x, y);
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (button == Buttons.A && _mod._gmcmKeybindOverlay is null && !_mod._capturingDraftKeybind)
            {
                ActivateFocused();
                return;
            }
            if (button is Buttons.DPadLeft or Buttons.LeftThumbstickLeft)
            {
                MoveFocus(-1, 0);
                return;
            }
            if (button is Buttons.DPadRight or Buttons.LeftThumbstickRight)
            {
                MoveFocus(1, 0);
                return;
            }
            if (button is Buttons.DPadUp or Buttons.LeftThumbstickUp)
            {
                MoveFocus(0, -1);
                return;
            }
            if (button is Buttons.DPadDown or Buttons.LeftThumbstickDown)
            {
                MoveFocus(0, 1);
                return;
            }

            base.receiveGamePadButton(button);
        }
    }

    private sealed class DeleteConfirmationMenu : IClickableMenu
    {
        private readonly ModEntry _mod;
        private readonly ClickableComponent _deleteButton;
        private readonly ClickableComponent _cancelButton;

        public InventoryTabEditorMenu Editor { get; }

        public DeleteConfirmationMenu(ModEntry mod, InventoryTabEditorMenu editor)
            : base(mod._deleteConfirmBounds.X, mod._deleteConfirmBounds.Y,
                mod._deleteConfirmBounds.Width, mod._deleteConfirmBounds.Height)
        {
            _mod = mod;
            Editor = editor;
            _deleteButton = new ClickableComponent(mod._deleteConfirmYesBounds, "Delete")
            {
                myID = DeleteConfirmYesSnapId,
                rightNeighborID = DeleteConfirmNoSnapId
            };
            _cancelButton = new ClickableComponent(mod._deleteConfirmNoBounds, "Cancel")
            {
                myID = DeleteConfirmNoSnapId,
                leftNeighborID = DeleteConfirmYesSnapId
            };
            allClickableComponents = new List<ClickableComponent> { _deleteButton, _cancelButton };
            snapToDefaultClickableComponent();
        }

        public override void snapToDefaultClickableComponent() => FocusCancel();

        public void FocusCancel() => Focus(_cancelButton);

        public void MoveFocus(int horizontal, int vertical)
        {
            if (horizontal < 0 || vertical > 0)
                Focus(_deleteButton);
            else if (horizontal > 0 || vertical < 0)
                Focus(_cancelButton);
        }

        private void Focus(ClickableComponent component)
        {
            currentlySnappedComponent = component;
            try
            {
                snapCursorToCurrentSnappedComponent();
            }
            catch (Exception ex)
            {
                // _mod.// Monitor.Log($"Couldn't snap cursor to delete confirmation '{component.name}': {ex.Message}", LogLevel.Trace);
            }
        }

        public void ActivateFocused()
        {
            if (currentlySnappedComponent == _deleteButton)
                _mod.ConfirmDelete();
            else
                _mod.CancelDeleteConfirmation();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_deleteButton.bounds.Contains(x, y))
                _mod.ConfirmDelete();
            else if (_cancelButton.bounds.Contains(x, y))
                _mod.CancelDeleteConfirmation();
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (button == Buttons.A)
                ActivateFocused();
            else if (button == Buttons.B)
                _mod.CancelDeleteConfirmation();
            else if (button is Buttons.DPadLeft or Buttons.LeftThumbstickLeft)
                MoveFocus(-1, 0);
            else if (button is Buttons.DPadRight or Buttons.LeftThumbstickRight)
                MoveFocus(1, 0);
            else if (button is Buttons.DPadUp or Buttons.LeftThumbstickUp)
                MoveFocus(0, -1);
            else if (button is Buttons.DPadDown or Buttons.LeftThumbstickDown)
                MoveFocus(0, 1);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key is Keys.Enter or Keys.Space)
                ActivateFocused();
            else if (key == Keys.Escape)
                _mod.CancelDeleteConfirmation();
            else if (key == Keys.Up)
                MoveFocus(0, -1);
            else if (key == Keys.Down)
                MoveFocus(0, 1);
            else if (key == Keys.Left)
                MoveFocus(-1, 0);
            else if (key == Keys.Right)
                MoveFocus(1, 0);
        }

        public override void draw(SpriteBatch b)
        {
            Editor.draw(b);
            if (!Game1.options.hardwareCursor)
                drawMouse(b, ignore_transparency: true);
        }
    }

    private void DrawKeybindCapture(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, _keybindCaptureBounds.X, _keybindCaptureBounds.Y,
            _keybindCaptureBounds.Width, _keybindCaptureBounds.Height, Color.White);
        const string prompt = "Press a key for this hotkey (Esc to cancel)";
        var promptSize = Game1.dialogueFont.MeasureString(prompt);
        b.DrawString(Game1.dialogueFont, prompt,
            new Vector2(_keybindCaptureBounds.Center.X - promptSize.X / 2, _keybindCaptureBounds.Y + 44), Color.SaddleBrown);
        var current = _draftKeybind.IsBound ? _draftKeybind.ToString() : "No key assigned";
        var currentSize = Game1.smallFont.MeasureString(current);
        b.DrawString(Game1.smallFont, current,
            new Vector2(_keybindCaptureBounds.Center.X - currentSize.X / 2, _keybindCaptureBounds.Y + 112), Color.SaddleBrown);
    }

}

// Must be a top-level public interface: SMAPI checks Type.IsPublic when it
// creates mod API proxies, and rejects even a public nested interface.
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void Unregister(IManifest mod);
    void OpenModMenuAsChildMenu(IManifest mod);
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string?>? tooltip = null);
    void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue,
        Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
}

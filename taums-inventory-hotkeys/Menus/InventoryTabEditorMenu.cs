using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Taums_menu_supreme;

public sealed partial class ModEntry
{
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
            catch (Exception)
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

        public void UpdateFocusBounds()
        {
            _focusComponents[EditorNameSnapId].bounds = _mod._editorNameHitBounds;
            _focusComponents[EditorIconPreviousSnapId].bounds = _mod._editorIconPreviousBounds;
            _focusComponents[EditorIconNextSnapId].bounds = _mod._editorIconNextBounds;
            _focusComponents[EditorIconDiceSnapId].bounds = _mod._editorIconDiceBounds;
            _focusComponents[EditorKeybindSnapId].bounds = _mod._editorKeybindHitBounds;
            _focusComponents[EditorCancelSnapId].bounds = _mod._editorCancelBounds;
            _focusComponents[EditorCreateSnapId].bounds = _mod._editorCreateBounds;
            if (_focusComponents.TryGetValue(EditorDeleteSnapId, out var delete))
                delete.bounds = _mod._editorDeleteBounds;
        }

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
                int confirmationId = (currentId, horizontal) switch
                {
                    (DeleteConfirmNoSnapId, < 0) => DeleteConfirmYesSnapId,
                    (DeleteConfirmYesSnapId, > 0) => DeleteConfirmNoSnapId,
                    _ => currentId
                };
                if (confirmationId != currentId)
                    Focus(confirmationId);
                return;
            }

            int nextId = (currentId, horizontal, vertical) switch
            {
                (EditorNameSnapId, _, > 0) => EditorIconPreviousSnapId,
                (EditorIconPreviousSnapId, > 0, _) => EditorIconNextSnapId,
                (EditorIconPreviousSnapId, _, < 0) => EditorNameSnapId,
                (EditorIconPreviousSnapId, _, > 0) => EditorKeybindSnapId,
                (EditorIconNextSnapId, < 0, _) => EditorIconPreviousSnapId,
                (EditorIconNextSnapId, > 0, _) => EditorIconDiceSnapId,
                (EditorIconNextSnapId, _, < 0) => EditorNameSnapId,
                (EditorIconNextSnapId, _, > 0) => EditorKeybindSnapId,
                (EditorIconDiceSnapId, < 0, _) => EditorIconNextSnapId,
                (EditorIconDiceSnapId, _, < 0) => EditorNameSnapId,
                (EditorIconDiceSnapId, _, > 0) => EditorKeybindSnapId,
                (EditorKeybindSnapId, _, < 0) => EditorIconNextSnapId,
                (EditorKeybindSnapId, _, > 0) => EditorCancelSnapId,
                (EditorKeybindSnapId, < 0, _) => EditorCancelSnapId,
                (EditorKeybindSnapId, > 0, _) => _focusComponents.ContainsKey(EditorDeleteSnapId) ? EditorDeleteSnapId : EditorCreateSnapId,
                (EditorCancelSnapId, < 0, _) when _focusComponents.ContainsKey(EditorDeleteSnapId) => EditorDeleteSnapId,
                (EditorCancelSnapId, > 0, _) => EditorCreateSnapId,
                (EditorCancelSnapId, _, < 0) => EditorKeybindSnapId,
                (EditorCreateSnapId, < 0, _) => EditorCancelSnapId,
                (EditorCreateSnapId, _, < 0) => EditorKeybindSnapId,
                (EditorDeleteSnapId, > 0, _) => EditorCancelSnapId,
                (EditorDeleteSnapId, _, < 0) => EditorKeybindSnapId,
                _ => currentId
            };

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
            catch (Exception)
            {
                // _mod.// Monitor.Log($"Couldn't snap controller cursor to editor control '{component.name}': {ex.Message}", LogLevel.Trace);
            }
        }

        private static int EditingDeleteId(ModEntry mod) => mod._editingTabIndex >= 0 ? EditorDeleteSnapId : ClickableComponent.SNAP_AUTOMATIC;

        public override void draw(SpriteBatch b)
        {
            _mod.EnsureLayout(_parent);
            UpdateFocusBounds();
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
            switch (button)
            {
                case Buttons.A when _mod._gmcmKeybindOverlay is null && !_mod._capturingDraftKeybind:
                    ActivateFocused();
                    break;
                case Buttons.DPadLeft or Buttons.LeftThumbstickLeft:
                    MoveFocus(-1, 0);
                    break;
                case Buttons.DPadRight or Buttons.LeftThumbstickRight:
                    MoveFocus(1, 0);
                    break;
                case Buttons.DPadUp or Buttons.LeftThumbstickUp:
                    MoveFocus(0, -1);
                    break;
                case Buttons.DPadDown or Buttons.LeftThumbstickDown:
                    MoveFocus(0, 1);
                    break;
                default:
                    base.receiveGamePadButton(button);
                    break;
            }
        }
    }

}

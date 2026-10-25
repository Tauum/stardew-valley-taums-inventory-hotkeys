using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
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
            ClickableComponent? target = (horizontal, vertical) switch
            {
                (< 0, _) or (_, > 0) => _deleteButton,
                (> 0, _) or (_, < 0) => _cancelButton,
                _ => null
            };
            if (target is not null)
                Focus(target);
        }

        private void Focus(ClickableComponent component)
        {
            currentlySnappedComponent = component;
            try
            {
                snapCursorToCurrentSnappedComponent();
            }
            catch (Exception)
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
            switch (button)
            {
                case Buttons.A:
                    ActivateFocused();
                    break;
                case Buttons.B:
                    _mod.CancelDeleteConfirmation();
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
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            switch (key)
            {
                case Keys.Enter or Keys.Space:
                    ActivateFocused();
                    break;
                case Keys.Escape:
                    _mod.CancelDeleteConfirmation();
                    break;
                case Keys.Up:
                    MoveFocus(0, -1);
                    break;
                case Keys.Down:
                    MoveFocus(0, 1);
                    break;
                case Keys.Left:
                    MoveFocus(-1, 0);
                    break;
                case Keys.Right:
                    MoveFocus(1, 0);
                    break;
            }
        }

        public override void draw(SpriteBatch b)
        {
            Editor.draw(b);
            if (!Game1.options.hardwareCursor)
                drawMouse(b, ignore_transparency: true);
        }
    }
}

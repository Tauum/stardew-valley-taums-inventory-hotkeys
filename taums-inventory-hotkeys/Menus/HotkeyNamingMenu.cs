using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private sealed class HotkeyNamingMenu : NamingMenu
    {
        public HotkeyNamingMenu(doneNamingBehavior onDone, string title, string initialName)
            : base(onDone, title, initialName)
        {
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (button == Buttons.A
                && (currentlySnappedComponent == textBoxCC
                    || textBoxCC.bounds.Contains(Game1.getMouseX(), Game1.getMouseY())))
            {
                receiveLeftClick(textBoxCC.bounds.Center.X, textBoxCC.bounds.Center.Y);
                return;
            }

            base.receiveGamePadButton(button);
        }
    }

}

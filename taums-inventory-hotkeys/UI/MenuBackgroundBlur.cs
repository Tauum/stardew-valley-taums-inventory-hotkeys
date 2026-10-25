using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private Texture2D? _blurredMenuBackground;
    private int _blurredBackgroundWidth;
    private int _blurredBackgroundHeight;

    private void OnMenuChangedForBlur(object? sender, MenuChangedEventArgs e)
    {
        if (e.NewMenu is null || !_config.BlurEnabled || _config.BlurOpacity <= 0)
        {
            _blurredMenuBackground?.Dispose();
            _blurredMenuBackground = null;
            return;
        }

        // The last completed frame still contains the game world here. By the
        // time RenderingActiveMenu runs, the next frame may have cleared the
        // backbuffer or replaced it with the menu's solid background.
        if ((e.OldMenu is null && e.NewMenu is GameMenu or InventoryTabEditorMenu or HotkeyNamingMenu)
            || (e.NewMenu is InventoryTabEditorMenu && e.OldMenu is not null))
        {
            var device = Game1.graphics.GraphicsDevice;
            var backBuffer = device.PresentationParameters;
            _blurredMenuBackground?.Dispose();
            _blurredMenuBackground = null;
            _blurredBackgroundWidth = backBuffer.BackBufferWidth;
            _blurredBackgroundHeight = backBuffer.BackBufferHeight;
            try
            {
                _blurredMenuBackground = CaptureBlurredBackground(device, _blurredBackgroundWidth, _blurredBackgroundHeight);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                Monitor.Log($"Could not capture the menu background for blur: {ex.Message}", LogLevel.Warn);
            }
        }
    }

    private void OnRenderingActiveMenu(object? sender, RenderingActiveMenuEventArgs e)
    {
        if (!_config.BlurEnabled || _config.BlurOpacity <= 0)
            return;

        var menu = Game1.activeClickableMenu;
        if (menu is not InventoryTabEditorMenu and not DeleteConfirmationMenu and not HotkeyNamingMenu
            && (menu is not GameMenu gameMenu || !IsInventoryPage(gameMenu)))
            return;

        var device = Game1.graphics.GraphicsDevice;
        var viewport = device.Viewport;
        var backBuffer = device.PresentationParameters;
        int width = backBuffer.BackBufferWidth;
        int height = backBuffer.BackBufferHeight;
        if (_blurredBackgroundWidth != width || _blurredBackgroundHeight != height)
            return;

        if (_blurredMenuBackground is not null)
            e.SpriteBatch.Draw(_blurredMenuBackground, new Rectangle(0, 0, viewport.Width, viewport.Height),
                new Color(205, 205, 205) * (_config.BlurOpacity / 100f));
    }

    private static Texture2D? CaptureBlurredBackground(GraphicsDevice device, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return null;

        // Capture the scene once per menu opening. Working at quarter resolution
        // keeps the blur inexpensive while avoiding large blocks when Stardew
        // draws the texture with its usual point sampling.
        const int scale = 4;
        var screen = new Color[width * height];
        device.GetBackBufferData(screen);
        int smallWidth = Math.Max(1, width / scale);
        int smallHeight = Math.Max(1, height / scale);
        var pixels = new Color[smallWidth * smallHeight];
        for (int y = 0; y < smallHeight; y++)
        for (int x = 0; x < smallWidth; x++)
        {
            int sx = Math.Min(width - 1, x * scale + scale / 2);
            int sy = Math.Min(height - 1, y * scale + scale / 2);
            pixels[y * smallWidth + x] = screen[sy * width + sx];
        }

        var horizontal = new Color[pixels.Length];
        for (int y = 0; y < smallHeight; y++)
        for (int x = 0; x < smallWidth; x++)
            horizontal[y * smallWidth + x] = Average(pixels, smallWidth, smallHeight, x, y, true);
        for (int y = 0; y < smallHeight; y++)
        for (int x = 0; x < smallWidth; x++)
            pixels[y * smallWidth + x] = Average(horizontal, smallWidth, smallHeight, x, y, false);

        var texture = new Texture2D(device, smallWidth, smallHeight);
        texture.SetData(pixels);
        return texture;
    }

    private static Color Average(Color[] pixels, int width, int height, int x, int y, bool horizontal)
    {
        int red = 0, green = 0, blue = 0;
        const int radius = 7;
        for (int offset = -radius; offset <= radius; offset++)
        {
            int sampleX = horizontal ? Math.Clamp(x + offset, 0, width - 1) : x;
            int sampleY = horizontal ? y : Math.Clamp(y + offset, 0, height - 1);
            var color = pixels[sampleY * width + sampleX];
            red += color.R;
            green += color.G;
            blue += color.B;
        }

        const int samples = radius * 2 + 1;
        return new Color(red / samples, green / samples, blue / samples);
    }
}

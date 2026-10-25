using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Taums_menu_supreme;

internal sealed class EmojiCatalog
{
    // The chat picker exposes IDs 0 through 195; the atlas contains unused cells.
    private const int ChatEmojiCount = 196;
    private Texture2D? _texture;

    public int Count { get; private set; }

    public void EnsureLoaded()
    {
        if (_texture is not null)
            return;

        _texture = Game1.content.Load<Texture2D>("LooseSprites\\emojis");
        Count = Math.Min(ChatEmojiCount, _texture.Width / 9 * (_texture.Height / 9));
    }

    public void DrawIcon(SpriteBatch b, int emojiIndex, Rectangle bounds, int maxSize)
    {
        EnsureLoaded();
        if (_texture is null || Count == 0)
            return;

        int spriteIndex = Math.Clamp(emojiIndex, 0, Count - 1);
        int columns = _texture.Width / 9;
        Rectangle source = new(spriteIndex % columns * 9, spriteIndex / columns * 9, 9, 9);
        float scale = Math.Min(maxSize / 9f, Math.Min((bounds.Width - 4) / 9f, (bounds.Height - 4) / 9f));
        Vector2 size = new(source.Width * scale, source.Height * scale);
        b.Draw(_texture, new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f), source,
            Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }
}

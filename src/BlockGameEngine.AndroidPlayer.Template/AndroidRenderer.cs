using Android.Content.Res;
using Android.Graphics;
using BlockGameEngine.Runtime;

namespace BlockGame.AndroidPlayer;

public sealed class AndroidRenderer
{
    private readonly Paint fillPaint = new() { AntiAlias = true };

    public void Draw(Canvas canvas, SceneModel scene, AssetManager? assets)
    {
        canvas.DrawColor(Color.ParseColor("#111827"));
        foreach (var sprite in scene.Sprites)
        {
            DrawSprite(canvas, sprite, assets);
        }
    }

    private void DrawSprite(Canvas canvas, SpriteModel sprite, AssetManager? assets)
    {
        var w = (float)(sprite.Width * sprite.Size);
        var h = (float)(sprite.Height * sprite.Size);
        var left = (float)(sprite.X - w / 2);
        var top = (float)(sprite.Y - h / 2);

        if (!string.IsNullOrWhiteSpace(sprite.ImageAssetPath) && assets is not null)
        {
            try
            {
                var assetPath = sprite.ImageAssetPath.Replace('\\', '/').TrimStart('/');
                if (assetPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = assetPath["assets/".Length..];
                }

                using var stream = assets.Open(assetPath);
                using var bitmap = BitmapFactory.DecodeStream(stream);
                if (bitmap is not null)
                {
                    canvas.Save();
                    canvas.Rotate((float)sprite.Rotation, left + w / 2, top + h / 2);
                    canvas.DrawBitmap(bitmap, null, new RectF(left, top, left + w, top + h), null);
                    canvas.Restore();
                    return;
                }
            }
            catch
            {
                // Fall back to fill color.
            }
        }

        fillPaint.Color = Color.ParseColor(sprite.Fill);
        canvas.Save();
        canvas.Rotate((float)sprite.Rotation, left + w / 2, top + h / 2);
        canvas.DrawRoundRect(new RectF(left, top, left + w, top + h), 8, 8, fillPaint);
        canvas.Restore();
    }
}

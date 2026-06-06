using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BlockGameEngine.ProjectIO;
using BlockGameEngine.Runtime;

namespace BlockGameEngine.WindowsPlayer;

public static class SpriteVisualFactory
{
    public static FrameworkElement Create(SpriteModel sprite, string assetsBaseDirectory)
    {
        var imagePath = ProjectSerializer.ResolveAssetPath(assetsBaseDirectory, sprite.ImageAssetPath);
        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            var image = new Image
            {
                Width = sprite.Width * sprite.Size,
                Height = sprite.Height * sprite.Size,
                Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute)),
                Stretch = Stretch.Fill
            };
            image.RenderTransform = new RotateTransform(sprite.Rotation);
            image.RenderTransformOrigin = new Point(0.5, 0.5);
            return image;
        }

        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = sprite.Width * sprite.Size,
            Height = sprite.Height * sprite.Size,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sprite.Fill)!),
            RadiusX = 4,
            RadiusY = 4
        };
        rect.RenderTransform = new RotateTransform(sprite.Rotation);
        rect.RenderTransformOrigin = new Point(0.5, 0.5);
        return rect;
    }
}

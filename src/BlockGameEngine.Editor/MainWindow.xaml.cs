using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BlockGameEngine.Editor.Controls;
using BlockGameEngine.Editor.ViewModels;
using BlockGameEngine.Runtime;

namespace BlockGameEngine.Editor;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
        DataContext = viewModel;

        GraphCanvas.BlockAdded += (_, block) => viewModel.AddGraphBlock(block);
        GraphCanvas.GraphChanged += (_, _) => viewModel.OnGraphChanged();

        Loaded += (_, _) =>
        {
            RefreshSceneCombo();
            RefreshPreview();
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MainViewModel.CurrentScene) or nameof(MainViewModel.PreviewSprites)
                    or nameof(MainViewModel.AssetsBaseDirectory))
                {
                    if (e.PropertyName == nameof(MainViewModel.CurrentScene))
                    {
                        RefreshSceneCombo();
                    }

                    RefreshPreview();
                }
            };
        };
    }

    private void RefreshSceneCombo()
    {
        var selected = SceneCombo.SelectedItem as SceneModel;
        SceneCombo.ItemsSource = viewModel.Scenes;
        SceneCombo.DisplayMemberPath = nameof(SceneModel.Name);
        SceneCombo.SelectedItem = viewModel.CurrentScene;
        if (selected is not null && viewModel.Scenes.Contains(selected))
        {
            SceneCombo.SelectedItem = selected;
        }
    }

    private void RefreshPreview()
    {
        PreviewCanvas.Children.Clear();
        foreach (var sprite in viewModel.CurrentScene.Sprites)
        {
            var element = SpriteVisualFactory.Create(sprite, viewModel.AssetsBaseDirectory);
            Canvas.SetLeft(element, sprite.X - sprite.Width * sprite.Size / 2);
            Canvas.SetTop(element, sprite.Y - sprite.Height * sprite.Size / 2);
            PreviewCanvas.Children.Add(element);
        }
    }

    private void Palette_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is BlockPaletteItem block)
        {
            DragDrop.DoDragDrop(element, block, DragDropEffects.Copy);
        }
    }

    private void SceneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SceneCombo.SelectedItem is SceneModel scene)
        {
            viewModel.ChangeScene(scene.Name);
            RefreshPreview();
        }
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PreviewCanvas);
        viewModel.SetPointer(pos.X, pos.Y, true);
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PreviewCanvas);
        viewModel.SetPointer(pos.X, pos.Y, false);
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(PreviewCanvas);
            viewModel.SetPointer(pos.X, pos.Y, true);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        viewModel.SetKey(e.Key.ToString(), true);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        viewModel.SetKey(e.Key.ToString(), false);
    }
}

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BlockGameEngine.Editor.ViewModels;

namespace BlockGameEngine.Editor.Controls;

public class BlockGraphCanvas : UserControl
{
    public static readonly DependencyProperty BlocksProperty =
        DependencyProperty.Register(nameof(Blocks), typeof(IEnumerable<GraphBlockViewModel>), typeof(BlockGraphCanvas),
            new PropertyMetadata(null, OnBlocksChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(BlockGraphCanvas),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty SelectedBlockProperty =
        DependencyProperty.Register(nameof(SelectedBlock), typeof(GraphBlockViewModel), typeof(BlockGraphCanvas),
            new PropertyMetadata(null));

    private readonly Canvas graphCanvas = new();
    private readonly Canvas connectionCanvas = new();
    private GraphBlockViewModel? dragBlock;
    private Point dragOffset;
    private GraphBlockViewModel? connectSource;
    private INotifyCollectionChanged? subscribedCollection;

    public BlockGraphCanvas()
    {
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var grid = new Grid();
        grid.Children.Add(connectionCanvas);
        grid.Children.Add(graphCanvas);
        connectionCanvas.Width = 2000;
        connectionCanvas.Height = 1400;
        graphCanvas.Width = 2000;
        graphCanvas.Height = 1400;
        scroll.Content = grid;

        Content = scroll;
        AllowDrop = true;
        Drop += OnDrop;
    }

    public IEnumerable<GraphBlockViewModel>? Blocks
    {
        get => (IEnumerable<GraphBlockViewModel>?)GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public GraphBlockViewModel? SelectedBlock
    {
        get => (GraphBlockViewModel?)GetValue(SelectedBlockProperty);
        set => SetValue(SelectedBlockProperty, value);
    }

    public event EventHandler<GraphBlockViewModel>? BlockAdded;
    public event EventHandler? GraphChanged;

    private static void OnBlocksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (BlockGraphCanvas)d;
        if (canvas.subscribedCollection is not null)
        {
            canvas.subscribedCollection.CollectionChanged -= canvas.OnCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged collection)
        {
            canvas.subscribedCollection = collection;
            collection.CollectionChanged += canvas.OnCollectionChanged;
        }

        canvas.RenderBlocks();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RenderBlocks();

    public void Refresh() => RenderBlocks();

    private void RenderBlocks()
    {
        graphCanvas.Children.Clear();
        connectionCanvas.Children.Clear();

        if (Blocks is null)
        {
            return;
        }

        var blockList = Blocks.ToList();
        foreach (var block in blockList)
        {
            if (block.NextBlockId is not null)
            {
                var target = blockList.FirstOrDefault(b => b.Id == block.NextBlockId);
                if (target is not null)
                {
                    DrawConnection(block, target);
                }
            }
        }

        foreach (var block in blockList)
        {
            graphCanvas.Children.Add(CreateBlockElement(block));
        }
    }

    private void DrawConnection(GraphBlockViewModel from, GraphBlockViewModel to)
    {
        connectionCanvas.Children.Add(new Line
        {
            X1 = from.X + 105,
            Y1 = from.Y + 52,
            X2 = to.X + 105,
            Y2 = to.Y + 8,
            Stroke = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            StrokeThickness = 2
        });
    }

    private UIElement CreateBlockElement(GraphBlockViewModel block)
    {
        var container = new Canvas { Width = 210, Height = 60 };
        Canvas.SetLeft(container, block.X);
        Canvas.SetTop(container, block.Y);

        var border = new Border
        {
            Width = 210,
            Height = 48,
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(block.Color)!),
            CornerRadius = new CornerRadius(7),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x24, 0x30, 0x44)),
            BorderThickness = new Thickness(1),
            Tag = block,
            Cursor = block.IsEventHat ? Cursors.Arrow : Cursors.Hand
        };

        var panel = new DockPanel();
        var topSocket = new Ellipse { Width = 10, Height = 10, Fill = Brushes.White, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(topSocket, Dock.Left);
        panel.Children.Add(topSocket);
        panel.Children.Add(new TextBlock
        {
            Text = block.Label,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        border.Child = panel;

        var bottomSocket = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = Brushes.White
        };
        Canvas.SetLeft(bottomSocket, 100);
        Canvas.SetTop(bottomSocket, 50);

        if (!block.IsEventHat)
        {
            border.MouseLeftButtonDown += (_, e) =>
            {
                SelectedBlock = block;
                dragBlock = block;
                dragOffset = e.GetPosition(border);
                border.CaptureMouse();
                e.Handled = true;
            };

            border.MouseMove += (_, e) =>
            {
                if (dragBlock != block || e.LeftButton != MouseButtonState.Pressed)
                {
                    return;
                }

                var pos = e.GetPosition(graphCanvas);
                block.X = Math.Max(0, pos.X - dragOffset.X);
                block.Y = Math.Max(0, pos.Y - dragOffset.Y);
                RenderBlocks();
                GraphChanged?.Invoke(this, EventArgs.Empty);
            };

            border.MouseLeftButtonUp += (_, _) =>
            {
                if (dragBlock == block)
                {
                    dragBlock = null;
                    border.ReleaseMouseCapture();
                }
            };

            topSocket.MouseLeftButtonUp += (_, e) =>
            {
                if (connectSource is not null && connectSource.Id != block.Id)
                {
                    connectSource.NextBlockId = block.Id;
                    connectSource = null;
                    RenderBlocks();
                    GraphChanged?.Invoke(this, EventArgs.Empty);
                }

                e.Handled = true;
            };
        }

        bottomSocket.MouseLeftButtonDown += (_, e) =>
        {
            connectSource = block;
            e.Handled = true;
        };

        container.Children.Add(border);
        container.Children.Add(bottomSocket);
        return container;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(BlockPaletteItem)) is not BlockPaletteItem paletteItem)
        {
            return;
        }

        var pos = e.GetPosition(graphCanvas);
        var block = new GraphBlockViewModel(
            Guid.NewGuid().ToString("N"),
            paletteItem.Kind,
            paletteItem.Label,
            paletteItem.Color)
        {
            X = Math.Max(0, pos.X - 105),
            Y = Math.Max(0, pos.Y - 24)
        };

        BlockAdded?.Invoke(this, block);
    }
}

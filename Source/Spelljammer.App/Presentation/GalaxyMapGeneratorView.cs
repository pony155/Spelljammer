using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Spelljammer.Simulation.Galaxy;

namespace Spelljammer.Presentation;

internal sealed class GalaxyMapGeneratorCompletedEventArgs(GalaxyMapSelection selection) : EventArgs
{
    internal GalaxyMapSelection Selection { get; } = selection;
}

/// <summary>
/// Presents the bounded first-voyage galaxy settings and a non-authoritative topology preview.
/// </summary>
internal sealed class GalaxyMapGeneratorView : Grid, IDisposable
{
    internal const double LogicalWidth = 1600;
    internal const double LogicalHeight = 900;

    private static readonly SolidColorBrush TextBrush = Brush("#F2E9D8");
    private static readonly SolidColorBrush MutedBrush = Brush("#AAB8D0");
    private static readonly SolidColorBrush GoldBrush = Brush("#D7AF70");
    private static readonly SolidColorBrush CyanBrush = Brush("#80DED9");
    private static readonly SolidColorBrush ErrorBrush = Brush("#FF8C82");
    private static readonly SolidColorBrush PanelBrush = Brush("#F00A1221");
    private static readonly SolidColorBrush PanelBorderBrush = Brush("#664D668A");

    private readonly GameText strings;
    private readonly TextBox seedTextBox;
    private readonly TextBlock statusText;
    private readonly GalaxyTopologyPreview preview;
    private readonly TextBlock starwayMetric;
    private GalaxyMapSelection selection;
    private bool disposed;

    internal GalaxyMapGeneratorView(GameText strings, GalaxyMapSelection? initial)
    {
        this.strings = strings;
        Width = LogicalWidth;
        Height = LogicalHeight;
        Focusable = true;
        ClipToBounds = true;
        AutomationProperties.SetName(this, strings.Get("galaxy.accessibility.screen"));

        selection = initial ?? new GalaxyMapSelection(NewSeed(), new GalaxyGenerationSettings());
        Image background = new() {
            Source = LoadBackground(),
            Stretch = Stretch.UniformToFill,
            Opacity = 0.34,
            IsHitTestVisible = false,
        };
        Children.Add(background);
        Children.Add(new Border {
            Background = new LinearGradientBrush(
                Color.FromArgb(238, 5, 10, 20),
                Color.FromArgb(210, 9, 21, 37),
                new Point(0, 0),
                new Point(1, 1)),
            IsHitTestVisible = false,
        });

        Grid layout = new();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(104) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
        Children.Add(layout);

        layout.Children.Add(BuildHeader());

        Grid body = new() { Margin = new Thickness(42, 16, 42, 14) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(412) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        layout.Children.Add(body);

        Border settingsPanel = Panel();
        settingsPanel.Padding = new Thickness(28, 20, 28, 20);
        body.Children.Add(settingsPanel);
        StackPanel settingsStack = new();
        settingsPanel.Child = settingsStack;
        settingsStack.Children.Add(Heading(strings.Get("galaxy.section.parameters"), 18));
        settingsStack.Children.Add(BodyText(strings.Get("galaxy.introduction"), 13, new Thickness(0, 7, 0, 16)));
        settingsStack.Children.Add(ReadOnlyField(
            strings.Get("galaxy.label.scenario"), strings.Get("galaxy.value.scenario.first-voyage")));
        settingsStack.Children.Add(ReadOnlyField(
            strings.Get("galaxy.label.shape"), strings.Get("galaxy.value.shape.dual-region")));
        settingsStack.Children.Add(ReadOnlyField(
            strings.Get("galaxy.label.systems"), strings.Get("galaxy.value.systems.voyage")));
        settingsStack.Children.Add(ReadOnlyField(
            strings.Get("galaxy.label.generator"), strings.Get("galaxy.value.generator.v1")));
        settingsStack.Children.Add(Label(strings.Get("galaxy.label.seed"), new Thickness(0, 14, 0, 7)));

        Grid seedRow = new();
        seedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        seedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        seedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        seedTextBox = new TextBox {
            Text = selection.Seed.ToString(CultureInfo.InvariantCulture),
            MaxLength = 20,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 16,
            Foreground = TextBrush,
            Background = Brush("#FF111D31"),
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 9, 12, 9),
            CaretBrush = CyanBrush,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(seedTextBox, strings.Get("galaxy.label.seed"));
        seedTextBox.TextChanged += SeedTextBox_TextChanged;
        seedTextBox.KeyDown += SeedTextBox_KeyDown;
        seedRow.Children.Add(seedTextBox);
        Button randomize = Button(strings.Get("galaxy.button.randomize"), primary: false);
        randomize.Click += Randomize_Click;
        Grid.SetColumn(randomize, 2);
        seedRow.Children.Add(randomize);
        settingsStack.Children.Add(seedRow);
        settingsStack.Children.Add(BodyText(strings.Get("galaxy.hint.seed"), 12, new Thickness(0, 7, 0, 14)));

        Button generate = Button(strings.Get("galaxy.button.generate"), primary: false);
        generate.HorizontalAlignment = HorizontalAlignment.Stretch;
        generate.Click += Generate_Click;
        settingsStack.Children.Add(generate);
        settingsStack.Children.Add(BodyText(strings.Get("galaxy.hint.locked"), 12, new Thickness(0, 12, 0, 0)));
        TextBlock legendHeading = Heading(strings.Get("galaxy.legend.title"), 13);
        legendHeading.Foreground = GoldBrush;
        legendHeading.Margin = new Thickness(0, 16, 0, 6);
        settingsStack.Children.Add(legendHeading);
        settingsStack.Children.Add(LegendRow(strings.Get("galaxy.legend.anchor-description"), LegendMark.Anchor));
        settingsStack.Children.Add(LegendRow(strings.Get("galaxy.legend.local-starway"), LegendMark.LocalStarway));
        settingsStack.Children.Add(LegendRow(strings.Get("galaxy.legend.crossing-starway"), LegendMark.CrossingStarway));

        Grid.SetColumn(settingsPanel, 0);

        Border previewPanel = Panel();
        previewPanel.Padding = new Thickness(26, 22, 26, 22);
        Grid.SetColumn(previewPanel, 2);
        body.Children.Add(previewPanel);
        Grid previewLayout = new();
        previewLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        previewLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        previewLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(84) });
        previewPanel.Child = previewLayout;

        Grid previewHeader = new();
        StackPanel previewTitles = new();
        previewTitles.Children.Add(Heading(strings.Get("galaxy.preview.title"), 18));
        previewTitles.Children.Add(BodyText(strings.Get("galaxy.preview.subtitle"), 12));
        previewHeader.Children.Add(previewTitles);
        previewLayout.Children.Add(previewHeader);

        preview = new GalaxyTopologyPreview(
            strings.Get("galaxy.region.inner"),
            strings.Get("galaxy.region.frontier"),
            strings.Get("galaxy.legend.anchor"));
        AutomationProperties.SetName(preview, strings.Get("galaxy.accessibility.map"));
        Grid.SetRow(preview, 1);
        previewLayout.Children.Add(preview);

        UniformGrid metrics = new() { Columns = 3, Margin = new Thickness(0, 14, 0, 0) };
        metrics.Children.Add(Metric(strings.Get("galaxy.metric.systems"), "16"));
        starwayMetric = MetricValue("—");
        metrics.Children.Add(Metric(strings.Get("galaxy.metric.starways"), starwayMetric));
        metrics.Children.Add(Metric(strings.Get("galaxy.metric.regions"), "2"));
        Grid.SetRow(metrics, 2);
        previewLayout.Children.Add(metrics);

        Border footer = new() {
            Background = Brush("#F20A1221"),
            BorderBrush = GoldBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(42, 18, 42, 18),
        };
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        Grid footerGrid = new();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Child = footerGrid;
        Button back = Button(strings.Get("galaxy.button.back"), primary: false);
        back.Width = 180;
        back.Click += Back_Click;
        footerGrid.Children.Add(back);
        statusText = new TextBlock {
            Foreground = MutedBrush,
            FontSize = 13,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(26, 0, 26, 0),
        };
        Grid.SetColumn(statusText, 1);
        footerGrid.Children.Add(statusText);
        Button continueButton = Button(strings.Get("galaxy.button.continue"), primary: true);
        continueButton.Width = 238;
        continueButton.Click += Continue_Click;
        Grid.SetColumn(continueButton, 2);
        footerGrid.Children.Add(continueButton);

        KeyDown += View_KeyDown;
        Loaded += View_Loaded;
        GeneratePreview();
    }

    internal event EventHandler<GalaxyMapGeneratorCompletedEventArgs>? Completed;
    internal event EventHandler? CancelRequested;

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Loaded -= View_Loaded;
        KeyDown -= View_KeyDown;
        seedTextBox.TextChanged -= SeedTextBox_TextChanged;
        seedTextBox.KeyDown -= SeedTextBox_KeyDown;
        Children.Clear();
        GC.SuppressFinalize(this);
    }

    private UIElement BuildHeader()
    {
        Border header = new() {
            Background = Brush("#F20A1221"),
            BorderBrush = GoldBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(54, 12, 54, 10),
        };
        StackPanel stack = new();
        header.Child = stack;
        TextBlock eyebrow = new() {
            Text = strings.Get("galaxy.eyebrow"),
            Foreground = GoldBrush,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
        };
        stack.Children.Add(eyebrow);
        stack.Children.Add(new TextBlock {
            Text = strings.Get("galaxy.title"),
            Foreground = TextBrush,
            FontSize = 29,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        });
        return header;
    }

    private Border ReadOnlyField(string label, string value)
    {
        Border field = new() {
            BorderBrush = Brush("#334D668A"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 7, 0, 7),
        };
        Grid row = new();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Label(label));
        TextBlock lockIcon = new() {
            Text = "\uE72E",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10,
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(lockIcon, 1);
        row.Children.Add(lockIcon);
        TextBlock valueText = new() {
            Text = value,
            Foreground = TextBrush,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(valueText, 2);
        row.Children.Add(valueText);
        field.Child = row;
        return field;
    }

    private static Border Panel() => new() {
        Background = PanelBrush,
        BorderBrush = PanelBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(3),
    };

    private static TextBlock Heading(string text, double size) => new() {
        Text = text,
        Foreground = TextBrush,
        FontSize = size,
        FontWeight = FontWeights.SemiBold,
    };

    private static TextBlock BodyText(string text, double size, Thickness margin = default) => new() {
        Text = text,
        Foreground = MutedBrush,
        FontSize = size,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = size * 1.45,
        Margin = margin,
    };

    private static TextBlock Label(string text, Thickness margin = default) => new() {
        Text = text,
        Foreground = GoldBrush,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = margin,
    };

    private static Button Button(string text, bool primary)
    {
        Button button = new() {
            Content = text,
            Foreground = primary ? Brush("#FF07111E") : TextBrush,
            Background = primary ? CyanBrush : Brush("#FF17243A"),
            BorderBrush = primary ? CyanBrush : PanelBorderBrush,
            BorderThickness = new Thickness(1),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(14, 10, 14, 10),
            Cursor = Cursors.Hand,
            MinHeight = 42,
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Border Metric(string label, string value) => Metric(label, MetricValue(value));

    private static Border Metric(string label, TextBlock value)
    {
        Border card = new() {
            BorderBrush = Brush("#334D668A"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(14, 10, 14, 10),
        };
        StackPanel stack = new();
        stack.Children.Add(value);
        stack.Children.Add(new TextBlock {
            Text = label,
            Foreground = MutedBrush,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        card.Child = stack;
        return card;
    }

    private static TextBlock MetricValue(string value) => new() {
        Text = value,
        Foreground = CyanBrush,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Grid LegendRow(string text, LegendMark mark)
    {
        Grid row = new() { Height = 23 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        FrameworkElement symbol;
        if (mark == LegendMark.Anchor)
        {
            symbol = new Border {
                Width = 13,
                Height = 13,
                CornerRadius = new CornerRadius(7),
                Background = CyanBrush,
                BorderBrush = Brush("#FFB9FFFF"),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else
        {
            symbol = new Line {
                X1 = 0,
                X2 = 29,
                Y1 = 7,
                Y2 = 7,
                Stroke = mark == LegendMark.CrossingStarway ? Brush("#FF5AAFC6") : Brush("#FF7388A8"),
                StrokeThickness = 2,
                StrokeDashArray = mark == LegendMark.CrossingStarway ? new DoubleCollection([4, 3]) : null,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        row.Children.Add(symbol);
        TextBlock label = BodyText(text, 11);
        label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        return row;
    }

    private enum LegendMark : byte
    {
        Anchor,
        LocalStarway,
        CrossingStarway,
    }

    private void GeneratePreview()
    {
        if (!TryReadSeed(out ulong seed))
        {
            preview.Galaxy = null;
            starwayMetric.Text = "—";
            SetStatus(strings.Get("galaxy.status.invalid-seed"), isError: true);
            return;
        }

        GalaxyGenerationSettings settings = new();
        GalaxyGenerationResult result = GalaxyGenerator.Generate(seed, settings);
        if (!result.Succeeded)
        {
            preview.Galaxy = null;
            starwayMetric.Text = "—";
            SetStatus(strings.Get("galaxy.status.generation-failed"), isError: true);
            return;
        }

        selection = new GalaxyMapSelection(seed, settings);
        preview.Galaxy = result.Galaxy;
        starwayMetric.Text = result.Galaxy!.Topology.Starways.Count.ToString(strings.Culture);
        SetStatus(strings.Get("galaxy.status.ready"), isError: false);
    }

    private bool TryReadSeed(out ulong seed) =>
        ulong.TryParse(seedTextBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out seed) && seed != 0;

    private void SetStatus(string text, bool isError)
    {
        statusText.Text = text;
        statusText.Foreground = isError ? ErrorBrush : MutedBrush;
    }

    private void View_Loaded(object sender, RoutedEventArgs e) => seedTextBox.Focus();

    private void View_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SeedTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            GeneratePreview();
        }
    }

    private void SeedTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!TryReadSeed(out ulong seed) || seed != selection.Seed)
        {
            SetStatus(strings.Get("galaxy.status.preview-stale"), isError: false);
        }
    }

    private void Randomize_Click(object sender, RoutedEventArgs e)
    {
        seedTextBox.Text = NewSeed().ToString(CultureInfo.InvariantCulture);
        GeneratePreview();
        seedTextBox.SelectAll();
        seedTextBox.Focus();
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => GeneratePreview();

    private void Back_Click(object sender, RoutedEventArgs e) =>
        CancelRequested?.Invoke(this, EventArgs.Empty);

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        GeneratePreview();
        if (preview.Galaxy is not null)
        {
            Completed?.Invoke(this, new GalaxyMapGeneratorCompletedEventArgs(selection));
        }
    }

    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong value;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }
        while (value == 0);

        return value;
    }

    private static BitmapSource LoadBackground()
    {
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri("pack://application:,,,/Assets/UI/MainMenu/Background.png", UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static SolidColorBrush Brush(string value)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Draws a read-only, bounded projection of immutable galaxy topology.
/// </summary>
internal sealed class GalaxyTopologyPreview(string innerRegion, string frontierRegion, string anchorLabel) : FrameworkElement
{
    private static readonly Brush BackgroundBrush = FrozenBrush("#D907101F");
    private static readonly Brush GridBrush = FrozenBrush("#184D668A");
    private static readonly Brush RouteBrush = FrozenBrush("#996F86A8");
    private static readonly Brush AlternateRouteBrush = FrozenBrush("#B35AAFC6");
    private static readonly Brush RegionBrush = FrozenBrush("#1872B3C7");
    private static readonly Brush NodeBrush = FrozenBrush("#FFE8D8B8");
    private static readonly Brush AnchorBrush = FrozenBrush("#FF80DED9");
    private static readonly Brush LabelBrush = FrozenBrush("#FFC9D6E8");
    private static readonly Brush MutedBrush = FrozenBrush("#FF91A3C1");
    private GalaxyState? galaxy;

    internal GalaxyState? Galaxy {
        get => galaxy;
        set {
            galaxy = value;
            InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        Rect bounds = new(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRoundedRectangle(BackgroundBrush, new Pen(FrozenBrush("#554D668A"), 1), bounds, 3, 3);
        if (ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        DrawGrid(drawingContext, bounds);
        if (galaxy is null || galaxy.Topology.Systems.Count == 0)
        {
            return;
        }

        IReadOnlyList<StarSystemState> systems = galaxy.Topology.Systems.Values.OrderBy(value => value.Ordinal).ToArray();
        int minimumY = systems.Min(value => value.DisplayY);
        int maximumY = systems.Max(value => value.DisplayY);
        int[] regions = systems.Select(value => value.Region).Distinct().Order().ToArray();
        Dictionary<int, (int Minimum, int Maximum)> horizontalBounds = regions.ToDictionary(
            region => region,
            region => (
                systems.Where(value => value.Region == region).Min(value => value.DisplayX),
                systems.Where(value => value.Region == region).Max(value => value.DisplayX)));
        const double paddingX = 72;
        const double paddingY = 64;
        const double regionGap = 112;
        Point Project(StarSystemState system)
        {
            int regionIndex = Array.IndexOf(regions, system.Region);
            (int regionMinimumX, int regionMaximumX) = horizontalBounds[system.Region];
            double regionWidth = Math.Max(1,
                (ActualWidth - paddingX * 2 - regionGap * (regions.Length - 1)) / regions.Length);
            double regionOriginX = paddingX + regionIndex * (regionWidth + regionGap);
            double xRange = Math.Max(1, regionMaximumX - regionMinimumX);
            double yRange = Math.Max(1, maximumY - minimumY);
            return new Point(
                regionOriginX + ((system.DisplayX - regionMinimumX) / xRange * regionWidth),
                paddingY + ((system.DisplayY - minimumY) / yRange * Math.Max(1, ActualHeight - paddingY * 2)));
        }

        Dictionary<StarSystemId, Point> points = systems.ToDictionary(value => value.Id, Project);
        foreach (int region in systems.Select(value => value.Region).Distinct().Order())
        {
            Point[] regionPoints = systems.Where(value => value.Region == region).Select(Project).ToArray();
            Rect regionBounds = Rect.Empty;
            foreach (Point point in regionPoints)
            {
                regionBounds.Union(point);
            }

            regionBounds.Inflate(42, 38);
            drawingContext.DrawRoundedRectangle(RegionBrush, new Pen(FrozenBrush("#3367AAB9"), 1), regionBounds, 32, 32);
            DrawText(drawingContext, region == 0 ? innerRegion : frontierRegion,
                new Point(regionBounds.Left + 12, regionBounds.Top + 9), 11, MutedBrush, FontWeights.SemiBold);
        }

        foreach (StarwayState starway in galaxy.Topology.Starways.Values.OrderBy(value => value.Id))
        {
            Point first = points[starway.FirstSystemId];
            Point second = points[starway.SecondSystemId];
            bool crossesRegion = galaxy.Topology.Systems[starway.FirstSystemId].Region !=
                galaxy.Topology.Systems[starway.SecondSystemId].Region;
            if (crossesRegion)
            {
                double horizontalSpan = second.X - first.X;
                StreamGeometry curve = new();
                using (StreamGeometryContext context = curve.Open())
                {
                    context.BeginFigure(first, isFilled: false, isClosed: false);
                    context.BezierTo(
                        new Point(first.X + horizontalSpan * 0.36, first.Y),
                        new Point(second.X - horizontalSpan * 0.36, second.Y),
                        second,
                        isStroked: true,
                        isSmoothJoin: true);
                }

                curve.Freeze();
                Pen bridgePen = new(AlternateRouteBrush, 2) { DashStyle = DashStyles.Dash };
                drawingContext.DrawGeometry(null, bridgePen, curve);
            }
            else
            {
                drawingContext.DrawLine(new Pen(RouteBrush, 2), first, second);
            }
        }

        foreach (StarSystemState system in systems)
        {
            Point point = points[system.Id];
            bool anchor = system.Ordinal == 0;
            double radius = anchor ? 10 : 6;
            if (anchor)
            {
                drawingContext.DrawEllipse(null, new Pen(AnchorBrush, 2), point, 16, 16);
            }

            drawingContext.DrawEllipse(anchor ? AnchorBrush : NodeBrush, null, point, radius, radius);
            if (anchor)
            {
                DrawText(drawingContext, (system.Ordinal + 1).ToString("00", CultureInfo.InvariantCulture),
                    new Point(point.X + 19, point.Y - 19), 10, LabelBrush, FontWeights.SemiBold);
                DrawText(drawingContext, anchorLabel, new Point(point.X + 19, point.Y + 1),
                    9, AnchorBrush, FontWeights.SemiBold);
            }
            else
            {
                DrawText(drawingContext, (system.Ordinal + 1).ToString("00", CultureInfo.InvariantCulture),
                    new Point(point.X + 10, point.Y - 9), 10, LabelBrush, FontWeights.SemiBold);
            }
        }
    }

    private void DrawGrid(DrawingContext drawingContext, Rect bounds)
    {
        Pen pen = new(GridBrush, 1);
        for (double x = 40; x < bounds.Width; x += 40)
        {
            drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, bounds.Height));
        }

        for (double y = 40; y < bounds.Height; y += 40)
        {
            drawingContext.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
        }
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Point origin,
        double size,
        Brush brush,
        FontWeight weight)
    {
        FormattedText formatted = new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(formatted, origin);
    }

    private static SolidColorBrush FrozenBrush(string value)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}

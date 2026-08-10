using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wachin.Models;
using Wachin.Services;
using Wachin.ViewModels;

namespace Wachin;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _hudTimer;

    public MainWindow()
    {
        InitializeComponent();
        // Pre-hide root so the entry animation starts from 0 (no startup flash)
        RootGrid.Opacity = 0;
        _vm = new MainViewModel();
        DataContext = _vm;

        StateChanged += (_, _) => OnWindowStateChanged();

        NavList.ItemsSource = _vm.NavItems;
        AppServices.ToastRequested += OnToastRequested;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) => RemoveOldestToast();
        _toastTimer.Start();

        // Live HUD status update every second
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _hudTimer.Tick += (_, _) => UpdateHud();
        _hudTimer.Start();

        // Select home on load + entry animation
        Loaded += (_, _) =>
        {
            NavList.SelectedIndex = 0;
            PlayEntryAnimation();
        };
    }

    // ── Custom window chrome ────────────────────────────────────────────────────

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (WindowState == WindowState.Normal)
            DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        OnWindowStateChanged();
    }

    private void UpdateMaxGlyph()
        => MaxGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";

    // Al maximizar: esquinas cuadradas (nativo); al restaurar: redondeadas
    private void OnWindowStateChanged()
    {
        UpdateMaxGlyph();
        bool max = WindowState == WindowState.Maximized;
        RootChrome.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(10);
    }

    private void UpdateHud()
    {
        try
        {
            var s = SystemInfoService.GetQuickStatus();
            HudClock.Text = s.Clock;
            HudRamPct.Text = $"{s.RamPercent}%";
            HudRamBar.Value = s.RamPercent;
            HudRamDetail.Text = $"{s.RamUsed} / {s.RamTotal}";
            HudUptime.Text = s.Uptime;
        }
        catch { }
    }

    private void PlayEntryAnimation()
    {
        // Snappy premium entry: fade + subtle scale with a custom spline (cubic-bezier feel)
        var spline = new System.Windows.Media.Animation.SplineDoubleKeyFrame
        {
            KeyTime = System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)),
            Value = 1,
            KeySpline = new System.Windows.Media.Animation.KeySpline(0.16, 1, 0.3, 1)
        };
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        fadeIn.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fadeIn.KeyFrames.Add(spline);
        RootGrid.BeginAnimation(OpacityProperty, fadeIn);

        // Subtle rise + zoom of the content shell
        var t = new System.Windows.Media.TransformGroup();
        var scale = new System.Windows.Media.ScaleTransform(0.992, 0.992);
        var rise = new System.Windows.Media.TranslateTransform(10, 0);
        t.Children.Add(scale);
        t.Children.Add(rise);
        ContentHost.RenderTransform = t;
        ContentHost.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

        var riseAnim = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        riseAnim.KeyFrames.Add(new System.Windows.Media.Animation.SplineDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)), new System.Windows.Media.Animation.KeySpline(0.16, 1, 0.3, 1)));
        rise.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, riseAnim);

        var scaleAnim = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        scaleAnim.KeyFrames.Add(new System.Windows.Media.Animation.SplineDoubleKeyFrame(1, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)), new System.Windows.Media.Animation.KeySpline(0.16, 1, 0.3, 1)));
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
    }

    // ── Navigation ──────────────────────────────────────────────────────────

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is NavItemVm nav && !nav.IsHeader)
        {
            _vm.SelectedNav = nav;
            PageTitle.Text = _vm.PageTitle;
            PageSubtitle.Text = _vm.PageSubtitle;

            // El subtitulo de Inicio es un aviso importante: rojo y doble de tamaño
            if (nav.Id == "home")
            {
                PageSubtitle.FontSize = 26; // doble del Body (13)
                PageSubtitle.Foreground = (Brush)FindResource("Danger");
            }
            else
            {
                PageSubtitle.FontSize = 13;
                PageSubtitle.Foreground = (Brush)FindResource("TextSecondary");
            }
        }
    }

    // ── Toast system ────────────────────────────────────────────────────────

    private void OnToastRequested(Toast toast)
    {
        Dispatcher.Invoke(() => ShowToast(toast));
    }

    private void ShowToast(Toast toast)
    {
        string bgColor = toast.Kind switch
        {
            ToastKind.Success => "ToastSuccessBg",
            ToastKind.Error => "ToastErrorBg",
            ToastKind.Warning => "ToastWarningBg",
            _ => "ToastInfoBg"
        };
        string icon = toast.Kind switch
        {
            ToastKind.Success => "\uE73E",
            ToastKind.Error => "\uEA39",
            ToastKind.Warning => "\uE7BA",
            _ => "\uE9CE"
        };
        string iconColor = toast.Kind switch
        {
            ToastKind.Success => "Success",
            ToastKind.Error => "Danger",
            ToastKind.Warning => "RiskMedium",
            _ => "Info"
        };

        var border = new Border
        {
            Background = (Brush)FindResource(bgColor),
            BorderBrush = (Brush)FindResource("BorderDefault"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8),
            MaxWidth = 380,
            IsHitTestVisible = true
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconText = new TextBlock
        {
            Text = icon,
            FontFamily = (FontFamily)FindResource("IconFont"),
            FontSize = 16,
            Foreground = (Brush)FindResource(iconColor),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(iconText, 0);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock
        {
            Text = toast.Title,
            FontFamily = (FontFamily)FindResource("AppFont"),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary")
        };
        var msg = new TextBlock
        {
            Text = toast.Message,
            FontFamily = (FontFamily)FindResource("AppFont"),
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 280
        };
        stack.Children.Add(title);
        stack.Children.Add(msg);

        if (toast.ActionText != null && toast.Action != null)
        {
            var btn = new Button
            {
                Content = toast.ActionText,
                Style = (Style)FindResource("SmallButton"),
                Foreground = (Brush)FindResource("Accent"),
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            btn.Click += (_, _) =>
            {
                toast.Action.Invoke();
                ToastHost.Items.Remove(border);
            };
            stack.Children.Add(btn);
        }

        Grid.SetColumn(stack, 2);
        grid.Children.Add(iconText);
        grid.Children.Add(stack);
        border.Child = grid;

        border.Tag = DateTime.Now;
        ToastHost.Items.Add(border);
        border.RenderTransform = new TranslateTransform(40, 0);
        border.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));

        // Simple slide in
        var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(200));
        border.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
    }

    private void RemoveOldestToast()
    {
        if (ToastHost.Items.Count == 0) return;

        var oldest = ToastHost.Items[0] as Border;
        if (oldest?.Tag is DateTime created && (DateTime.Now - created).TotalSeconds > 4)
        {
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (_, _) => ToastHost.Items.Remove(oldest);
            oldest.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OhMyCodeStatusIndicator {
  public class WindowConfig {
    public double Width { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public bool CustomPositioned { get; set; }

    public WindowConfig() {
      Width = 320;
      CustomPositioned = false;
    }
  }

  public class SessionData {
    public string SessionId { get; set; }
    public string Title { get; set; }
    public string Directory { get; set; }
    public string Status { get; set; } // "working", "waiting", "idle"
    public ImageSource ProjectIcon { get; set; }
    public Color ProjectColor { get; set; }

    public SessionData() {
      ProjectColor = Color.FromRgb(100, 110, 130);
    }
  }

  public class ServiceConfig {
    public int port { get; set; }
    public string password { get; set; }
  }

  public static class TamagotchiSprites {
    // 6 frames of 26x34: Idle1, Idle2, Work1, Work2, Wait1, Wait2
    private const string Base64Strip = "iVBORw0KGgoAAAANSUhEUgAAAJwAAAAiCAYAAACqeLBIAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAANASURBVHhe7ZsxqhRBFEX/IkRUfiAYuwMTwQWYuBdjYxcxuAEXYWxiKD8xMTExFqGlBmrsOXbdW69ruuY7UxcOQr/3zptpnx8EvbkZuahML99MCT4/dabPz6cEn49cWcbBjXRNr4MbGWnKr9/TlNh9+Et+xt4U9RNOumQxGOVSNXpclEvV6HFRLlWjx0W5VI2eFPUTTrlUjR4X6ZLFYJRL1ehxUS5Vo8dFuVSNHhflUjV6XJRL1ehxkS5ZDEa5VI0eF+VSNXpclEvV6HFRLlWjx0W5VI0eF+mSxWCUS9XocVEuVaPHRblUjR4X5VI1elyUS9XocZEuWQxGuVSNHhflUjV6XJRL1ehxUS5Vo8dFuVSNHhfpksVglEvV6HFRLlWjx0W5VI0eF+VSNXpclEvV6HGRLlkMRrlUjR4X5VI1elyUS9XocVEuVaPHRbkU9Lhw/sjFB1stUtDjwvla6HHhfC30uHC+FnpcOF8LPS6cP3LxwVaLFPS4cL4Welw4Xws9LpyvhR4Xzp+De/EhBtfDOLhBV8bBDboyDm7QlXFwg66Mgxt0ZRzcQPLg4e0B1khNb/HgaobX9J6LyGeM9JLIbKSXRGYjvSQyW9N71oN79OTpP7BnTe8Skc8Y6SWR2UgvicxGeklktqb3rAfXk8hnjPSSyGykl0RmI71bUzy4wWALrv7g0n8DSPD5qem1575z8QeX8u79rokUekmvPf87F39w6Tfyy9fvTSQHvaTXnnPDf4GSw74S9Y2FsK8V+nPYV0uvQ+i1pwTfVw77Wpn/BWTNX0aqPxAXRBfVQn/rHncIN3fTHj6PHoLb8/bntIfPo3tK8H1F31vtcdIf2RP6U8AFaxbx+RL0t+5xh1B7cPPkPfNnbs+ag1v6PiX4vlrfW4ncm/1rZkPNLYv4fIlT7IkcQg3Jsfv4ae9Lv+Y982en2jPPmu/e8t4i/fM9rC8x39F1EetLtO7hIZyCpT0p+Tn717J02PM9itTX8t5y2ENST2QP/YewkaSeUyxiH0k9LXtevHo9bUnvPTn83iT1tLy3nFS7+/bjCM6WWJqj/5DSQA1Lc/TnlPprWJqj//Hts2lLeu/JKX3/Gpbm6O+RP6LFy6rIEjoZAAAAAElFTkSuQmCC";

    public static BitmapSource[] IdleFrames;
    public static BitmapSource[] WorkFrames;
    public static BitmapSource[] WaitFrames;

    static TamagotchiSprites() {
      byte[] bytes = Convert.FromBase64String(Base64Strip);
      var bmp = new BitmapImage();
      using (var ms = new MemoryStream(bytes)) {
        bmp.BeginInit();
        bmp.StreamSource = ms;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
      }

      Func<int, BitmapSource> slice = (idx) => {
        var cb = new CroppedBitmap(bmp, new System.Windows.Int32Rect(idx * 26, 0, 26, 34));
        cb.Freeze();
        return cb;
      };

      IdleFrames = new BitmapSource[] { slice(0), slice(1) };
      WorkFrames = new BitmapSource[] { slice(2), slice(3) };
      WaitFrames = new BitmapSource[] { slice(4), slice(5) };
    }
  }

  public class MainWindow : Window {
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    private StackPanel _itemsPanel;
    private TextBlock _appLabel;
    private TextBlock _summaryText;
    private TextBlock _connectionStatusText;
    private System.Windows.Shapes.Rectangle _separator;
    private Button _smallerBtn;
    private Button _widerBtn;
    private Button _collapseBtn;
    private Ellipse _pulseDot;
    private Border _mainCard;
    private Image _petImage;
    private DispatcherTimer _pollTimer;
    private DispatcherTimer _petAnimTimer;
    private JavaScriptSerializer _serializer = new JavaScriptSerializer();
    private string _authHeader;
    private int _port;
    private Storyboard _pulseStoryboard;
    private Storyboard _waitingStoryboard;
    private bool _isCollapsed = false;
    private bool _isInitialized = false;
    private WindowConfig _config = new WindowConfig();
    private string _configPath;
    private Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Color> _colorCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

    private string _currentPetState = "idle";
    private int _currentPetFrameIdx = 0;

    public MainWindow() {
      Title = "OhMyCode Sessions Indicator";
      MinWidth = 230;
      MaxWidth = 600;
      SizeToContent = SizeToContent.Height;
      WindowStyle = WindowStyle.None;
      AllowsTransparency = true;
      Background = Brushes.Transparent;
      Topmost = true;
      ShowInTaskbar = false;
      ResizeMode = ResizeMode.NoResize;

      LoadWindowConfig();
      Width = Math.Max(MinWidth, Math.Min(MaxWidth, _config.Width));

      BuildUI();
      LoadServiceConfig();

      Loaded += (s, e) => {
        if (!_config.CustomPositioned) {
          PositionAtBottomRight();
        } else {
          Left = _config.Left;
          Top = _config.Top;
        }
        StartPolling();
        StartPetAnimation();
        _isInitialized = true;
      };

      LocationChanged += (s, e) => {
        if (_isInitialized) SaveWindowConfig();
      };
      SizeChanged += (s, e) => {
        if (_isInitialized) SaveWindowConfig();
      };
    }

    private void LoadWindowConfig() {
      try {
        string dir = System.IO.Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          @"ru.ohmylama.ohmycode\data\opencode\storage\oh-my-opencode-slim"
        );
        _configPath = System.IO.Path.Combine(dir, "indicator-settings.json");
        if (File.Exists(_configPath)) {
          var cfg = _serializer.Deserialize<WindowConfig>(File.ReadAllText(_configPath, Encoding.UTF8));
          if (cfg != null) {
            _config = cfg;
          }
        }
      } catch { }
    }

    private void SaveWindowConfig() {
      try {
        if (string.IsNullOrEmpty(_configPath)) return;
        _config.Width = Width;
        _config.Left = Left;
        _config.Top = Top;
        _config.CustomPositioned = true;
        string json = _serializer.Serialize(_config);
        File.WriteAllText(_configPath, json, Encoding.UTF8);
      } catch { }
    }

    private void BuildUI() {
      var outerGrid = new Grid();

      _mainCard = new Border {
        Background = new SolidColorBrush(Color.FromArgb(238, 20, 22, 27)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(140, 60, 64, 75)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(11, 8, 11, 8),
        Margin = new Thickness(8, 28, 8, 8)
      };

      var dropShadow = new System.Windows.Media.Effects.DropShadowEffect {
        BlurRadius = 16,
        ShadowDepth = 3,
        Opacity = 0.55,
        Color = Colors.Black
      };
      _mainCard.Effect = dropShadow;

      var rootGrid = new Grid();
      rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
      rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Separator
      rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Items
      rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer hint

      // Header Grid
      var headerGrid = new Grid { Cursor = Cursors.SizeAll };
      headerGrid.MouseLeftButtonDown += (s, e) => {
        if (e.ButtonState == MouseButtonState.Pressed) {
          DragMove();
          SaveWindowConfig();
        }
      };

      headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
      headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

      // Pulse indicator dot
      _pulseDot = new Ellipse {
        Width = 8,
        Height = 8,
        Fill = new SolidColorBrush(Color.FromRgb(90, 100, 120)),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0)
      };
      Grid.SetColumn(_pulseDot, 0);
      headerGrid.Children.Add(_pulseDot);

      // Title & Summary
      var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
      _appLabel = new TextBlock {
        Text = "OHMYCODE SESSIONS",
        FontSize = 9.5,
        FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180))
      };
      _summaryText = new TextBlock {
        Text = "Подключение к OhMyCode...",
        FontSize = 11.5,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        Margin = new Thickness(0, 1, 0, 0),
        TextTrimming = TextTrimming.CharacterEllipsis
      };
      titleStack.Children.Add(_appLabel);
      titleStack.Children.Add(_summaryText);
      Grid.SetColumn(titleStack, 1);
      headerGrid.Children.Add(titleStack);

      // Header buttons (Width -, Width +, Collapse, Focus OhMyCode)
      var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

      _smallerBtn = new Button {
        Content = "◀",
        Width = 18,
        Height = 18,
        FontSize = 9,
        Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        ToolTip = "Уже (уменьшить ширину на 30px)"
      };
      _smallerBtn.Click += (s, e) => ChangeWidth(-30);
      btnStack.Children.Add(_smallerBtn);

      _widerBtn = new Button {
        Content = "▶",
        Width = 18,
        Height = 18,
        FontSize = 9,
        Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Margin = new Thickness(1, 0, 3, 0),
        ToolTip = "Шире (увеличить ширину на 30px)"
      };
      _widerBtn.Click += (s, e) => ChangeWidth(+30);
      btnStack.Children.Add(_widerBtn);

      _collapseBtn = new Button {
        Content = "—",
        Width = 20,
        Height = 20,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 200)),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        ToolTip = "Свернуть / развернуть список"
      };
      _collapseBtn.Click += (s, e) => ToggleCollapse();
      btnStack.Children.Add(_collapseBtn);

      var focusBtn = new Button {
        Content = "↗",
        Width = 20,
        Height = 20,
        FontSize = 12,
        FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 255)),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        Margin = new Thickness(2, 0, 0, 0),
        ToolTip = "Развернуть окно OhMyCode"
      };
      focusBtn.Click += (s, e) => FocusOhMyCode();
      btnStack.Children.Add(focusBtn);

      Grid.SetColumn(btnStack, 2);
      headerGrid.Children.Add(btnStack);

      Grid.SetRow(headerGrid, 0);
      rootGrid.Children.Add(headerGrid);

      // Separator
      _separator = new System.Windows.Shapes.Rectangle {
        Height = 1,
        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        Margin = new Thickness(0, 7, 0, 7)
      };
      Grid.SetRow(_separator, 1);
      rootGrid.Children.Add(_separator);

      // Items panel
      _itemsPanel = new StackPanel();
      Grid.SetRow(_itemsPanel, 2);
      rootGrid.Children.Add(_itemsPanel);

      // Footer
      _connectionStatusText = new TextBlock {
        Text = "Клик по задаче — развернуть окно",
        FontSize = 9.5,
        Foreground = new SolidColorBrush(Color.FromArgb(130, 140, 150, 170)),
        Margin = new Thickness(0, 5, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Center
      };
      Grid.SetRow(_connectionStatusText, 3);
      rootGrid.Children.Add(_connectionStatusText);

      _mainCard.Child = rootGrid;
      outerGrid.Children.Add(_mainCard);

      // Tamagotchi Pixel Llama sitting on the top edge
      _petImage = new Image {
        Source = TamagotchiSprites.IdleFrames[0],
        Width = 26,
        Height = 34,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(24, 0, 0, 0),
        Cursor = Cursors.SizeAll
      };
      RenderOptions.SetBitmapScalingMode(_petImage, BitmapScalingMode.NearestNeighbor);

      _petImage.MouseLeftButtonDown += (s, e) => {
        if (e.ButtonState == MouseButtonState.Pressed) {
          DragMove();
          SaveWindowConfig();
        }
      };

      outerGrid.Children.Add(_petImage);

      Content = outerGrid;

      InitAnimations();
    }

    private void StartPetAnimation() {
      _petAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
      _petAnimTimer.Tick += (s, e) => OnPetAnimTick();
      _petAnimTimer.Start();
    }

    private void OnPetAnimTick() {
      BitmapSource[] currentFrames;
      if (_currentPetState == "waiting") {
        currentFrames = TamagotchiSprites.WaitFrames;
      } else if (_currentPetState == "working") {
        currentFrames = TamagotchiSprites.WorkFrames;
      } else {
        currentFrames = TamagotchiSprites.IdleFrames;
      }

      if (currentFrames == null || currentFrames.Length == 0) return;

      _currentPetFrameIdx = (_currentPetFrameIdx + 1) % currentFrames.Length;
      _petImage.Source = currentFrames[_currentPetFrameIdx];
    }

    private void SetPetState(string newState) {
      if (_currentPetState == newState) return;
      _currentPetState = newState;
      _currentPetFrameIdx = 0;

      // Adjust animation speed based on state
      if (_currentPetState == "waiting") {
        _petAnimTimer.Interval = TimeSpan.FromMilliseconds(300);
      } else if (_currentPetState == "working") {
        _petAnimTimer.Interval = TimeSpan.FromMilliseconds(420);
      } else {
        _petAnimTimer.Interval = TimeSpan.FromMilliseconds(800);
      }

      OnPetAnimTick();
    }

    protected override void OnSourceInitialized(EventArgs e) {
      base.OnSourceInitialized(e);
      var source = (HwndSource)PresentationSource.FromVisual(this);
      if (source != null) {
        source.AddHook(WndProc);
      }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
      const int WM_NCHITTEST = 0x0084;
      if (msg == WM_NCHITTEST) {
        int x = lParam.ToInt32() & 0xFFFF;
        int y = (lParam.ToInt32() >> 16) & 0xFFFF;
        var pt = PointFromScreen(new Point(x, y));
        const int border = 8;

        if (pt.X <= border) {
          handled = true;
          return (IntPtr)HTLEFT;
        } else if (pt.X >= ActualWidth - border) {
          handled = true;
          return (IntPtr)HTRIGHT;
        }
      }
      return IntPtr.Zero;
    }

    private void ChangeWidth(double delta) {
      double newWidth = Math.Max(MinWidth, Math.Min(MaxWidth, Width + delta));
      // Keep right edge pinned when adjusting width
      double right = Left + Width;
      Width = newWidth;
      Left = right - newWidth;
      SaveWindowConfig();
    }

    private void InitAnimations() {
      // Working Pulse (Yellow/Orange)
      var pulseAnim = new ColorAnimation {
        From = Color.FromRgb(255, 190, 40),
        To = Color.FromRgb(120, 80, 10),
        Duration = TimeSpan.FromSeconds(0.9),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever
      };
      _pulseStoryboard = new Storyboard();
      _pulseStoryboard.Children.Add(pulseAnim);
      Storyboard.SetTarget(_pulseStoryboard, _pulseDot);
      Storyboard.SetTargetProperty(_pulseStoryboard, new PropertyPath("Fill.(SolidColorBrush.Color)"));

      // Waiting Attention Flash (Cyan/Blue to Red/Pink)
      var waitingAnim = new ColorAnimation {
        From = Color.FromRgb(255, 60, 90),
        To = Color.FromRgb(0, 180, 255),
        Duration = TimeSpan.FromSeconds(0.5),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever
      };
      _waitingStoryboard = new Storyboard();
      _waitingStoryboard.Children.Add(waitingAnim);
      Storyboard.SetTarget(_waitingStoryboard, _pulseDot);
      Storyboard.SetTargetProperty(_waitingStoryboard, new PropertyPath("Fill.(SolidColorBrush.Color)"));
    }

    private void PositionAtBottomRight() {
      UpdateLayout();
      var workingArea = SystemParameters.WorkArea;
      Left = workingArea.Right - Width - 10;
      Top = workingArea.Bottom - ActualHeight - 10;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
      base.OnRenderSizeChanged(sizeInfo);
      if (!_config.CustomPositioned) {
        var workingArea = SystemParameters.WorkArea;
        Left = workingArea.Right - Width - 10;
        Top = workingArea.Bottom - ActualHeight - 10;
      }
    }

    private void ToggleCollapse() {
      _isCollapsed = !_isCollapsed;
      _appLabel.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
      _smallerBtn.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
      _widerBtn.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
      _separator.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
      _itemsPanel.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
      _connectionStatusText.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
      _collapseBtn.Content = _isCollapsed ? "+" : "—";
      _collapseBtn.ToolTip = _isCollapsed ? "Развернуть список" : "Свернуть список";

      double bottomBefore = Top + ActualHeight;
      UpdateLayout();

      if (!_config.CustomPositioned) {
        var workingArea = SystemParameters.WorkArea;
        Left = workingArea.Right - Width - 10;
        Top = workingArea.Bottom - ActualHeight - 10;
      } else {
        Top = bottomBefore - ActualHeight;
      }
    }

    private void LoadServiceConfig() {
      try {
        string cfgPath = System.IO.Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          @"ru.ohmylama.ohmycode\config\opencode\service-prod.json"
        );
        if (File.Exists(cfgPath)) {
          var cfg = _serializer.Deserialize<ServiceConfig>(File.ReadAllText(cfgPath, Encoding.UTF8));
          _port = cfg.port;
          string cred = "opencode:" + cfg.password;
          _authHeader = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(cred));
        }
      } catch (Exception ex) {
        _summaryText.Text = "Ошибка чтения конфига: " + ex.Message;
      }
    }

    private void StartPolling() {
      _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
      _pollTimer.Tick += (s, e) => RefreshStatus();
      _pollTimer.Start();
      RefreshStatus();
    }

    private void RefreshStatus() {
      if (_port <= 0 || string.IsNullOrEmpty(_authHeader)) {
        LoadServiceConfig();
        if (_port <= 0) return;
      }

      ThreadPool.QueueUserWorkItem(_ => {
        try {
          string sessionsUrl = string.Format("http://127.0.0.1:{0}/api/session?limit=40", _port);
          string activeUrl = string.Format("http://127.0.0.1:{0}/api/session/active", _port);

          string sessionsJson = HttpGet(sessionsUrl);
          string activeJson = HttpGet(activeUrl);

          try {
            string projectsUrl = string.Format("http://127.0.0.1:{0}/api/project", _port);
            string projectsJson = HttpGet(projectsUrl);
            UpdateProjectIcons(projectsJson);
          } catch { }

          var sessionDataList = ParseSessions(sessionsJson, activeJson);

          Dispatcher.BeginInvoke(new Action(() => {
            try {
              UpdateUI(sessionDataList);
            } catch { }
          }));
        } catch (Exception ex) {
          Dispatcher.BeginInvoke(new Action(() => {
            _summaryText.Text = "Сервер недоступен (" + ex.Message + ")";
            _pulseDot.Fill = new SolidColorBrush(Color.FromRgb(150, 70, 70));
            _pulseStoryboard.Stop();
            _waitingStoryboard.Stop();
            SetPetState("idle");
          }));
        }
      });
    }

    private void UpdateProjectIcons(string projectsJson) {
      try {
        var root = _serializer.DeserializeObject(projectsJson) as IEnumerable;
        if (root == null) return;
        foreach (object item in root) {
          var p = item as IDictionary;
          if (p == null) continue;
          if (!p.Contains("canonical") || p["canonical"] == null) continue;
          string dir = p["canonical"].ToString();

          if (p.Contains("icon") && p["icon"] != null) {
            var iconDict = p["icon"] as IDictionary;
            if (iconDict != null) {
              if (iconDict.Contains("color") && iconDict["color"] != null) {
                _colorCache[dir] = ParseProjectColor(iconDict["color"].ToString());
              }
              if (iconDict.Contains("override") && iconDict["override"] != null && !_iconCache.ContainsKey(dir)) {
                string dataUri = iconDict["override"].ToString();
                int commaIdx = dataUri.IndexOf(',');
                if (commaIdx >= 0) {
                  byte[] bytes = Convert.FromBase64String(dataUri.Substring(commaIdx + 1));
                  Dispatcher.BeginInvoke(new Action(() => {
                    try {
                      var img = new BitmapImage();
                      img.BeginInit();
                      img.StreamSource = new MemoryStream(bytes);
                      img.CacheOption = BitmapCacheOption.OnLoad;
                      img.EndInit();
                      img.Freeze();
                      _iconCache[dir] = img;
                    } catch { }
                  }));
                }
              }
            }
          }
        }
      } catch { }
    }

    private Color ParseProjectColor(string hex) {
      try {
        if (string.IsNullOrEmpty(hex)) return Color.FromRgb(100, 110, 130);
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6) {
          byte r = Convert.ToByte(hex.Substring(0, 2), 16);
          byte g = Convert.ToByte(hex.Substring(2, 2), 16);
          byte b = Convert.ToByte(hex.Substring(4, 2), 16);
          return Color.FromRgb(r, g, b);
        }
      } catch { }
      return Color.FromRgb(100, 110, 130);
    }

    private string HttpGet(string url) {
      var req = (HttpWebRequest)WebRequest.Create(url);
      req.Method = "GET";
      req.Timeout = 1500;
      if (!string.IsNullOrEmpty(_authHeader)) {
        req.Headers["Authorization"] = _authHeader;
      }
      using (var resp = req.GetResponse())
      using (var stream = resp.GetResponseStream())
      using (var reader = new StreamReader(stream, Encoding.UTF8)) {
        return reader.ReadToEnd();
      }
    }

    private List<SessionData> ParseSessions(string sessionsJson, string activeJson) {
      var activeIds = new HashSet<string>();
      try {
        var activeObj = _serializer.DeserializeObject(activeJson);
        if (activeObj is IDictionary) {
          var dict = activeObj as IDictionary;
          if (dict.Contains("data")) {
            var data = dict["data"];
            if (data is IDictionary) {
              foreach (var k in ((IDictionary)data).Keys) {
                if (k != null) activeIds.Add(k.ToString());
              }
            } else if (data is IEnumerable) {
              foreach (var item in (IEnumerable)data) {
                if (item != null) activeIds.Add(item.ToString());
              }
            }
          }
        }
      } catch { }

      var allItems = new List<SessionData>();
      var parentToChildren = new Dictionary<string, List<string>>();
      var subagentSessions = new HashSet<string>();

      try {
        var rootObj = _serializer.DeserializeObject(sessionsJson);
        IEnumerable list = null;
        if (rootObj is IDictionary) {
          var d = rootObj as IDictionary;
          if (d.Contains("data")) list = d["data"] as IEnumerable;
        } else if (rootObj is IEnumerable) {
          list = rootObj as IEnumerable;
        }

        if (list != null) {
          foreach (object obj in list) {
            var dict = obj as IDictionary;
            if (dict == null) continue;

            string id = dict.Contains("id") && dict["id"] != null ? dict["id"].ToString() : "";
            string title = dict.Contains("title") && dict["title"] != null ? dict["title"].ToString() : "Без названия";
            string parentID = dict.Contains("parentID") && dict["parentID"] != null ? dict["parentID"].ToString() : null;

            string dir = "";
            if (dict.Contains("location") && dict["location"] is IDictionary) {
              var loc = dict["location"] as IDictionary;
              if (loc.Contains("directory") && loc["directory"] != null) {
                dir = loc["directory"].ToString();
              }
            }
            if (string.IsNullOrEmpty(dir) && dict.Contains("directory") && dict["directory"] != null) {
              dir = dict["directory"].ToString();
            }

            if (!string.IsNullOrEmpty(parentID)) {
              subagentSessions.Add(id);
              if (!parentToChildren.ContainsKey(parentID)) {
                parentToChildren[parentID] = new List<string>();
              }
              parentToChildren[parentID].Add(id);
            }

            ImageSource cachedIcon = null;
            if (!string.IsNullOrEmpty(dir) && _iconCache.ContainsKey(dir)) {
              cachedIcon = _iconCache[dir];
            }

            Color projColor = Color.FromRgb(100, 110, 130);
            if (!string.IsNullOrEmpty(dir) && _colorCache.ContainsKey(dir)) {
              projColor = _colorCache[dir];
            }

            allItems.Add(new SessionData {
              SessionId = id,
              Title = title,
              Directory = dir,
              Status = "idle",
              ProjectIcon = cachedIcon,
              ProjectColor = projColor
            });
          }
        }
      } catch { }

      var rootSessions = allItems.Where(s => !subagentSessions.Contains(s.SessionId)).ToList();
      var waitingIds = CheckWaitingSessions(activeIds);

      foreach (var session in rootSessions) {
        bool isDirectWaiting = waitingIds.Contains(session.SessionId);
        bool isDirectActive = activeIds.Contains(session.SessionId);

        bool hasChildWaiting = false;
        bool hasChildActive = false;

        if (parentToChildren.ContainsKey(session.SessionId)) {
          foreach (var cid in parentToChildren[session.SessionId]) {
            if (waitingIds.Contains(cid)) hasChildWaiting = true;
            if (activeIds.Contains(cid)) hasChildActive = true;
          }
        }

        if (isDirectWaiting || hasChildWaiting) {
          session.Status = "waiting";
        } else if (isDirectActive || hasChildActive) {
          session.Status = "working";
        } else {
          session.Status = "idle";
        }
      }

      return rootSessions
        .OrderBy(s => s.Status == "waiting" ? 0 : (s.Status == "working" ? 1 : 2))
        .Take(5)
        .ToList();
    }

    private HashSet<string> CheckWaitingSessions(HashSet<string> activeIds) {
      var waiting = new HashSet<string>();
      if (activeIds.Count == 0) return waiting;

      foreach (var sid in activeIds) {
        try {
          string formUrl = string.Format("http://127.0.0.1:{0}/api/session/{1}/form", _port, sid);
          string permUrl = string.Format("http://127.0.0.1:{0}/api/session/{1}/permission", _port, sid);

          string fJson = HttpGet(formUrl);
          string pJson = HttpGet(permUrl);

          if (fJson.Contains("\"data\":[") && !fJson.Contains("\"data\":[]")) {
            waiting.Add(sid);
            continue;
          }
          if (pJson.Contains("\"data\":[") && !pJson.Contains("\"data\":[]")) {
            waiting.Add(sid);
            continue;
          }
        } catch { }
      }
      return waiting;
    }

    private void UpdateUI(List<SessionData> items) {
      int waitingCount = items.Count(x => x.Status == "waiting");
      int workingCount = items.Count(x => x.Status == "working");
      int idleCount = items.Count(x => x.Status == "idle");

      // Update pet state & header summary & pulse
      if (waitingCount > 0) {
        SetPetState("waiting");
        _summaryText.Text = string.Format("{0} {1} ответа!", waitingCount, waitingCount == 1 ? "сессия требует" : "сессии требуют");
        _summaryText.Foreground = new SolidColorBrush(Color.FromRgb(255, 110, 130));
        _pulseStoryboard.Stop();
        _waitingStoryboard.Begin();
      } else if (workingCount > 0) {
        SetPetState("working");
        _summaryText.Text = string.Format("{0} {1} прямо сейчас", workingCount, workingCount == 1 ? "сессия работает" : "сессии работают");
        _summaryText.Foreground = new SolidColorBrush(Color.FromRgb(255, 210, 80));
        _waitingStoryboard.Stop();
        _pulseStoryboard.Begin();
      } else {
        SetPetState("idle");
        _summaryText.Text = string.Format("Все сессии завершены ({0})", items.Count);
        _summaryText.Foreground = new SolidColorBrush(Color.FromRgb(140, 220, 160));
        _pulseStoryboard.Stop();
        _waitingStoryboard.Stop();
        _pulseDot.Fill = new SolidColorBrush(Color.FromRgb(80, 215, 120));
      }

      _itemsPanel.Children.Clear();

      if (items.Count == 0) {
        var emptyText = new TextBlock {
          Text = "Нет открытых сессий",
          FontSize = 11,
          Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 160)),
          Margin = new Thickness(0, 6, 0, 6),
          HorizontalAlignment = HorizontalAlignment.Center
        };
        _itemsPanel.Children.Add(emptyText);
      } else {
        foreach (var item in items) {
          _itemsPanel.Children.Add(CreateItemView(item));
        }
      }

      UpdateLayout();

      if (!_config.CustomPositioned) {
        var workingArea = SystemParameters.WorkArea;
        Left = workingArea.Right - Width - 10;
        Top = workingArea.Bottom - ActualHeight - 10;
      }
    }

    private UIElement CreateItemView(SessionData item) {
      Brush statusBrush;
      Brush statusBgBrush;

      if (item.Status == "waiting") {
        statusBrush = new SolidColorBrush(Color.FromRgb(255, 75, 105));
        statusBgBrush = new SolidColorBrush(Color.FromArgb(50, 255, 60, 90));
      } else if (item.Status == "working") {
        statusBrush = new SolidColorBrush(Color.FromRgb(255, 195, 45));
        statusBgBrush = new SolidColorBrush(Color.FromArgb(40, 255, 195, 45));
      } else {
        statusBrush = new SolidColorBrush(Color.FromRgb(80, 215, 120));
        statusBgBrush = new SolidColorBrush(Color.FromArgb(28, 80, 215, 120));
      }

      var card = new Border {
        Background = statusBgBrush,
        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(7, 5, 7, 5),
        Margin = new Thickness(0, 1.5, 0, 2.5),
        Height = 44,
        Cursor = Cursors.Hand
      };

      card.MouseEnter += (s, e) => {
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
      };
      card.MouseLeave += (s, e) => {
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
      };
      card.MouseLeftButtonUp += (s, e) => {
        FocusOhMyCode();
      };

      var grid = new Grid();
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Project icon
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Titles
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Status Pill

      // Project icon or badge
      UIElement iconElem;
      if (item.ProjectIcon != null) {
        var img = new Image {
          Source = item.ProjectIcon,
          Width = 18,
          Height = 18,
          VerticalAlignment = VerticalAlignment.Center,
          Margin = new Thickness(1, 0, 7, 0)
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        iconElem = img;
      } else {
        // Fallback letter badge with project color
        var badgeBorder = new Border {
          Width = 18,
          Height = 18,
          CornerRadius = new CornerRadius(4),
          Background = new SolidColorBrush(item.ProjectColor),
          VerticalAlignment = VerticalAlignment.Center,
          Margin = new Thickness(1, 0, 7, 0)
        };
        string folderName = GetProjectShortName(item.Directory);
        string letter = string.IsNullOrEmpty(folderName) ? "O" : folderName.Substring(0, 1).ToUpper();
        badgeBorder.Child = new TextBlock {
          Text = letter,
          FontSize = 10,
          FontWeight = FontWeights.Bold,
          Foreground = Brushes.White,
          HorizontalAlignment = HorizontalAlignment.Center,
          VerticalAlignment = VerticalAlignment.Center
        };
        iconElem = badgeBorder;
      }
      Grid.SetColumn(iconElem, 0);
      grid.Children.Add(iconElem);

      var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

      var titleText = new TextBlock {
        Text = item.Title,
        FontSize = 11.5,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        TextTrimming = TextTrimming.CharacterEllipsis
      };
      textStack.Children.Add(titleText);

      string subTitle = GetProjectShortName(item.Directory);
      if (!string.IsNullOrEmpty(subTitle)) {
        var pathText = new TextBlock {
          Text = subTitle,
          FontSize = 9.5,
          Foreground = new SolidColorBrush(Color.FromArgb(170, 200, 210, 230)),
          Margin = new Thickness(0, 1, 0, 0),
          TextTrimming = TextTrimming.CharacterEllipsis
        };
        textStack.Children.Add(pathText);
      }

      Grid.SetColumn(textStack, 1);
      grid.Children.Add(textStack);

      // Status pill badge
      var badge = new Border {
        Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(5, 2, 5, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 0, 0, 0)
      };

      var badgeStack = new StackPanel { Orientation = Orientation.Horizontal };
      string label = item.Status == "waiting" ? "● Требует ответа" : (item.Status == "working" ? "● В работе..." : "✓ Завершено");

      var badgeText = new TextBlock {
        Text = label,
        FontSize = 9.5,
        FontWeight = FontWeights.Medium,
        Foreground = statusBrush,
        VerticalAlignment = VerticalAlignment.Center
      };
      badgeStack.Children.Add(badgeText);

      badge.Child = badgeStack;
      Grid.SetColumn(badge, 2);
      grid.Children.Add(badge);

      card.Child = grid;
      return card;
    }

    private string GetProjectShortName(string dir) {
      if (string.IsNullOrEmpty(dir)) return "";
      try {
        string name = System.IO.Path.GetFileName(dir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? dir : name;
      } catch {
        return dir;
      }
    }

    private void FocusOhMyCode() {
      try {
        var proc = Process.GetProcessesByName("OhMyCode").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        if (proc != null) {
          IntPtr hWnd = proc.MainWindowHandle;
          if (IsIconic(hWnd)) {
            ShowWindowAsync(hWnd, SW_RESTORE);
          } else {
            ShowWindowAsync(hWnd, SW_SHOW);
          }
          SetForegroundWindow(hWnd);
        }
      } catch { }
    }
  }

  public static class Program {
    private static Mutex _mutex;

    [STAThread]
    public static void Main() {
      bool createdNew;
      _mutex = new Mutex(true, "OhMyCodeStatusIndicator_SingleInstanceMutex", out createdNew);
      if (!createdNew) {
        return;
      }

      var app = new Application();
      app.Run(new MainWindow());
    }
  }
}

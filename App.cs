using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace MiniCalc
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new CalcWindow());
        }
    }

    sealed class CalcWindow : Window
    {
        const int DwmwaWindowCornerPreference = 33;
        const int DwmwcpRound = 2;
        const int DwmwaBorderColor = 34;
        const int WmNcHitTest = 0x0084;
        const int HtLeft = 10;
        const int HtRight = 11;
        const int HtTop = 12;
        const int HtTopLeft = 13;
        const int HtTopRight = 14;
        const int HtBottom = 15;
        const int HtBottomLeft = 16;
        const int HtBottomRight = 17;
        const double ResizeGrip = 8;
        const double InitialWidth = 312;
        const double InitialHeight = 588;

        readonly CalcEngine _eng = new CalcEngine();
        readonly Dictionary<string, RoundButton> _ops = new Dictionary<string, RoundButton>();
        readonly List<RoundButton> _keys = new List<RoundButton>();
        readonly TextBlock _expr;
        readonly TextBlock _value;
        TextBlock _title;
        Border _card;
        RoundButton _minBtn;
        RoundButton _closeBtn;
        RoundButton _themeBtn;
        RoundButton _historyBtn;
        RoundButton _restoreBtn;
        RoundButton _clearHistBtn;
        Grid _keysGrid;
        Border _historyPanel;
        StackPanel _historyList;
        ScrollViewer _historyScroll;
        TextBlock _historyCaption;
        readonly List<string> _history = new List<string>();
        bool _historyOpen;
        Theme _colors;
        bool _dark;
        IntPtr _hwnd;
        DispatcherTimer _toastTimer;

        public CalcWindow()
        {
            _dark = ThemeStore.LoadDark();
            _colors = _dark ? Theme.Dark : Theme.Light;

            Title = "计算器";
            Width = InitialWidth;
            Height = InitialHeight;
            MinWidth = 260;
            MinHeight = 470;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = _colors.Window;
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Microsoft YaHei");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 48,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(8),
                UseAeroCaptionButtons = false,
                NonClientFrameEdges = NonClientFrameEdges.None
            });

            foreach (string line in HistoryStore.Load())
                _history.Add(line);

            var root = new Grid { Margin = new Thickness(16, 10, 16, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildTitle());
            root.Children.Add(BuildDisplay(out _expr, out _value));
            var tools = BuildTools();
            Grid.SetRow(tools, 2);
            root.Children.Add(tools);
            var body = BuildBody();
            Grid.SetRow(body, 3);
            root.Children.Add(body);

            Content = root;
            StateChanged += delegate { UpdateRestoreState(); };
            Refresh();
            UpdateRestoreState();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateRestoreState();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            int preference = DwmwcpRound;
            DwmSetWindowAttribute(_hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
            ApplyBorder();
            ((HwndSource)PresentationSource.FromVisual(this)).AddHook(ResizeHitTest);
        }

        IntPtr ResizeHitTest(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WmNcHitTest || WindowState == WindowState.Maximized)
                return IntPtr.Zero;

            long packed = lParam.ToInt64();
            int sx = unchecked((short)(packed & 0xFFFF));
            int sy = unchecked((short)((packed >> 16) & 0xFFFF));
            Point local;
            try
            {
                local = PointFromScreen(new Point(sx, sy));
            }
            catch (InvalidOperationException)
            {
                return IntPtr.Zero;
            }

            double w = ActualWidth;
            double h = ActualHeight;
            bool left = local.X <= ResizeGrip;
            bool right = local.X >= w - ResizeGrip;
            bool top = local.Y <= ResizeGrip;
            bool bottom = local.Y >= h - ResizeGrip;
            int hit = 0;
            if (top && left) hit = HtTopLeft;
            else if (top && right) hit = HtTopRight;
            else if (bottom && left) hit = HtBottomLeft;
            else if (bottom && right) hit = HtBottomRight;
            else if (left) hit = HtLeft;
            else if (right) hit = HtRight;
            else if (top) hit = HtTop;
            else if (bottom) hit = HtBottom;
            if (hit == 0) return IntPtr.Zero;

            handled = true;
            return new IntPtr(hit);
        }

        void ToggleTheme()
        {
            _dark = !_dark;
            _colors = _dark ? Theme.Dark : Theme.Light;
            ApplyTheme();
            ThemeStore.Save(_dark);
        }

        void ApplyBorder()
        {
            if (_hwnd == IntPtr.Zero) return;
            int border = _colors.BorderColor;
            DwmSetWindowAttribute(_hwnd, DwmwaBorderColor, ref border, sizeof(int));
        }

        void ApplyTheme()
        {
            Background = _colors.Window;
            _title.Foreground = _colors.Title;
            _card.Background = _colors.Display;
            _card.BorderBrush = _colors.Line;
            _expr.Foreground = _colors.Muted;
            _value.Foreground = _colors.Ink;

            _themeBtn.Recolor(_colors.ThemeBg, _colors.ThemeHover, _colors.ThemePress, Brushes.Transparent, _colors.Ink);
            _minBtn.Recolor(_colors.MinBg, _colors.MinHover, _colors.MinPress, Brushes.Transparent, _colors.Ink);
            _closeBtn.Recolor(_colors.CloseBg, _colors.CloseHover, _colors.ClosePress, Brushes.Transparent, _colors.Ink);
            _themeBtn.Content = _dark ? "\uE706" : "\uE708";
            _themeBtn.ToolTip = _dark ? "切换浅色" : "切换深色";
            AutomationProperties.SetName(_themeBtn, _dark ? "浅色" : "深色");
            PaintHistoryButton();
            _restoreBtn.Recolor(_colors.Fn, _colors.FnHover, _colors.FnPress, _colors.FnLine, _colors.FnInk);
            _clearHistBtn.Recolor(_colors.Fn, _colors.FnHover, _colors.FnPress, _colors.FnLine, _colors.FnInk);
            _historyPanel.Background = _colors.Window;
            _historyScroll.Background = _colors.Window;
            _historyCaption.Foreground = _colors.Muted;
            if (_historyOpen) RebuildHistory();

            foreach (RoundButton key in _keys)
            {
                Face face = FaceFor((string)key.Tag);
                key.Recolor(face.Normal, face.Hover, face.Press, face.Border, face.Ink);
            }

            ApplyBorder();
        }

        Face FaceFor(string kind)
        {
            Theme t = _colors;
            if (kind == "op")
                return new Face(t.Op, t.OpHover, t.OpPress, t.OpLine, t.OpInk);
            if (kind == "eq")
                return new Face(t.Eq, t.EqHover, t.EqPress, t.EqLine, t.EqInk);
            if (kind == "fn")
                return new Face(t.Fn, t.FnHover, t.FnPress, t.FnLine, t.FnInk);
            return new Face(t.Num, t.NumHover, t.NumPress, t.NumLine, t.Ink);
        }

        UIElement BuildTitle()
        {
            var bar = new Grid { Height = 36 };
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _title = new TextBlock
            {
                Text = "计算器",
                FontSize = 13,
                Foreground = _colors.Title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            bar.Children.Add(_title);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            _restoreBtn = ToolButton("还原", RestoreInitialSize, "k-restore");
            _restoreBtn.ToolTip = "还原初始尺寸";
            _restoreBtn.Margin = new Thickness(0, 0, 2, 0);
            WindowChrome.SetIsHitTestVisibleInChrome(_restoreBtn, true);

            _themeBtn = ChromeButton(
                _dark ? "\uE706" : "\uE708",
                ToggleTheme,
                _colors.ThemeBg,
                _colors.ThemeHover,
                _colors.ThemePress,
                "Segoe MDL2 Assets");
            _themeBtn.FontWeight = FontWeights.Normal;
            _themeBtn.FontSize = 15;
            _themeBtn.ToolTip = _dark ? "切换浅色" : "切换深色";
            AutomationProperties.SetAutomationId(_themeBtn, "k-theme");
            AutomationProperties.SetName(_themeBtn, _dark ? "浅色" : "深色");

            _minBtn = ChromeButton("−", delegate { WindowState = WindowState.Minimized; }, _colors.MinBg, _colors.MinHover, _colors.MinPress, null);
            _closeBtn = ChromeButton("×", delegate { Close(); }, _colors.CloseBg, _colors.CloseHover, _colors.ClosePress, null);
            actions.Children.Add(_restoreBtn);
            actions.Children.Add(_themeBtn);
            actions.Children.Add(_minBtn);
            actions.Children.Add(_closeBtn);
            Grid.SetColumn(actions, 1);
            bar.Children.Add(actions);
            return bar;
        }

        RoundButton ChromeButton(string glyph, Action click, Brush normal, Brush hover, Brush press, string family)
        {
            var btn = new RoundButton(
                glyph, 14, 15, new Thickness(4, 0, 0, 0),
                normal, hover, press,
                Brushes.Transparent, _colors.Ink, 0);
            btn.Width = 30;
            btn.Height = 30;
            if (family != null)
                btn.FontFamily = new FontFamily(family);
            btn.Click += delegate { click(); };
            WindowChrome.SetIsHitTestVisibleInChrome(btn, true);
            return btn;
        }

        UIElement BuildDisplay(out TextBlock expr, out TextBlock value)
        {
            _card = new Border
            {
                Background = _colors.Display,
                BorderBrush = _colors.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16, 12, 16, 12),
                Height = 116,
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(_card, 1);

            var stack = new Grid();
            stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            expr = new TextBlock
            {
                FontSize = 13,
                Foreground = _colors.Muted,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            value = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Light, Segoe UI"),
                FontSize = 42,
                Foreground = _colors.Ink,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            AutomationProperties.SetAutomationId(value, "display");

            stack.Children.Add(expr);
            Grid.SetRow(value, 1);
            stack.Children.Add(value);
            _card.Child = stack;

            _card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                if (args.ClickCount == 2)
                {
                    CopyDisplay();
                    args.Handled = true;
                }
            };
            return _card;
        }

        UIElement BuildTools()
        {
            var bar = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            _historyBtn = ToolButton("历史", delegate { ShowHistory(!_historyOpen); }, "k-history");
            _historyBtn.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Children.Add(_historyBtn);
            return bar;
        }

        RoundButton ToolButton(string text, Action click, string autoId)
        {
            var btn = new RoundButton(text, 13, 14, new Thickness(0), _colors.Fn, _colors.FnHover, _colors.FnPress, _colors.FnLine, _colors.FnInk, 1);
            btn.Height = 32;
            btn.MinWidth = 72;
            btn.Click += delegate { click(); };
            AutomationProperties.SetAutomationId(btn, autoId);
            AutomationProperties.SetName(btn, text);
            return btn;
        }

        UIElement BuildBody()
        {
            _keysGrid = (Grid)BuildKeys();
            _historyPanel = BuildHistoryPanel();
            var body = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            body.Children.Add(_keysGrid);
            body.Children.Add(_historyPanel);
            return body;
        }

        Border BuildHistoryPanel()
        {
            var panel = new Border
            {
                Background = _colors.Window,
                Visibility = Visibility.Collapsed
            };
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Grid { Margin = new Thickness(2, 0, 2, 8) };
            _historyCaption = new TextBlock
            {
                Text = "最近计算",
                Foreground = _colors.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            header.Children.Add(_historyCaption);
            _clearHistBtn = ToolButton("清空", ClearHistory, "k-clear-history");
            _clearHistBtn.Height = 28;
            _clearHistBtn.MinWidth = 60;
            _clearHistBtn.HorizontalAlignment = HorizontalAlignment.Right;
            header.Children.Add(_clearHistBtn);
            layout.Children.Add(header);

            _historyList = new StackPanel();
            _historyScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = _colors.Window,
                Content = _historyList
            };
            Grid.SetRow(_historyScroll, 1);
            layout.Children.Add(_historyScroll);
            panel.Child = layout;
            return panel;
        }

        void ShowHistory(bool open)
        {
            _historyOpen = open;
            _keysGrid.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            _historyPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            PaintHistoryButton();
            if (open) RebuildHistory();
        }

        void PaintHistoryButton()
        {
            if (_historyBtn == null) return;
            if (_historyOpen)
                _historyBtn.Recolor(_colors.Op, _colors.OpHover, _colors.OpPress, _colors.OpLine, _colors.OpInk);
            else
                _historyBtn.Recolor(_colors.Fn, _colors.FnHover, _colors.FnPress, _colors.FnLine, _colors.FnInk);
        }

        void RebuildHistory()
        {
            _historyList.Children.Clear();
            _clearHistBtn.IsEnabled = _history.Count > 0;
            _clearHistBtn.Opacity = _history.Count > 0 ? 1 : 0.4;
            if (_history.Count == 0)
            {
                _historyList.Children.Add(new TextBlock
                {
                    Text = "还没有记录",
                    Foreground = _colors.Muted,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 28, 0, 0),
                    FontSize = 14
                });
                return;
            }

            for (int i = 0; i < _history.Count; i++)
            {
                string line = _history[i];
                var item = new Button
                {
                    Content = line,
                    OverridesDefaultStyle = true,
                    Focusable = false,
                    FocusVisualStyle = null,
                    Background = _colors.Num,
                    Foreground = _colors.Ink,
                    BorderBrush = _colors.NumLine,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Cursor = Cursors.Hand,
                    FontSize = 14
                };
                var shell = new FrameworkElementFactory(typeof(Border));
                shell.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
                shell.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                shell.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                shell.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                shell.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
                var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
                shell.AppendChild(presenter);
                item.Template = new ControlTemplate(typeof(Button)) { VisualTree = shell };
                AutomationProperties.SetAutomationId(item, "hist-" + i);
                AutomationProperties.SetName(item, line);
                string captured = line;
                item.Click += delegate { Recall(captured); };
                _historyList.Children.Add(item);
            }
        }

        void Recall(string line)
        {
            int eq = line.LastIndexOf('=');
            string result = eq >= 0 ? line.Substring(eq + 1).Trim() : line;
            _eng.TryPaste(result);
            ShowHistory(false);
            Refresh();
        }

        void ClearHistory()
        {
            _history.Clear();
            HistoryStore.Save(_history);
            RebuildHistory();
        }

        void Remember()
        {
            string line = _eng.TakeRecord();
            if (string.IsNullOrEmpty(line)) return;
            _history.Insert(0, line);
            if (_history.Count > 30) _history.RemoveAt(_history.Count - 1);
            HistoryStore.Save(_history);
            if (_historyOpen) RebuildHistory();
        }

        void RestoreInitialSize()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            Width = InitialWidth;
            Height = InitialHeight;
            UpdateRestoreState();
        }

        void UpdateRestoreState()
        {
            if (_restoreBtn == null) return;
            bool changed = WindowState == WindowState.Maximized
                || Math.Abs(Width - InitialWidth) > 1.5
                || Math.Abs(Height - InitialHeight) > 1.5;
            _restoreBtn.IsEnabled = changed;
            _restoreBtn.Opacity = changed ? 1 : 0.4;
        }

        UIElement BuildKeys()
        {
            var grid = new Grid();
            for (int c = 0; c < 4; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int r = 0; r < 5; r++)
                grid.RowDefinitions.Add(new RowDefinition());

            string[,] labels =
            {
                { "C", "\uE72B", "%", "÷" },
                { "7", "8", "9", "×" },
                { "4", "5", "6", "−" },
                { "1", "2", "3", "+" },
                { "±", "0", ".", "=" }
            };
            string[,] actions =
            {
                { "c", "b", "p", "/" },
                { "7", "8", "9", "*" },
                { "4", "5", "6", "-" },
                { "1", "2", "3", "+" },
                { "s", "0", ".", "=" }
            };
            string[,] kinds =
            {
                { "fn", "fn", "fn", "op" },
                { "num", "num", "num", "op" },
                { "num", "num", "num", "op" },
                { "num", "num", "num", "op" },
                { "fn", "num", "num", "eq" }
            };

            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    string action = actions[r, c];
                    string kind = kinds[r, c];
                    var btn = MakeKey(labels[r, c], kind);
                    AutomationProperties.SetAutomationId(btn, "k-" + action);
                    AutomationProperties.SetName(btn, action == "b" ? "退格" : labels[r, c]);
                    string captured = action;
                    btn.Click += delegate { Act(captured); };
                    btn.Tag = kind;
                    _keys.Add(btn);
                    if (kind == "op") _ops[action] = btn;
                    Grid.SetRow(btn, r);
                    Grid.SetColumn(btn, c);
                    grid.Children.Add(btn);
                }
            }

            return grid;
        }

        RoundButton MakeKey(string label, string kind)
        {
            Face face = FaceFor(kind);
            Brush normal = face.Normal, hover = face.Hover, press = face.Press, border = face.Border, ink = face.Ink;
            double size = 22;
            string family = "Segoe UI";
            if (kind == "op" || kind == "eq") size = 24;
            else if (kind == "fn")
            {
                size = 18;
                if (label.Length == 1 && label[0] == '\uE72B')
                {
                    family = "Segoe MDL2 Assets";
                    size = 16;
                }
            }

            var btn = new RoundButton(label, size, 16, new Thickness(4), normal, hover, press, border, ink, 1);
            btn.FontFamily = new FontFamily(family);
            if (label.Length == 1 && label[0] == '\uE72B')
                btn.FontWeight = FontWeights.Normal;
            return btn;
        }

        void Act(string action)
        {
            switch (action)
            {
                case "c": _eng.Clear(); break;
                case "b": _eng.Backspace(); break;
                case "p": _eng.Percent(); break;
                case "s": _eng.ToggleSign(); break;
                case ".": _eng.InputDot(); break;
                case "=": _eng.Evaluate(); break;
                case "+":
                case "-":
                case "*":
                case "/":
                    _eng.SetOperator(action);
                    break;
                default:
                    if (action.Length == 1 && action[0] >= '0' && action[0] <= '9')
                        _eng.InputDigit(action[0]);
                    break;
            }
            Refresh();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            ModifierKeys mods = Keyboard.Modifiers;
            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.C) { CopyDisplay(); e.Handled = true; return; }
                if (e.Key == Key.V) { Paste(); e.Handled = true; return; }
                base.OnPreviewKeyDown(e);
                return;
            }

            bool shift = (mods & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool used = true;

            if (!shift && e.Key >= Key.D0 && e.Key <= Key.D9)
                _eng.InputDigit((char)('0' + (e.Key - Key.D0)));
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                _eng.InputDigit((char)('0' + (e.Key - Key.NumPad0)));
            else if (e.Key == Key.Decimal || e.Key == Key.OemPeriod || (!shift && e.Key == Key.OemComma))
                _eng.InputDot();
            else if (e.Key == Key.Add || (shift && e.Key == Key.OemPlus))
                _eng.SetOperator("+");
            else if (e.Key == Key.Subtract || (!shift && e.Key == Key.OemMinus))
                _eng.SetOperator("-");
            else if (e.Key == Key.Multiply || (shift && e.Key == Key.D8))
                _eng.SetOperator("*");
            else if (e.Key == Key.Divide || (!shift && e.Key == Key.Oem2))
                _eng.SetOperator("/");
            else if (e.Key == Key.Enter || (!shift && e.Key == Key.OemPlus))
                _eng.Evaluate();
            else if (shift && e.Key == Key.D5)
                _eng.Percent();
            else if (e.Key == Key.Back)
                _eng.Backspace();
            else if (e.Key == Key.Escape || e.Key == Key.Delete)
                _eng.Clear();
            else
                used = false;

            if (!used)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            e.Handled = true;
            Refresh();
        }

        void CopyDisplay()
        {
            if (_eng.HasError) return;
            try
            {
                Clipboard.SetText(_eng.DisplayText);
            }
            catch (COMException)
            {
                return;
            }

            Refresh();
            _expr.Text = "已复制";
            if (_toastTimer == null)
            {
                _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                _toastTimer.Tick += delegate
                {
                    _toastTimer.Stop();
                    Refresh();
                };
            }
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        void Paste()
        {
            string text = null;
            try
            {
                if (Clipboard.ContainsText()) text = Clipboard.GetText();
            }
            catch (COMException)
            {
                return;
            }
            if (_eng.TryPaste(text)) Refresh();
        }

        void Refresh()
        {
            if (_toastTimer != null) _toastTimer.Stop();
            Remember();

            string text = _eng.DisplayText;
            _value.Text = text;
            _expr.Text = _eng.Expression ?? "";
            AutomationProperties.SetName(_value, text);

            if (_eng.HasError)
            {
                _value.FontFamily = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");
                _value.FontSize = 26;
            }
            else
            {
                _value.FontFamily = new FontFamily("Segoe UI Light, Segoe UI");
                _value.FontSize = DisplaySize(text);
            }

            string active = _eng.ActiveOperator;
            foreach (var pair in _ops)
                pair.Value.SetActive(pair.Key == active);
        }

        static double DisplaySize(string text)
        {
            int n = text == null ? 0 : text.Length;
            if (n <= 7) return 42;
            if (n <= 9) return 36;
            if (n <= 12) return 30;
            if (n <= 15) return 24;
            return 20;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }

    sealed class RoundButton : Button
    {
        Brush _normal;
        Brush _hover;
        Brush _press;
        bool _active;

        public Brush HoverBrush
        {
            get { return _hover; }
            set { _hover = value; }
        }

        public Brush PressBrush
        {
            get { return _press; }
            set { _press = value; }
        }

        public RoundButton(
            string text, double fontSize, double radius, Thickness margin,
            Brush normal, Brush hover, Brush press, Brush border, Brush foreground, double borderThickness)
        {
            _normal = normal;
            _hover = hover;
            _press = press;

            Content = text;
            FontSize = fontSize;
            FontWeight = FontWeights.Medium;
            Foreground = foreground;
            Background = normal;
            BorderBrush = border;
            BorderThickness = new Thickness(borderThickness);
            Margin = margin;
            Cursor = Cursors.Hand;
            Focusable = false;
            FocusVisualStyle = null;
            OverridesDefaultStyle = true;
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;

            var shell = new FrameworkElementFactory(typeof(Border));
            shell.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            shell.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
            shell.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
            shell.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
            shell.SetValue(SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentProperty));
            presenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentTemplateProperty));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(TextElement.ForegroundProperty, new TemplateBindingExtension(ForegroundProperty));
            shell.AppendChild(presenter);

            Template = new ControlTemplate(typeof(Button)) { VisualTree = shell };

            MouseEnter += delegate { ApplyFace(); };
            MouseLeave += delegate { ApplyFace(); };
            PreviewMouseLeftButtonDown += delegate { Background = _press; };
            PreviewMouseLeftButtonUp += delegate { ApplyFace(); };
        }

        public void SetActive(bool active)
        {
            _active = active;
            ApplyFace();
        }

        public void Recolor(Brush normal, Brush hover, Brush press, Brush border, Brush foreground)
        {
            _normal = normal;
            _hover = hover;
            _press = press;
            BorderBrush = border;
            Foreground = foreground;
            ApplyFace();
        }

        void ApplyFace()
        {
            if (_active) Background = _press;
            else if (IsMouseOver) Background = _hover;
            else Background = _normal;
        }
    }

    struct Face
    {
        public readonly Brush Normal;
        public readonly Brush Hover;
        public readonly Brush Press;
        public readonly Brush Border;
        public readonly Brush Ink;

        public Face(Brush normal, Brush hover, Brush press, Brush border, Brush ink)
        {
            Normal = normal;
            Hover = hover;
            Press = press;
            Border = border;
            Ink = ink;
        }
    }

    sealed class Theme
    {
        public int BorderColor;
        public Brush Window, Display, Line, Ink, Muted, Title;
        public Brush Num, NumHover, NumPress, NumLine;
        public Brush Fn, FnHover, FnPress, FnLine, FnInk;
        public Brush Op, OpHover, OpPress, OpLine, OpInk;
        public Brush Eq, EqHover, EqPress, EqLine, EqInk;
        public Brush ThemeBg, ThemeHover, ThemePress;
        public Brush MinBg, MinHover, MinPress;
        public Brush CloseBg, CloseHover, ClosePress;

        public static readonly Theme Light = CreateLight();
        public static readonly Theme Dark = CreateDark();

        static Theme CreateLight()
        {
            return new Theme
            {
                BorderColor = 0x00B4BEC4,
                Window = B("#E6E3DC"),
                Display = B("#F4F2EC"),
                Line = B("#DDD8CF"),
                Ink = B("#2F2E2B"),
                Muted = B("#6E6A63"),
                Title = B("#6E6A63"),
                Num = B("#F7F6F2"),
                NumHover = B("#EEECE5"),
                NumPress = B("#E4E0D8"),
                NumLine = B("#E4E0D7"),
                Fn = B("#DDD7CE"),
                FnHover = B("#D2CCC2"),
                FnPress = B("#C6BFB3"),
                FnLine = B("#D4CEC4"),
                FnInk = B("#514E48"),
                Op = B("#CFC8BB"),
                OpHover = B("#C3BBAE"),
                OpPress = B("#B4AC9E"),
                OpLine = B("#C5BEB0"),
                OpInk = B("#3C3934"),
                Eq = B("#1877F2"),
                EqHover = B("#166BDB"),
                EqPress = B("#115FC2"),
                EqLine = B("#1464C8"),
                EqInk = B("#FFFFFF"),
                ThemeBg = B("#C3D0C6"),
                ThemeHover = B("#B0C0B4"),
                ThemePress = B("#9DB0A2"),
                MinBg = B("#E3D4B8"),
                MinHover = B("#D5C4A4"),
                MinPress = B("#C6B490"),
                CloseBg = B("#E8CDC8"),
                CloseHover = B("#DBB9B3"),
                ClosePress = B("#CDA59E")
            };
        }

        static Theme CreateDark()
        {
            return new Theme
            {
                BorderColor = 0x00383C3E,
                Window = B("#2A2926"),
                Display = B("#34332E"),
                Line = B("#45433C"),
                Ink = B("#F3F1EA"),
                Muted = B("#C4BFB6"),
                Title = B("#B7B2A8"),
                Num = B("#3C3B36"),
                NumHover = B("#474640"),
                NumPress = B("#525048"),
                NumLine = B("#4A4943"),
                Fn = B("#45433C"),
                FnHover = B("#504E46"),
                FnPress = B("#5C5950"),
                FnLine = B("#524F47"),
                FnInk = B("#E4DFD6"),
                Op = B("#524E45"),
                OpHover = B("#5E594F"),
                OpPress = B("#6A6458"),
                OpLine = B("#5C574E"),
                OpInk = B("#F3EFE6"),
                Eq = B("#1877F2"),
                EqHover = B("#166BDB"),
                EqPress = B("#115FC2"),
                EqLine = B("#1464C8"),
                EqInk = B("#FFFFFF"),
                ThemeBg = B("#5E6E64"),
                ThemeHover = B("#6E8074"),
                ThemePress = B("#4C5A52"),
                MinBg = B("#6E624F"),
                MinHover = B("#7E705A"),
                MinPress = B("#5A5040"),
                CloseBg = B("#73544E"),
                CloseHover = B("#84625B"),
                ClosePress = B("#5E443F")
            };
        }

        static Brush B(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }

    static class HistoryStore
    {
        static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "轻量计算器",
                    "history.txt");
            }
        }

        public static List<string> Load()
        {
            var list = new List<string>();
            try
            {
                if (!File.Exists(FilePath)) return list;
                string[] lines = File.ReadAllLines(FilePath);
                for (int i = 0; i < lines.Length && list.Count < 30; i++)
                {
                    if (lines[i].Trim().Length > 0) list.Add(lines[i]);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return list;
        }

        public static void Save(List<string> items)
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, items.ToArray());
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    static class ThemeStore
    {
        static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "轻量计算器",
                    "theme.txt");
            }
        }

        public static bool LoadDark()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string text = File.ReadAllText(FilePath).Trim().ToLowerInvariant();
                    if (text == "dark") return true;
                    if (text == "light") return false;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value is int mode) return mode == 0;
                }
            }
            catch (System.Security.SecurityException) { }
            catch (IOException) { }

            return false;
        }

        public static void Save(bool dark)
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, dark ? "dark" : "light");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

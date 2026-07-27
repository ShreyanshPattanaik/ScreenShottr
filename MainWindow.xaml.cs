using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.ComponentModel;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using IOPath = System.IO.Path;

namespace ShottrClone;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private enum AnnotationTool { None, Rectangle, Ellipse, Arrow, Line, Text, Freehand, Step, Highlight, Crop }

    private sealed class HistoryEntry(Action undo, Action redo, params IDisposable[] resources) : IDisposable
    {
        public void Undo() => undo();
        public void Redo() => redo();

        public void Dispose()
        {
            foreach (var resource in resources)
                resource.Dispose();
        }
    }

    private AnnotationTool _currentTool = AnnotationTool.None;
    private Shape? _currentShape;
    private Polyline? _currentPolyline;
    private System.Windows.Point _startPoint;
    private readonly List<UIElement> _annotations = [];
    private System.Windows.Shapes.Path? _currentArrow;
    private Ellipse? _currentEllipse;
    private int _stepCounter = 1;
    private Line? _currentLine;
    private UIElement? _selectedElement;
    private System.Windows.Point _elementPositionBeforeDrag;
    private UIElement? _selectorDownElement;
    private System.Windows.Point _selectorDownPoint;
    private bool _selectorDragStarted = false;
    private readonly Stack<HistoryEntry> _undoStack = new();
    private readonly Stack<HistoryEntry> _redoStack = new();
    private System.Windows.Shapes.Rectangle? _cropRectOverlay;
    private bool _isCropping;
    private System.Drawing.Rectangle? _lastCaptureRegion;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private HwndSource? _windowSource;
    private bool _allowClose;
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings;
    private CapturePreviewWindow? _previewWindow;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const int HOTKEY_AREA = 1;
    private const int HOTKEY_FULLSCREEN = 2;
    private const int HOTKEY_WINDOW = 3;
    private const int HOTKEY_REPEAT = 4;

    private void ResetToolButtons()
    {
        BtnRectTool.IsChecked = false;
        BtnEllipseTool.IsChecked = false;
        BtnArrowTool.IsChecked = false;
        BtnLineTool.IsChecked = false;
        BtnTextTool.IsChecked = false;
        BtnFreehandTool.IsChecked = false;
        BtnStepTool.IsChecked = false;
        BtnHighlightTool.IsChecked = false;
        BtnSelectorTool.IsChecked = false;
        BtnCropTool.IsChecked = false;
    }

    private void BtnRectTool_Click(object sender, RoutedEventArgs e)
    {
        ResetToolButtons();
        BtnRectTool.IsChecked = true;
        _currentTool = AnnotationTool.Rectangle;
    }
    private void BtnEllipseTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnEllipseTool.IsChecked = true; _currentTool = AnnotationTool.Ellipse; }
    private void BtnArrowTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnArrowTool.IsChecked = true; _currentTool = AnnotationTool.Arrow; }
    private void BtnLineTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnLineTool.IsChecked = true; _currentTool = AnnotationTool.Line; }
    private void BtnTextTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnTextTool.IsChecked = true; _currentTool = AnnotationTool.Text; }
    private void BtnFreehandTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnFreehandTool.IsChecked = true; _currentTool = AnnotationTool.Freehand; }
    private void BtnStepTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnStepTool.IsChecked = true; _currentTool = AnnotationTool.Step; }
    private void BtnHighlightTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnHighlightTool.IsChecked = true; _currentTool = AnnotationTool.Highlight; }
    private void BtnSelectorTool_Click(object sender, RoutedEventArgs e)
    {
        ResetToolButtons();
        BtnSelectorTool.IsChecked = true;
        _currentTool = AnnotationTool.None;
        DeselectElement();
    }
    private void BtnCropTool_Click(object sender, RoutedEventArgs e) { ResetToolButtons(); BtnCropTool.IsChecked = true; _currentTool = AnnotationTool.Crop; }
    // Removed Pixelate, Blur, and Erase tool logic and handlers.

    private void DeselectElement()
    {
        if (_selectedElement != null)
        {
            if (_selectedElement is Shape shape)
                shape.StrokeDashArray = null;
            _selectedElement = null;
        }
    }

    private void SelectElement(UIElement element)
    {
        DeselectElement();
        _selectedElement = element;
        if (element is Shape shape)
            shape.StrokeDashArray = new DoubleCollection { 2, 2 };
        AnnotationCanvas.Focus();
    }

    private void AnnotationCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool == AnnotationTool.None) // Selector tool
        {
            var pos = e.GetPosition(AnnotationCanvas);
            var hit = FindAnnotationElement(AnnotationCanvas.InputHitTest(pos) as DependencyObject);
            _selectorDownElement = hit;
            _selectorDownPoint = pos;
            _selectorDragStarted = false;
            if (hit != null && _annotations.Contains(hit))
            {
                SelectElement(hit);
                _elementPositionBeforeDrag = GetElementPosition(hit);
                AnnotationCanvas.CaptureMouse();
            }
            else
            {
                DeselectElement();
            }
            return;
        }
        if (_currentTool == AnnotationTool.Rectangle)
        {
            _startPoint = e.GetPosition(AnnotationCanvas);
            var rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.Transparent
            };
            System.Windows.Controls.Canvas.SetLeft(rect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(rect, _startPoint.Y);
            rect.Width = 0;
            rect.Height = 0;
            _currentShape = rect;
            AddAnnotation(rect);
            AnnotationCanvas.CaptureMouse();
        }
        else if (_currentTool == AnnotationTool.Ellipse)
        {
            _startPoint = e.GetPosition(AnnotationCanvas);
            var ellipse = new Ellipse
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.Transparent
            };
            System.Windows.Controls.Canvas.SetLeft(ellipse, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(ellipse, _startPoint.Y);
            ellipse.Width = 0;
            ellipse.Height = 0;
            _currentEllipse = ellipse;
            AddAnnotation(ellipse);
            AnnotationCanvas.CaptureMouse();
        }
        else if (_currentTool == AnnotationTool.Arrow)
        {
            _startPoint = e.GetPosition(AnnotationCanvas);
            var arrow = new System.Windows.Shapes.Path
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.Red
            };
            _currentArrow = arrow;
            UpdateArrowGeometry(arrow, _startPoint, _startPoint);
            AddAnnotation(arrow);
            AnnotationCanvas.CaptureMouse();
        }
        else if (_currentTool == AnnotationTool.Line)
        {
            _startPoint = e.GetPosition(AnnotationCanvas);
            var line = new Line
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                X1 = _startPoint.X,
                Y1 = _startPoint.Y,
                X2 = _startPoint.X,
                Y2 = _startPoint.Y
            };
            _currentLine = line;
            AddAnnotation(line);
            AnnotationCanvas.CaptureMouse();
        }
        else if (_currentTool == AnnotationTool.Text)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            var textBox = new TextBox
            {
                Text = "Text",
                FontSize = 18,
                Foreground = System.Windows.Media.Brushes.Black,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                MinWidth = 40,
                AcceptsReturn = false,
                Tag = "Text"
            };
            System.Windows.Controls.Canvas.SetLeft(textBox, pos.X);
            System.Windows.Controls.Canvas.SetTop(textBox, pos.Y);
            textBox.LostFocus += (_, _) => CommitTextEdit(textBox);
            textBox.KeyDown += (_, ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    CommitTextEdit(textBox);
                    AnnotationCanvas.Focus();
                    ev.Handled = true;
                }
            };
            textBox.MouseDoubleClick += (_, ev) =>
            {
                BeginTextEdit(textBox);
                ev.Handled = true;
            };
            AddAnnotation(textBox);
            textBox.Focus();
            textBox.SelectAll();
        }
        else if (_currentTool == AnnotationTool.Step)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            var marker = new Grid
            {
                Width = 32,
                Height = 32
            };
            marker.Children.Add(new Ellipse
            {
                Fill = System.Windows.Media.Brushes.Gold,
                Stroke = System.Windows.Media.Brushes.DarkGoldenrod,
                StrokeThickness = 2
            });
            var label = new TextBlock
            {
                Text = _stepCounter.ToString(),
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            marker.Children.Add(label);
            System.Windows.Controls.Canvas.SetLeft(marker, pos.X - 16);
            System.Windows.Controls.Canvas.SetTop(marker, pos.Y - 16);
            AddAnnotation(marker);
            _stepCounter++;
        }
        else if (_currentTool == AnnotationTool.Highlight)
        {
            _startPoint = e.GetPosition(AnnotationCanvas);
            var rect = new System.Windows.Shapes.Rectangle
            {
                // Stroke = System.Windows.Media.Brushes.Yellow, // Remove border
                // StrokeThickness = 2,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 80)) // More yellow, slightly less transparent
            };
            System.Windows.Controls.Canvas.SetLeft(rect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(rect, _startPoint.Y);
            rect.Width = 0;
            rect.Height = 0;
            _currentShape = rect;
            AddAnnotation(rect);
            AnnotationCanvas.CaptureMouse();
        }
        else if (_currentTool == AnnotationTool.Freehand)
        {
            _currentPolyline = new Polyline
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2
            };
            _currentPolyline.Points.Add(e.GetPosition(AnnotationCanvas));
            AddAnnotation(_currentPolyline);
            AnnotationCanvas.CaptureMouse();
        }
        else if (_currentTool == AnnotationTool.Crop)
        {
            // Remove previous crop overlay if it exists
            if (_cropRectOverlay != null)
            {
                AnnotationCanvas.Children.Remove(_cropRectOverlay);
                _cropRectOverlay = null;
            }
            _startPoint = e.GetPosition(AnnotationCanvas);
            _cropRectOverlay = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.Transparent
            };
            System.Windows.Controls.Canvas.SetLeft(_cropRectOverlay, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(_cropRectOverlay, _startPoint.Y);
            _cropRectOverlay.Width = 0;
            _cropRectOverlay.Height = 0;
            AnnotationCanvas.Children.Add(_cropRectOverlay);
            _isCropping = true;
            AnnotationCanvas.CaptureMouse();
        }
    }

    private void AnnotationCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_currentTool == AnnotationTool.None && _selectorDownElement != null)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double dx = pos.X - _selectorDownPoint.X;
            double dy = pos.Y - _selectorDownPoint.Y;
            if (!_selectorDragStarted && (Math.Abs(dx) > 3 || Math.Abs(dy) > 3))
            {
                _selectorDragStarted = true;
            }
            if (_selectorDragStarted && _selectedElement != null)
            {
                double left = System.Windows.Controls.Canvas.GetLeft(_selectedElement);
                double top = System.Windows.Controls.Canvas.GetTop(_selectedElement);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                System.Windows.Controls.Canvas.SetLeft(_selectedElement, left + dx);
                System.Windows.Controls.Canvas.SetTop(_selectedElement, top + dy);
                _selectorDownPoint = pos;
                e.Handled = true;
                return;
            }
        }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_currentTool == AnnotationTool.Rectangle && _currentShape is System.Windows.Shapes.Rectangle rect)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double x = Math.Min(pos.X, _startPoint.X);
            double y = Math.Min(pos.Y, _startPoint.Y);
            double w = Math.Abs(pos.X - _startPoint.X);
            double h = Math.Abs(pos.Y - _startPoint.Y);
            System.Windows.Controls.Canvas.SetLeft(rect, x);
            System.Windows.Controls.Canvas.SetTop(rect, y);
            rect.Width = w;
            rect.Height = h;
        }
        else if (_currentTool == AnnotationTool.Ellipse && _currentEllipse != null)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double x = Math.Min(pos.X, _startPoint.X);
            double y = Math.Min(pos.Y, _startPoint.Y);
            double w = Math.Abs(pos.X - _startPoint.X);
            double h = Math.Abs(pos.Y - _startPoint.Y);
            System.Windows.Controls.Canvas.SetLeft(_currentEllipse, x);
            System.Windows.Controls.Canvas.SetTop(_currentEllipse, y);
            _currentEllipse.Width = w;
            _currentEllipse.Height = h;
        }
        else if (_currentTool == AnnotationTool.Arrow && _currentArrow != null)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            UpdateArrowGeometry(_currentArrow, _startPoint, pos);
        }
        else if (_currentTool == AnnotationTool.Line && _currentLine != null)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            _currentLine.X2 = pos.X;
            _currentLine.Y2 = pos.Y;
        }
        else if (_currentTool == AnnotationTool.Freehand && _currentPolyline != null)
        {
            _currentPolyline.Points.Add(e.GetPosition(AnnotationCanvas));
        }
        else if (_currentTool == AnnotationTool.Highlight && _currentShape is System.Windows.Shapes.Rectangle hrect)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double x = Math.Min(pos.X, _startPoint.X);
            double y = Math.Min(pos.Y, _startPoint.Y);
            double w = Math.Abs(pos.X - _startPoint.X);
            double h = Math.Abs(pos.Y - _startPoint.Y);
            System.Windows.Controls.Canvas.SetLeft(hrect, x);
            System.Windows.Controls.Canvas.SetTop(hrect, y);
            hrect.Width = w;
            hrect.Height = h;
        }
        else if (_currentTool == AnnotationTool.Crop && _isCropping && _cropRectOverlay != null)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double x = Math.Min(pos.X, _startPoint.X);
            double y = Math.Min(pos.Y, _startPoint.Y);
            double w = Math.Abs(pos.X - _startPoint.X);
            double h = Math.Abs(pos.Y - _startPoint.Y);
            System.Windows.Controls.Canvas.SetLeft(_cropRectOverlay, x);
            System.Windows.Controls.Canvas.SetTop(_cropRectOverlay, y);
            _cropRectOverlay.Width = w;
            _cropRectOverlay.Height = h;
        }
    }

    private void AnnotationCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool == AnnotationTool.None)
        {
            AnnotationCanvas.ReleaseMouseCapture();
            if (_selectorDragStarted && _selectedElement != null)
            {
                var movedElement = _selectedElement;
                var before = _elementPositionBeforeDrag;
                var after = GetElementPosition(movedElement);
                if (before != after)
                {
                    PushHistory(new HistoryEntry(
                        () => SetElementPosition(movedElement, before),
                        () => SetElementPosition(movedElement, after)));
                }
            }
            _selectorDownElement = null;
            _selectorDragStarted = false;
            return;
        }
        if (_currentTool == AnnotationTool.Rectangle && _currentShape != null)
        {
            _currentShape = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }
        else if (_currentTool == AnnotationTool.Ellipse && _currentEllipse != null)
        {
            _currentEllipse = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }
        else if (_currentTool == AnnotationTool.Arrow && _currentArrow != null)
        {
            _currentArrow = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }
        else if (_currentTool == AnnotationTool.Line && _currentLine != null)
        {
            _currentLine = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }
        else if (_currentTool == AnnotationTool.Freehand && _currentPolyline != null)
        {
            _currentPolyline = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }
        else if (_currentTool == AnnotationTool.Highlight && _currentShape != null)
        {
            _currentShape = null;
            AnnotationCanvas.ReleaseMouseCapture();
        }
        else if (_currentTool == AnnotationTool.Crop && _isCropping && _cropRectOverlay != null)
        {
            AnnotationCanvas.ReleaseMouseCapture();
            _isCropping = false;
            // Wait for Enter key to confirm crop
        }
    }

    private static void UpdateArrowGeometry(
        System.Windows.Shapes.Path arrow,
        System.Windows.Point start,
        System.Windows.Point end)
    {
        double theta = Math.Atan2(end.Y - start.Y, end.X - start.X);
        const double arrowLength = 16;
        const double arrowAngle = Math.PI / 7;
        var point1 = new System.Windows.Point(
            end.X - arrowLength * Math.Cos(theta - arrowAngle),
            end.Y - arrowLength * Math.Sin(theta - arrowAngle));
        var point2 = new System.Windows.Point(
            end.X - arrowLength * Math.Cos(theta + arrowAngle),
            end.Y - arrowLength * Math.Sin(theta + arrowAngle));

        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(start, end));
        var head = new PathFigure { StartPoint = end, IsClosed = true, IsFilled = true };
        head.Segments.Add(new LineSegment(point1, true));
        head.Segments.Add(new LineSegment(point2, true));
        geometry.Children.Add(new PathGeometry([head]));
        arrow.Data = geometry;
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => Undo();
    private void BtnRedo_Click(object sender, RoutedEventArgs e) => Redo();

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var entry = _undoStack.Pop();
        entry.Undo();
        _redoStack.Push(entry);
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var entry = _redoStack.Pop();
        entry.Redo();
        _undoStack.Push(entry);
    }

    private void PushHistory(HistoryEntry entry)
    {
        _undoStack.Push(entry);
        ClearHistory(_redoStack);
    }

    private static void ClearHistory(Stack<HistoryEntry> history)
    {
        while (history.TryPop(out var entry))
            entry.Dispose();
    }

    private void RemoveElementVisuals(UIElement element)
    {
        AnnotationCanvas.Children.Remove(element);
        _annotations.Remove(element);
    }

    private void RestoreElementVisuals(UIElement element)
    {
        if (!AnnotationCanvas.Children.Contains(element))
            AnnotationCanvas.Children.Add(element);
        if (!_annotations.Contains(element))
            _annotations.Add(element);
    }

    private void AddAnnotation(UIElement element)
    {
        AnnotationCanvas.Children.Add(element);
        _annotations.Add(element);
        PushHistory(new HistoryEntry(
            () => RemoveElementVisuals(element),
            () => RestoreElementVisuals(element)));
    }

    private void RemoveAnnotation(UIElement element)
    {
        RemoveElementVisuals(element);
        PushHistory(new HistoryEntry(
            () => RestoreElementVisuals(element),
            () => RemoveElementVisuals(element)));
    }

    private static System.Windows.Point GetElementPosition(UIElement element)
    {
        var left = System.Windows.Controls.Canvas.GetLeft(element);
        var top = System.Windows.Controls.Canvas.GetTop(element);
        return new System.Windows.Point(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top);
    }

    private static void SetElementPosition(UIElement element, System.Windows.Point position)
    {
        System.Windows.Controls.Canvas.SetLeft(element, position.X);
        System.Windows.Controls.Canvas.SetTop(element, position.Y);
    }

    private UIElement? FindAnnotationElement(DependencyObject? hit)
    {
        while (hit != null && !ReferenceEquals(hit, AnnotationCanvas))
        {
            if (hit is UIElement element && _annotations.Contains(element))
                return element;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        AnnotationCanvas.MouseLeftButtonDown += AnnotationCanvas_MouseLeftButtonDown;
        AnnotationCanvas.MouseMove += AnnotationCanvas_MouseMove;
        AnnotationCanvas.MouseLeftButtonUp += AnnotationCanvas_MouseLeftButtonUp;
        AnnotationCanvas.KeyDown += AnnotationCanvas_KeyDown;
        AnnotationCanvas.Focusable = true;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        InitializeTrayIcon();
        Closed += (_, _) =>
        {
            UnregisterGlobalHotkeys();
            _trayIcon?.Dispose();
            _lastScreenshot?.Dispose();
            ClearHistory(_undoStack);
            ClearHistory(_redoStack);
        };
    }

    private void InitializeTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Capture area", null, (_, _) => Dispatcher.BeginInvoke(CaptureArea));
        menu.Items.Add("Capture window", null, (_, _) => Dispatcher.BeginInvoke(CaptureWindow));
        menu.Items.Add("Capture full screen", null, (_, _) => Dispatcher.BeginInvoke(CaptureFullScreen));
        menu.Items.Add("Repeat last area", null, (_, _) => Dispatcher.BeginInvoke(RepeatLastArea));
        menu.Items.Add("Delayed capture (3 seconds)", null, (_, _) => Dispatcher.BeginInvoke(DelayedCapture));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Open editor", null, (_, _) => Dispatcher.BeginInvoke(ShowFromTray));
        menu.Items.Add("Settings…", null, (_, _) => Dispatcher.BeginInvoke(OpenSettings));
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.BeginInvoke(QuitApplication));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ScreenShottr",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowFromTray);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        Hide();
        _trayIcon?.ShowBalloonTip(
            1500,
            "ScreenShottr is still running",
            "Use the notification-area icon or a capture shortcut.",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        ShowFromTray();
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _settingsService.Save(dialog.Settings);
            _settings = dialog.Settings.Clone();
            _trayIcon?.ShowBalloonTip(
                1200,
                "Settings saved",
                "New capture settings will be used immediately.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            ShowError("save settings", ex);
        }
    }

    private void QuitApplication()
    {
        _allowClose = true;
        _previewWindow?.Close();
        Close();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource.AddHook(WindowMessageHook);

        RegisterCaptureHotkey(HOTKEY_AREA, 0x31, "Ctrl+Shift+1 (Capture area)");
        RegisterCaptureHotkey(HOTKEY_FULLSCREEN, 0x32, "Ctrl+Shift+2 (Capture full screen)");
        RegisterCaptureHotkey(HOTKEY_WINDOW, 0x33, "Ctrl+Shift+3 (Capture window)");
        RegisterCaptureHotkey(HOTKEY_REPEAT, 0x34, "Ctrl+Shift+4 (Repeat area)");
    }

    private void RegisterCaptureHotkey(int id, uint key, string label)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (!RegisterHotKey(handle, id, MOD_CONTROL | MOD_SHIFT, key))
        {
            _trayIcon?.ShowBalloonTip(
                2500,
                "Shortcut unavailable",
                $"{label} is already in use by another application.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WM_HOTKEY)
            return IntPtr.Zero;

        handled = true;
        switch (wParam.ToInt32())
        {
            case HOTKEY_AREA:
                CaptureArea();
                break;
            case HOTKEY_FULLSCREEN:
                CaptureFullScreen();
                break;
            case HOTKEY_WINDOW:
                CaptureWindow();
                break;
            case HOTKEY_REPEAT:
                RepeatLastArea();
                break;
        }
        return IntPtr.Zero;
    }

    private void UnregisterGlobalHotkeys()
    {
        var handle = new WindowInteropHelper(this).Handle;
        foreach (var id in new[] { HOTKEY_AREA, HOTKEY_FULLSCREEN, HOTKEY_WINDOW, HOTKEY_REPEAT })
            UnregisterHotKey(handle, id);
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y)
            {
                Redo();
                e.Handled = true;
            }
        }
        else if (_currentTool == AnnotationTool.None && e.Key == Key.Delete && _selectedElement != null)
        {
            RemoveAnnotation(_selectedElement);
            _selectedElement = null;
            e.Handled = true;
        }
        if (_currentTool == AnnotationTool.Crop && _cropRectOverlay != null && e.Key == Key.Enter)
        {
            double x = System.Windows.Controls.Canvas.GetLeft(_cropRectOverlay);
            double y = System.Windows.Controls.Canvas.GetTop(_cropRectOverlay);
            double w = _cropRectOverlay.Width;
            double h = _cropRectOverlay.Height;
            if (_lastScreenshot != null)
            {
                using var cropped = CreateCroppedBitmap(x, y, w, h);
                if (cropped != null)
                {
                    var before = new Bitmap(_lastScreenshot);
                    var after = new Bitmap(cropped);
                    var annotationsBefore = _annotations.ToList();
                    ApplyScreenshot(after);
                    PushHistory(new HistoryEntry(
                        () =>
                        {
                            ApplyScreenshot(before);
                            RestoreAnnotationSet(annotationsBefore);
                        },
                        () => ApplyScreenshot(after),
                        before,
                        after));
                    CopyScreenshotToClipboard();
                }
            }
            AnnotationCanvas.Children.Remove(_cropRectOverlay);
            _cropRectOverlay = null;
            _currentTool = AnnotationTool.None;
            ResetToolButtons();
            e.Handled = true;
        }
    }

    private void AnnotationCanvas_KeyDown(object sender, KeyEventArgs e)
    {
        if (_currentTool == AnnotationTool.None && e.Key == Key.Delete && _selectedElement != null)
        {
            RemoveAnnotation(_selectedElement);
            _selectedElement = null;
        }
    }

    private Bitmap? _lastScreenshot;

    // Auto copy to clipboard after capture/crop
    private void CopyScreenshotToClipboard()
    {
        if (_lastScreenshot == null) return;
        using var composite = CreateCompositeBitmap();
        Clipboard.SetImage(BitmapToImageSource(composite));
    }

    // Call CopyScreenshotToClipboard after capture/crop
    private void BtnAreaCapture_Click(object sender, RoutedEventArgs e)
        => CaptureArea();

    private void BtnWindowCapture_Click(object sender, RoutedEventArgs e)
        => CaptureWindow();

    private void BtnFullScreenCapture_Click(object sender, RoutedEventArgs e)
        => CaptureFullScreen();

    private void BtnRepeatCapture_Click(object sender, RoutedEventArgs e)
        => RepeatLastArea();

    private void BtnDelayedCapture_Click(object sender, RoutedEventArgs e)
        => DelayedCapture();

    private async void CaptureArea()
    {
        var restoreEditorOnCancel = IsVisible;
        var completed = false;
        Hide();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        try
        {
            var overlay = new AreaSelectionWindow();
            if (overlay.ShowDialog() == true)
            {
                _lastCaptureRegion = overlay.SelectedRegion;
                BtnRepeatCapture.IsEnabled = true;
                CompleteCapture(CaptureScreenRegion(overlay.SelectedRegion), "Area captured");
                completed = true;
            }
        }
        catch (Exception ex)
        {
            ShowError("capture the selected area", ex);
        }
        finally
        {
            if (!completed && restoreEditorOnCancel)
                ShowFromTray();
        }
    }

    private async void CaptureWindow()
    {
        var restoreEditorOnCancel = IsVisible;
        var completed = false;
        Hide();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        try
        {
            var overlay = new WindowPickerOverlay();
            if (overlay.ShowDialog() == true)
            {
                CompleteCapture(
                    CaptureWindowHandle(overlay.SelectedWindowHandle),
                    "Window captured");
                completed = true;
            }
        }
        catch (Exception ex)
        {
            ShowError("capture the selected window", ex);
        }
        finally
        {
            if (!completed && restoreEditorOnCancel)
                ShowFromTray();
        }
    }

    private async void CaptureFullScreen()
    {
        var restoreEditorOnFailure = IsVisible;
        var completed = false;
        Hide();
        await System.Threading.Tasks.Task.Delay(250);
        try
        {
            CompleteCapture(CaptureFullScreenDpiAware(), "Full screen captured");
            completed = true;
        }
        catch (Exception ex)
        {
            ShowError("capture the screen", ex);
        }
        finally
        {
            if (!completed && restoreEditorOnFailure)
                ShowFromTray();
        }
    }

    private async void RepeatLastArea()
    {
        if (_lastCaptureRegion is not { } region)
        {
            _trayIcon?.ShowBalloonTip(
                1800,
                "No area to repeat",
                "Capture an area first, then repeat it with Ctrl+Shift+4.",
                System.Windows.Forms.ToolTipIcon.Info);
            return;
        }

        var restoreEditorOnFailure = IsVisible;
        var completed = false;
        Hide();
        await System.Threading.Tasks.Task.Delay(200);
        try
        {
            CompleteCapture(CaptureScreenRegion(region), "Area captured again");
            completed = true;
        }
        catch (Exception ex)
        {
            ShowError("repeat the last area capture", ex);
        }
        finally
        {
            if (!completed && restoreEditorOnFailure)
                ShowFromTray();
        }
    }

    private async void DelayedCapture()
    {
        var restoreEditorOnFailure = IsVisible;
        var completed = false;
        Hide();
        _trayIcon?.ShowBalloonTip(
            1500,
            "Delayed capture",
            "Capturing the full screen in 3 seconds.",
            System.Windows.Forms.ToolTipIcon.Info);
        await System.Threading.Tasks.Task.Delay(3000);
        try
        {
            CompleteCapture(CaptureFullScreenDpiAware(), "Delayed capture complete");
            completed = true;
        }
        catch (Exception ex)
        {
            ShowError("take the delayed screenshot", ex);
        }
        finally
        {
            if (!completed && restoreEditorOnFailure)
                ShowFromTray();
        }
    }

    private void CompleteCapture(Bitmap bitmap, string notification)
    {
        _previewWindow?.Close();
        SetScreenshot(bitmap);
        var action = _settings.PostCaptureAction;
        var copied = _settings.AutoCopy || action == PostCaptureAction.CopyOnly;
        var saved = _settings.AutoSave || action == PostCaptureAction.SaveOnly;
        string? savedPath = null;

        if (copied)
            CopyScreenshotToClipboard();
        if (saved)
            savedPath = SaveScreenshotAutomatically();

        switch (action)
        {
            case PostCaptureAction.Preview:
                ShowCapturePreview();
                break;
            case PostCaptureAction.Editor:
                ShowFromTray();
                break;
            case PostCaptureAction.CopyOnly:
            case PostCaptureAction.SaveOnly:
                Hide();
                break;
        }

        var outcomes = new List<string>();
        if (copied) outcomes.Add("copied");
        if (savedPath != null) outcomes.Add($"saved to {IOPath.GetFileName(savedPath)}");
        var suffix = outcomes.Count > 0 ? $" — {string.Join(" and ", outcomes)}." : ".";
        _trayIcon?.ShowBalloonTip(
            1200,
            "ScreenShottr",
            notification + suffix,
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ShowCapturePreview()
    {
        if (_lastScreenshot == null)
            return;

        _previewWindow?.Close();
        using var composite = CreateCompositeBitmap();
        var preview = BitmapToImageSource(composite);
        preview.Freeze();
        _previewWindow = new CapturePreviewWindow(
            preview,
            () => RunUserAction("copy the screenshot", CopyScreenshotToClipboard),
            () => RunUserAction("save the screenshot", SaveScreenshotAs),
            ShowFromTray);
        _previewWindow.Closed += (_, _) => _previewWindow = null;
        _previewWindow.Show();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScreenshot == null) return;
        RunUserAction("save the screenshot", SaveScreenshotAs);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScreenshot == null) return;
        RunUserAction("copy the screenshot", CopyScreenshotToClipboard);
    }

    private void RunUserAction(string action, Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            ShowError(action, ex);
        }
    }

    private string SaveScreenshotAutomatically()
    {
        Directory.CreateDirectory(_settings.SaveFolder);
        var fileName = CaptureFileNaming.BuildFileName(
            _settings.FileNamePattern,
            DateTime.Now,
            _settings.ImageFormat);
        var path = GetAvailablePath(IOPath.Combine(_settings.SaveFolder, fileName));
        SaveCompositeBitmap(path, _settings.ImageFormat);
        return path;
    }

    private void SaveScreenshotAs()
    {
        if (_lastScreenshot == null)
            return;

        var suggestedName = CaptureFileNaming.BuildFileName(
            _settings.FileNamePattern,
            DateTime.Now,
            _settings.ImageFormat);
        var isJpeg = _settings.ImageFormat == "jpg";
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg",
            FilterIndex = isJpeg ? 2 : 1,
            DefaultExt = isJpeg ? ".jpg" : ".png",
            AddExtension = true,
            InitialDirectory = Directory.Exists(_settings.SaveFolder)
                ? _settings.SaveFolder
                : null,
            FileName = suggestedName
        };
        if (dialog.ShowDialog() != true)
            return;

        var format = string.Equals(IOPath.GetExtension(dialog.FileName), ".png", StringComparison.OrdinalIgnoreCase)
            ? "png"
            : "jpg";
        SaveCompositeBitmap(dialog.FileName, format);
    }

    private void SaveCompositeBitmap(string path, string format)
    {
        using var composite = CreateCompositeBitmap();
        composite.Save(
            path,
            format == "jpg"
                ? System.Drawing.Imaging.ImageFormat.Jpeg
                : System.Drawing.Imaging.ImageFormat.Png);
    }

    private static string GetAvailablePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var directory = IOPath.GetDirectoryName(path) ?? string.Empty;
        var stem = IOPath.GetFileNameWithoutExtension(path);
        var extension = IOPath.GetExtension(path);
        for (var index = 2; ; index++)
        {
            var candidate = IOPath.Combine(directory, $"{stem}_{index}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private void ShowError(string action, Exception exception)
    {
        if (IsVisible)
        {
            MessageBox.Show(
                this,
                $"ScreenShottr could not {action}.\n\n{exception.Message}",
                "ScreenShottr",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(
                $"ScreenShottr could not {action}.\n\n{exception.Message}",
                "ScreenShottr",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SetScreenshot(Bitmap bitmap)
    {
        _lastScreenshot?.Dispose();
        _lastScreenshot = bitmap;
        ScreenshotImage.Source = BitmapToImageSource(bitmap);
        AnnotationCanvas.Children.Clear();
        _annotations.Clear();
        ClearHistory(_undoStack);
        ClearHistory(_redoStack);
        DeselectElement();
        _stepCounter = 1;
    }

    private void ApplyScreenshot(Bitmap bitmap)
    {
        _lastScreenshot?.Dispose();
        _lastScreenshot = new Bitmap(bitmap);
        ScreenshotImage.Source = BitmapToImageSource(_lastScreenshot);
        AnnotationCanvas.Children.Clear();
        _annotations.Clear();
        DeselectElement();
        _stepCounter = 1;
    }

    private void RestoreAnnotationSet(IEnumerable<UIElement> annotations)
    {
        foreach (var annotation in annotations)
            RestoreElementVisuals(annotation);
    }

    private Bitmap CreateCompositeBitmap()
    {
        if (_lastScreenshot == null)
            throw new InvalidOperationException("There is no screenshot to render.");

        var composite = new Bitmap(_lastScreenshot);
        if (_annotations.Count == 0 || AnnotationCanvas.ActualWidth <= 0 || AnnotationCanvas.ActualHeight <= 0)
            return composite;

        var renderedAnnotations = new RenderTargetBitmap(
            _lastScreenshot.Width,
            _lastScreenshot.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(
                _lastScreenshot.Width / AnnotationCanvas.ActualWidth,
                _lastScreenshot.Height / AnnotationCanvas.ActualHeight));
            context.DrawRectangle(
                new VisualBrush(AnnotationCanvas),
                null,
                new Rect(0, 0, AnnotationCanvas.ActualWidth, AnnotationCanvas.ActualHeight));
        }
        renderedAnnotations.Render(drawing);

        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderedAnnotations));
        encoder.Save(stream);
        stream.Position = 0;
        using var overlay = new Bitmap(stream);
        using var graphics = Graphics.FromImage(composite);
        graphics.DrawImageUnscaled(overlay, 0, 0);
        return composite;
    }

    // --- Screenshot helpers ---
    private Bitmap CaptureScreenRegion(System.Drawing.Rectangle region)
    {
        var bmp = new Bitmap(region.Width, region.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(region.X, region.Y, 0, 0, new System.Drawing.Size(region.Width, region.Height));
        }
        return bmp;
    }

    private Bitmap CaptureWindowHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("No window was selected.");

        RECT rect;
        if (DwmGetWindowAttribute(
                handle,
                DWMWA_EXTENDED_FRAME_BOUNDS,
                out rect,
                Marshal.SizeOf<RECT>()) != 0)
        {
            if (!GetWindowRect(handle, out rect))
                throw new InvalidOperationException("Windows could not read the selected window bounds.");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("The selected window has no visible capture area.");

        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
        }
        return bmp;
    }

    private Bitmap CaptureFullScreenDpiAware()
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
        }
        return bmp;
    }

    // --- Interop for active window ---
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        out RECT value,
        int valueSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // --- Bitmap to ImageSource ---
    private BitmapSource BitmapToImageSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    private bool BitmapsAreEqual(Bitmap bmp1, Bitmap bmp2)
    {
        if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height) return false;
        for (int y = 0; y < bmp1.Height; y++)
        {
            for (int x = 0; x < bmp1.Width; x++)
            {
                if (bmp1.GetPixel(x, y) != bmp2.GetPixel(x, y))
                    return false;
            }
        }
        return true;
    }

    private Bitmap StitchBitmapsVertically(List<Bitmap> bitmaps)
    {
        int width = bitmaps[0].Width;
        int height = 0;
        foreach (var bmp in bitmaps) height += bmp.Height;
        var result = new Bitmap(width, height);
        using (var g = Graphics.FromImage(result))
        {
            int y = 0;
            foreach (var bmp in bitmaps)
            {
                g.DrawImage(bmp, 0, y);
                y += bmp.Height;
            }
        }
        return result;
    }

    private void SendPageDownKey()
    {
        const byte VK_NEXT = 0x22; // Page Down
        keybd_event(VK_NEXT, 0, 0, 0); // key down
        keybd_event(VK_NEXT, 0, 2, 0); // key up
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private void BeginTextEdit(TextBox textBox)
    {
        textBox.Tag = textBox.Text;
        textBox.IsReadOnly = false;
        textBox.Focusable = true;
        textBox.Focus();
        textBox.SelectAll();
    }

    private void CommitTextEdit(TextBox textBox)
    {
        if (!AnnotationCanvas.Children.Contains(textBox))
            return;

        var before = textBox.Tag as string ?? textBox.Text;
        var after = textBox.Text.Trim();
        if (string.IsNullOrEmpty(after))
        {
            RemoveAnnotation(textBox);
            return;
        }

        textBox.Text = after;
        textBox.Tag = after;
        textBox.IsReadOnly = true;
        textBox.Focusable = false;
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            PushHistory(new HistoryEntry(
                () =>
                {
                    textBox.Text = before;
                    textBox.Tag = before;
                },
                () =>
                {
                    textBox.Text = after;
                    textBox.Tag = after;
                }));
        }
    }

    private Bitmap? CreateCroppedBitmap(double x, double y, double w, double h)
    {
        if (_lastScreenshot == null) return null;
        int bmpW = (int)AnnotationCanvas.ActualWidth;
        int bmpH = (int)AnnotationCanvas.ActualHeight;
        if (bmpW <= 0 || bmpH <= 0) return null;

        var crop = CaptureGeometry.ProjectCrop(
            x, y, w, h, bmpW, bmpH, _lastScreenshot.Width, _lastScreenshot.Height);
        if (crop.IsEmpty) return null;
        var cropped = new Bitmap(crop.Width, crop.Height);
        using (var g = Graphics.FromImage(cropped))
        {
            g.DrawImage(
                _lastScreenshot,
                new System.Drawing.Rectangle(0, 0, crop.Width, crop.Height),
                crop,
                GraphicsUnit.Pixel);
        }
        return cropped;
    }
}

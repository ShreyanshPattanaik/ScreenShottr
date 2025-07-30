using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;
using WindowsInput;
using WindowsInput.Native;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Forms;
// using Tesseract;

namespace ShottrClone;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private enum AnnotationTool { None, Rectangle, Ellipse, Arrow, Line, Text, Freehand, Step, Highlight, Pixelate, Blur, Erase, Crop }
    private AnnotationTool _currentTool = AnnotationTool.None;
    private Shape _currentShape;
    private Polyline _currentPolyline;
    private System.Windows.Point _startPoint;
    private List<UIElement> _annotations = new();
    private Line _currentArrow;
    private Ellipse _currentEllipse;
    private TextBox _currentTextBox;
    private int _stepCounter = 1;
    private Line _currentLine;
    private UIElement _selectedElement;
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging = false;
    private UIElement _selectorDownElement;
    private System.Windows.Point _selectorDownPoint;
    private bool _selectorDragStarted = false;
    private Stack<(string action, UIElement element, object extra)> _undoStack = new();
    private Stack<(string action, UIElement element, object extra)> _redoStack = new();
    private System.Windows.Shapes.Rectangle _cropRectOverlay;
    private bool _isCropping = false;

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
            var hit = AnnotationCanvas.InputHitTest(pos) as UIElement;
            _selectorDownElement = hit;
            _selectorDownPoint = pos;
            _selectorDragStarted = false;
            if (hit != null && _annotations.Contains(hit))
            {
                SelectElement(hit);
                _dragStartPoint = pos;
                _isDragging = false;
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
            var line = new Line
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                X1 = _startPoint.X,
                Y1 = _startPoint.Y,
                X2 = _startPoint.X,
                Y2 = _startPoint.Y
            };
            _currentArrow = line;
            AddAnnotation(line);
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
                MinWidth = 40
            };
            System.Windows.Controls.Canvas.SetLeft(textBox, pos.X);
            System.Windows.Controls.Canvas.SetTop(textBox, pos.Y);
            textBox.LostFocus += (s, ev) => ConvertTextBoxToTextBlock(textBox);
            textBox.KeyDown += (s, ev) => { if (ev.Key == Key.Enter) ConvertTextBoxToTextBlock(textBox); };
            AddAnnotation(textBox);
            textBox.Focus();
            textBox.SelectAll();
        }
        else if (_currentTool == AnnotationTool.Step)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            var circle = new Ellipse
            {
                Width = 32,
                Height = 32,
                Fill = System.Windows.Media.Brushes.Gold,
                Stroke = System.Windows.Media.Brushes.DarkGoldenrod,
                StrokeThickness = 2
            };
            var label = new TextBlock
            {
                Text = _stepCounter.ToString(),
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Foreground = System.Windows.Media.Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Canvas.SetLeft(circle, pos.X - 16);
            System.Windows.Controls.Canvas.SetTop(circle, pos.Y - 16);
            System.Windows.Controls.Canvas.SetLeft(label, pos.X - 8);
            System.Windows.Controls.Canvas.SetTop(label, pos.Y - 12);
            AddAnnotation(circle);
            AddAnnotation(label);
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
                _isDragging = true;
            }
            if (_selectorDragStarted && _selectedElement != null)
            {
                _dragStartPoint = pos;
                double left = System.Windows.Controls.Canvas.GetLeft(_selectedElement);
                double top = System.Windows.Controls.Canvas.GetTop(_selectedElement);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                System.Windows.Controls.Canvas.SetLeft(_selectedElement, left + dx);
                System.Windows.Controls.Canvas.SetTop(_selectedElement, top + dy);
                // Move paired label for step tool
                if (_selectedElement is Ellipse)
                {
                    foreach (var el in _annotations)
                    {
                        if (el is TextBlock lbl)
                        {
                            double l = System.Windows.Controls.Canvas.GetLeft(lbl);
                            double t = System.Windows.Controls.Canvas.GetTop(lbl);
                            if (Math.Abs(l - (left + 8)) < 2 && Math.Abs(t - (top + 4)) < 2)
                            {
                                System.Windows.Controls.Canvas.SetLeft(lbl, l + dx);
                                System.Windows.Controls.Canvas.SetTop(lbl, t + dy);
                            }
                        }
                    }
                }
                _selectorDownPoint = pos;
                e.Handled = true;
                return;
            }
        }
        if (_currentTool == AnnotationTool.None && _isDragging && _selectedElement != null)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double dx = pos.X - _dragStartPoint.X;
            double dy = pos.Y - _dragStartPoint.Y;
            _dragStartPoint = pos;
            double left = System.Windows.Controls.Canvas.GetLeft(_selectedElement);
            double top = System.Windows.Controls.Canvas.GetTop(_selectedElement);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            System.Windows.Controls.Canvas.SetLeft(_selectedElement, left + dx);
            System.Windows.Controls.Canvas.SetTop(_selectedElement, top + dy);
            // Move paired label for step tool
            if (_selectedElement is Ellipse)
            {
                foreach (var el in _annotations)
                {
                    if (el is TextBlock lbl)
                    {
                        double l = System.Windows.Controls.Canvas.GetLeft(lbl);
                        double t = System.Windows.Controls.Canvas.GetTop(lbl);
                        if (Math.Abs(l - (left + 8)) < 2 && Math.Abs(t - (top + 4)) < 2)
                        {
                            System.Windows.Controls.Canvas.SetLeft(lbl, l + dx);
                            System.Windows.Controls.Canvas.SetTop(lbl, t + dy);
                        }
                    }
                }
            }
            e.Handled = true;
            return;
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
            _currentArrow.X2 = pos.X;
            _currentArrow.Y2 = pos.Y;
            DrawArrowHead(_currentArrow);
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
            if (!_selectorDragStarted && _selectorDownElement is TextBlock tb && _annotations.Contains(tb))
            {
                ConvertTextBlockToTextBox(tb);
            }
            _selectorDownElement = null;
            _selectorDragStarted = false;
            _isDragging = false;
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

    // Draws an arrowhead at the end of a line
    private void DrawArrowHead(Line line)
    {
        // Remove any previous arrowhead
        var toRemove = new List<UIElement>();
        foreach (var child in AnnotationCanvas.Children)
        {
            if (child is Polygon poly && poly.Tag == line)
                toRemove.Add(poly);
        }
        foreach (var el in toRemove)
            AnnotationCanvas.Children.Remove(el);

        // Calculate arrowhead points
        double theta = Math.Atan2(line.Y2 - line.Y1, line.X2 - line.X1);
        double arrowLen = 16;
        double angle = Math.PI / 7;
        var pt1 = new System.Windows.Point(
            line.X2 - arrowLen * Math.Cos(theta - angle),
            line.Y2 - arrowLen * Math.Sin(theta - angle));
        var pt2 = new System.Windows.Point(
            line.X2 - arrowLen * Math.Cos(theta + angle),
            line.Y2 - arrowLen * Math.Sin(theta + angle));
        var arrowHead = new Polygon
        {
            Points = new PointCollection { new System.Windows.Point(line.X2, line.Y2), pt1, pt2 },
            Fill = System.Windows.Media.Brushes.Red,
            Stroke = System.Windows.Media.Brushes.Red,
            StrokeThickness = 2,
            Tag = line
        };
        AnnotationCanvas.Children.Add(arrowHead);
        _annotations.Add(arrowHead);
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => Undo();
    private void BtnRedo_Click(object sender, RoutedEventArgs e) => Redo();

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var (action, element, extra) = _undoStack.Pop();
        if (action == "add")
        {
            AnnotationCanvas.Children.Remove(element);
            _annotations.Remove(element);
            _redoStack.Push(("add", element, null));
        }
        else if (action == "delete")
        {
            AnnotationCanvas.Children.Add(element);
            _annotations.Add(element);
            _redoStack.Push(("delete", element, null));
        }
        else if (action == "crop")
        {
            var prevBmp = extra as Bitmap;
            if (prevBmp != null)
            {
                ScreenshotImage.Source = BitmapToImageSource(prevBmp);
                _lastScreenshot?.Dispose();
                _lastScreenshot = new Bitmap(prevBmp);
                AnnotationCanvas.Children.Clear();
                _annotations.Clear();
            }
            _redoStack.Push(("crop", null, null)); // Clear redo stack for crop
        }
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var (action, element, extra) = _redoStack.Pop();
        if (action == "add")
        {
            AnnotationCanvas.Children.Add(element);
            _annotations.Add(element);
            _undoStack.Push(("add", element, null));
        }
        else if (action == "delete")
        {
            AnnotationCanvas.Children.Remove(element);
            _annotations.Remove(element);
            _undoStack.Push(("delete", element, null));
        }
    }

    private void AddAnnotation(UIElement element)
    {
        AnnotationCanvas.Children.Add(element);
        _annotations.Add(element);
        _undoStack.Push(("add", element, null));
        _redoStack.Clear();
    }

    private void RemoveAnnotation(UIElement element)
    {
        AnnotationCanvas.Children.Remove(element);
        _annotations.Remove(element);
        _undoStack.Push(("delete", element, null));
        _redoStack.Clear();
    }

    public MainWindow()
    {
        InitializeComponent();
        AnnotationCanvas.MouseLeftButtonDown += AnnotationCanvas_MouseLeftButtonDown;
        AnnotationCanvas.MouseMove += AnnotationCanvas_MouseMove;
        AnnotationCanvas.MouseLeftButtonUp += AnnotationCanvas_MouseLeftButtonUp;
        AnnotationCanvas.KeyDown += AnnotationCanvas_KeyDown;
        AnnotationCanvas.Focusable = true;
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
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
            // Perform crop
            double x = System.Windows.Controls.Canvas.GetLeft(_cropRectOverlay);
            double y = System.Windows.Controls.Canvas.GetTop(_cropRectOverlay);
            double w = _cropRectOverlay.Width;
            double h = _cropRectOverlay.Height;
            // Push current image to undo stack before cropping
            if (_lastScreenshot != null)
            {
                var bmpCopy = new Bitmap(_lastScreenshot);
                _undoStack.Push(("crop", null, bmpCopy));
                _redoStack.Clear();
            }
            CropImageToRect(x, y, w, h);
            AnnotationCanvas.Children.Remove(_cropRectOverlay);
            _cropRectOverlay = null;
            _currentTool = AnnotationTool.None;
            ResetToolButtons();
            e.Handled = true;
        }
        // Add undo for crop
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Z)
        {
            if (_undoStack.Count > 0 && _undoStack.Peek().action == "crop")
            {
                var prevBmp = _undoStack.Pop().extra as Bitmap;
                if (prevBmp != null)
                {
                    ScreenshotImage.Source = BitmapToImageSource(prevBmp);
                    _lastScreenshot?.Dispose();
                    _lastScreenshot = new Bitmap(prevBmp);
                    AnnotationCanvas.Children.Clear();
                    _annotations.Clear();
                }
                e.Handled = true;
                return;
            }
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

    private Bitmap _lastScreenshot;

    // Comment out OCR button handler and logic
    // private void BtnOcr_Click(object sender, RoutedEventArgs e)
    // {
    //     if (_lastScreenshot == null) return;
    //     string text = PerformOcr(_lastScreenshot);
    //     if (!string.IsNullOrWhiteSpace(text))
    //     {
    //         Clipboard.SetText(text);
    //         System.Windows.MessageBox.Show("OCR result copied to clipboard:\n\n" + text, "OCR");
    //     }
    //     else
    //     {
    //         System.Windows.MessageBox.Show("No text found.", "OCR");
    //     }
    // }
    // private string PerformOcr(System.Drawing.Bitmap bmp)
    // {
    //     try
    //     {
    //         using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
    //         using var ms = new MemoryStream();
    //         bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
    //         ms.Position = 0;
    //         using var img = Pix.LoadFromMemory(ms.ToArray());
    //         using var page = engine.Process(img);
    //         return page.GetText();
    //     }
    //     catch (Exception ex)
    //     {
    //         System.Windows.MessageBox.Show("OCR error: " + ex.Message);
    //         return string.Empty;
    //     }
    // }

    // Auto copy to clipboard after capture/crop
    private void CopyScreenshotToClipboard()
    {
        if (_lastScreenshot == null) return;
        Clipboard.SetImage(BitmapToImageSource(_lastScreenshot));
    }

    // Call CopyScreenshotToClipboard after capture/crop
    private void BtnAreaCapture_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var overlay = new AreaSelectionWindow();
            if (overlay.ShowDialog() == true)
            {
                var rect = overlay.SelectedRegion;
                var bmp = CaptureScreenRegion(rect);
                ScreenshotImage.Source = BitmapToImageSource(bmp);
                _lastScreenshot = bmp;
                CopyScreenshotToClipboard();
            }
            Show();
        }), DispatcherPriority.ApplicationIdle);
    }
    private void BtnFullScreenCapture_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        Dispatcher.BeginInvoke(async () =>
        {
            await System.Threading.Tasks.Task.Delay(500); // Increased delay to ensure window is hidden
            var bmp = CaptureFullScreenDpiAware();
            ScreenshotImage.Source = BitmapToImageSource(bmp);
            _lastScreenshot = bmp;
            CopyScreenshotToClipboard();
            Show();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScreenshot == null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (dialog.ShowDialog() == true)
        {
            _lastScreenshot.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScreenshot == null) return;
        Clipboard.SetImage(BitmapToImageSource(_lastScreenshot));
    }

    // --- Screenshot helpers ---
    private Bitmap CaptureScreen()
    {
        int width = (int)SystemParameters.VirtualScreenWidth;
        int height = (int)SystemParameters.VirtualScreenHeight;
        int left = (int)SystemParameters.VirtualScreenLeft;
        int top = (int)SystemParameters.VirtualScreenTop;
        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(left, top, 0, 0, bmp.Size);
        }
        return bmp;
    }

    private Bitmap CaptureScreenRegion(System.Drawing.Rectangle region)
    {
        // Adjust for virtual screen origin (handles negative coordinates for multi-monitor)
        int absX = region.X + (int)SystemParameters.VirtualScreenLeft;
        int absY = region.Y + (int)SystemParameters.VirtualScreenTop;
        var bmp = new Bitmap(region.Width, region.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(absX, absY, 0, 0, new System.Drawing.Size(region.Width, region.Height));
        }
        return bmp;
    }

    private Bitmap CaptureActiveWindow()
    {
        IntPtr handle = GetForegroundWindow();
        GetWindowRect(handle, out RECT rect);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
        }
        return bmp;
    }

    private Bitmap CaptureFullScreenDpiAware()
    {
        // Get the virtual screen bounds in physical pixels
        int left = (int)SystemParameters.VirtualScreenLeft;
        int top = (int)SystemParameters.VirtualScreenTop;
        int width = (int)SystemParameters.VirtualScreenWidth;
        int height = (int)SystemParameters.VirtualScreenHeight;

        // Get system DPI (assume primary screen for simplicity)
        var source = PresentationSource.FromVisual(this);
        double dpiX = 1.0, dpiY = 1.0;
        if (source != null)
        {
            dpiX = source.CompositionTarget.TransformToDevice.M11;
            dpiY = source.CompositionTarget.TransformToDevice.M22;
        }
        int pxLeft = (int)(left * dpiX);
        int pyTop = (int)(top * dpiY);
        int pxWidth = (int)(width * dpiX);
        int pyHeight = (int)(height * dpiY);

        var bmp = new Bitmap(pxWidth, pyHeight);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(pxLeft, pyTop, 0, 0, new System.Drawing.Size(pxWidth, pyHeight));
        }
        return bmp;
    }

    // --- Interop for active window ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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

    private void ConvertTextBoxToTextBlock(TextBox textBox)
    {
        if (!AnnotationCanvas.Children.Contains(textBox)) return;
        var text = textBox.Text;
        var left = System.Windows.Controls.Canvas.GetLeft(textBox);
        var top = System.Windows.Controls.Canvas.GetTop(textBox);
        AnnotationCanvas.Children.Remove(textBox);
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 18,
            Foreground = System.Windows.Media.Brushes.Black,
            FontWeight = FontWeights.Bold
        };
        System.Windows.Controls.Canvas.SetLeft(textBlock, left);
        System.Windows.Controls.Canvas.SetTop(textBlock, top);
        textBlock.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                ConvertTextBlockToTextBox(textBlock);
                e.Handled = true;
            }
        };
        AnnotationCanvas.Children.Add(textBlock);
        _annotations.Add(textBlock);
    }

    private void ConvertTextBlockToTextBox(TextBlock textBlock)
    {
        if (!AnnotationCanvas.Children.Contains(textBlock)) return;
        var text = textBlock.Text;
        var left = System.Windows.Controls.Canvas.GetLeft(textBlock);
        var top = System.Windows.Controls.Canvas.GetTop(textBlock);
        AnnotationCanvas.Children.Remove(textBlock);
        var textBox = new TextBox
        {
            Text = text,
            FontSize = 18,
            Foreground = System.Windows.Media.Brushes.Black,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            MinWidth = 40
        };
        System.Windows.Controls.Canvas.SetLeft(textBox, left);
        System.Windows.Controls.Canvas.SetTop(textBox, top);
        textBox.LostFocus += (s, ev) => ConvertTextBoxToTextBlock(textBox);
        textBox.KeyDown += (s, ev) => { if (ev.Key == Key.Enter) ConvertTextBoxToTextBlock(textBox); };
        AnnotationCanvas.Children.Add(textBox);
        _annotations.Add(textBox);
        textBox.Focus();
        textBox.SelectAll();
    }

    private void CropImageToRect(double x, double y, double w, double h)
    {
        if (_lastScreenshot == null) return;
        int bmpW = (int)AnnotationCanvas.ActualWidth;
        int bmpH = (int)AnnotationCanvas.ActualHeight;
        double scaleX = _lastScreenshot.Width / (double)bmpW;
        double scaleY = _lastScreenshot.Height / (double)bmpH;
        int px = (int)(x * scaleX);
        int py = (int)(y * scaleY);
        int pw = (int)(w * scaleX);
        int ph = (int)(h * scaleY);
        if (pw <= 0 || ph <= 0) return;
        var cropped = new Bitmap(pw, ph);
        using (var g = Graphics.FromImage(cropped))
        {
            g.DrawImage(_lastScreenshot, new System.Drawing.Rectangle(0, 0, pw, ph), new System.Drawing.Rectangle(px, py, pw, ph), GraphicsUnit.Pixel);
        }
        ScreenshotImage.Source = BitmapToImageSource(cropped);
        _lastScreenshot.Dispose();
        _lastScreenshot = cropped;
        CopyScreenshotToClipboard();
        // Remove all annotations after crop for simplicity
        AnnotationCanvas.Children.Clear();
        _annotations.Clear();
    }
}
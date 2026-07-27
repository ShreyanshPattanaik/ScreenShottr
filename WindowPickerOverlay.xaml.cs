using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShottrClone
{
    public partial class WindowPickerOverlay : Window
    {
        public IntPtr SelectedWindowHandle { get; private set; }
        private readonly List<(IntPtr hWnd, Rect rect, Rectangle shape)> _windowRects = [];
        private Rectangle? _highlightRect;

        public WindowPickerOverlay()
        {
            InitializeComponent();
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            Loaded += WindowPickerOverlay_Loaded;
            MouseMove += WindowPickerOverlay_MouseMove;
            MouseLeftButtonDown += WindowPickerOverlay_MouseLeftButtonDown;
            KeyDown += WindowPickerOverlay_KeyDown;
        }

        private void WindowPickerOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            EnumerateWindows();
        }

        private void EnumerateWindows()
        {
            _windowRects.Clear();
            OverlayCanvas.Children.Clear();
            IntPtr thisHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (hWnd == thisHwnd) return true;
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == Process.GetCurrentProcess().Id) return true;
                GetWindowRect(hWnd, out RECT rect);
                if (rect.Right - rect.Left < 50 || rect.Bottom - rect.Top < 50) return true; // skip tiny windows
                var r = new Rect(
                    rect.Left - SystemParameters.VirtualScreenLeft,
                    rect.Top - SystemParameters.VirtualScreenTop,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top);
                var shape = new Rectangle
                {
                    Width = r.Width,
                    Height = r.Height,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 0, 200, 255)),
                    Tag = hWnd
                };
                Canvas.SetLeft(shape, r.Left);
                Canvas.SetTop(shape, r.Top);
                OverlayCanvas.Children.Add(shape);
                _windowRects.Add((hWnd, r, shape));
                return true;
            }, IntPtr.Zero);
        }

        private void WindowPickerOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            Rectangle? hovered = null;
            foreach (var (_, rect, shape) in _windowRects)
            {
                if (rect.Contains(pos))
                {
                    hovered = shape;
                    break;
                }
            }
            if (_highlightRect != null)
                _highlightRect.Stroke = Brushes.DodgerBlue;
            if (hovered != null)
            {
                hovered.Stroke = Brushes.Orange;
                _highlightRect = hovered;
            }
            else
            {
                _highlightRect = null;
            }
        }

        private void WindowPickerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            foreach (var (hWnd, rect, _) in _windowRects)
            {
                if (rect.Contains(pos))
                {
                    SelectedWindowHandle = hWnd;
                    DialogResult = true;
                    Close();
                    return;
                }
            }
        }

        private void WindowPickerOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        // Win32 interop
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
    }
}

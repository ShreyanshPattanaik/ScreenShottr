using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ShottrClone
{
    public partial class AreaSelectionWindow : Window
    {
        private System.Windows.Point _startPoint;
        private System.Windows.Point _endPoint;
        public System.Drawing.Rectangle SelectedRegion { get; private set; }
        private bool _isSelecting;

        public AreaSelectionWindow()
        {
            InitializeComponent();
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            MouseLeftButtonDown += AreaSelectionWindow_MouseLeftButtonDown;
            MouseMove += AreaSelectionWindow_MouseMove;
            MouseLeftButtonUp += AreaSelectionWindow_MouseLeftButtonUp;
            KeyDown += AreaSelectionWindow_KeyDown;
        }

        private void AreaSelectionWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            _isSelecting = true;
            SelectionRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRectangle, _startPoint.X);
            Canvas.SetTop(SelectionRectangle, _startPoint.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            CaptureMouse();
        }

        private void AreaSelectionWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            _endPoint = e.GetPosition(this);
            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double w = Math.Abs(_startPoint.X - _endPoint.X);
            double h = Math.Abs(_startPoint.Y - _endPoint.Y);
            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = w;
            SelectionRectangle.Height = h;
        }

        private void AreaSelectionWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;
            ReleaseMouseCapture();
            _endPoint = e.GetPosition(this);
            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double w = Math.Abs(_startPoint.X - _endPoint.X);
            double h = Math.Abs(_startPoint.Y - _endPoint.Y);

            // PointToScreen lets WPF apply the correct per-monitor DPI conversion.
            var first = PointToScreen(new System.Windows.Point(x, y));
            var second = PointToScreen(new System.Windows.Point(x + w, y + h));
            var physicalRegion = CaptureGeometry.FromScreenPoints(first, second);
            if (physicalRegion.Width < 1 || physicalRegion.Height < 1)
            {
                SelectionRectangle.Visibility = Visibility.Collapsed;
                return;
            }

            SelectedRegion = physicalRegion;
            DialogResult = true;
            Close();
        }

        private void AreaSelectionWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}

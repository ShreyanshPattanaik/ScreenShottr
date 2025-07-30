using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Controls;

namespace ShottrClone
{
    public partial class AreaSelectionWindow : Window
    {
        private System.Windows.Point _startPoint;
        private System.Windows.Point _endPoint;
        public System.Drawing.Rectangle SelectedRegion { get; private set; }
        private bool _isSelecting = false;

        public AreaSelectionWindow()
        {
            InitializeComponent();
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

            // Convert DIPs to physical pixels
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }
            int px = (int)(x * dpiX);
            int py = (int)(y * dpiY);
            int pw = (int)(w * dpiX);
            int ph = (int)(h * dpiY);
            SelectedRegion = new System.Drawing.Rectangle(px, py, pw, ph);
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
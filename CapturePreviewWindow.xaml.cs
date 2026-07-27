using System.Windows;
using System.Windows.Media.Imaging;

namespace ShottrClone;

public partial class CapturePreviewWindow : Window
{
    private readonly Action _copy;
    private readonly Action _save;
    private readonly Action _edit;

    public CapturePreviewWindow(
        BitmapSource preview,
        Action copy,
        Action save,
        Action edit)
    {
        InitializeComponent();
        PreviewImage.Source = preview;
        _copy = copy;
        _save = save;
        _edit = edit;

        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 20);
        Top = Math.Max(workArea.Top, workArea.Bottom - Height - 20);
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => _copy();

    private void Save_Click(object sender, RoutedEventArgs e) => _save();

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        _edit();
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

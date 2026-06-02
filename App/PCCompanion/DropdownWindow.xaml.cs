using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace PCCompanion;

public partial class DropdownWindow : Window
{
    public event Action<int>? Selected;

    private static readonly SolidColorBrush C_HOVER = new(Color.FromRgb(0x2A, 0x3A, 0x4A));
    private static readonly SolidColorBrush C_BG    = new(Color.FromRgb(0x1A, 0x22, 0x2D));
    private static readonly SolidColorBrush C_SEL   = new(Color.FromRgb(0x7C, 0xFF, 0x91));
    private static readonly SolidColorBrush C_TEXT  = new(Colors.White);

    private const int RowH           = 36;
    private const int MaxVisibleRows = 6;

    // Guard against re-entrant Close() calls.
    // Set to true before invoking Selected or calling Close() so that OnDeactivated
    // (which can fire during Selected.Invoke due to WPF message processing) becomes a no-op.
    private bool _isClosing;

    public DropdownWindow(IList<string> items, int selectedIndex, Point screenPt, int width)
    {
        InitializeComponent();

        int visible  = Math.Min(items.Count, MaxVisibleRows);
        Width  = width;
        Height = visible * RowH;
        Left   = screenPt.X;
        Top    = screenPt.Y;

        Scroll.MaxHeight = visible * RowH;

        for (int i = 0; i < items.Count; i++)
        {
            int  idx     = i;
            bool current = i == selectedIndex;

            var row = new TextBlock
            {
                Text              = items[i],
                FontFamily        = new FontFamily("Segoe UI"),
                FontSize          = 9,
                Foreground        = current ? C_SEL : C_TEXT,
                Background        = C_BG,
                Padding           = new Thickness(12, 0, 0, 0),
                Height            = RowH,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor            = Cursors.Hand,
            };

            row.MouseLeftButtonDown += (_, _) =>
            {
                if (_isClosing) return;
                _isClosing = true;   // guard before Selected.Invoke — WPF may fire Deactivated inside
                Selected?.Invoke(idx);
                Close();
            };

            row.MouseEnter += (_, _) => row.Background = C_HOVER;
            row.MouseLeave += (_, _) => row.Background = C_BG;

            ItemsStack.Children.Add(row);
        }
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        Close();
    }
}

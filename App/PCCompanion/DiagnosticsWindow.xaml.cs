using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCCompanion;

public partial class DiagnosticsWindow : Window
{
    private List<DiagnosticsService.Row> _rows = new();

    public DiagnosticsWindow()
    {
        InitializeComponent();
        // Gather off the UI thread (some probes touch DDC/CI, WMI, COM) so the window
        // shows instantly, then populate when ready.
        Loaded += (_, _) => GatherAndShow();
    }

    private void GatherAndShow()
    {
        Task.Run(() =>
        {
            try { return DiagnosticsService.Collect(); }
            catch (Exception ex) { Logger.Log($"Diagnostics collect: {ex.Message}"); return new List<DiagnosticsService.Row>(); }
        })
        .ContinueWith(t =>
        {
            _rows = t.Result ?? new();
            BuildRows();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void BuildRows()
    {
        Rows.Children.Clear();
        foreach (var row in _rows)
        {
            if (row.IsHeader)
            {
                var h = new TextBlock
                {
                    Text       = row.Label,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize   = 13,
                    FontWeight = FontWeights.Bold,
                    Margin     = new Thickness(0, 12, 0, 4),
                };
                h.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                Rows.Children.Add(h);
            }
            else
            {
                var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text              = row.Label,
                    FontFamily        = new FontFamily("Segoe UI"),
                    FontSize          = 12,
                    TextWrapping      = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");

                var val = new TextBlock
                {
                    Text         = row.Value,
                    FontFamily   = new FontFamily("Segoe UI"),
                    FontSize     = 12,
                    TextWrapping = TextWrapping.Wrap,
                };
                val.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                Grid.SetColumn(val, 1);

                g.Children.Add(lbl);
                g.Children.Add(val);
                Rows.Children.Add(g);
            }
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            try { DragMove(); } catch { /* DragMove throws if the button was released */ }
    }

    private void OnClose(object sender, MouseButtonEventArgs e) { e.Handled = true; Close(); }

    private void OnOpenLogs(object sender, MouseButtonEventArgs e)   { e.Handled = true; OpenFolder(AppPaths.Logs); }
    private void OnOpenConfig(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenFolder(AppPaths.Config); }

    private void OnCopy(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        try { System.Windows.Clipboard.SetText(DiagnosticsService.ToText(_rows)); }
        catch (Exception ex) { Logger.Log($"Diagnostics copy: {ex.Message}"); }
    }

    private void OnReset(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var ans = MessageBox.Show(
            "Reset all PC Companion settings to defaults?\n\n" +
            "This clears your audio devices, theme, prayer location, and display options. " +
            "It cannot be undone.",
            "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;

        try
        {
            var file = Path.Combine(AppPaths.Config, "settings.json");
            if (File.Exists(file)) File.Delete(file);
            AppSettings.Invalidate();
            Logger.Log("Settings reset to defaults (Diagnostics).");
            MessageBox.Show(
                "Settings reset to defaults.\nReopen the popup or restart PC Companion to see the changes.",
                "PC Companion", MessageBoxButton.OK, MessageBoxImage.Information);
            GatherAndShow();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not reset settings: {ex.Message}",
                "PC Companion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Log($"OpenFolder: {ex.Message}"); }
    }
}

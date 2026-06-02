// Resolve conflicts between WPF and WinForms/System.Drawing global implicit usings.
// Both are active because UseWPF=true and UseWindowsForms=true coexist (NotifyIcon).
global using Application      = System.Windows.Application;
global using Brush             = System.Windows.Media.Brush;
global using Color             = System.Windows.Media.Color;
global using Colors            = System.Windows.Media.Colors;
global using ColorConverter    = System.Windows.Media.ColorConverter;
global using Cursors           = System.Windows.Input.Cursors;
global using FontFamily        = System.Windows.Media.FontFamily;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using MessageBox        = System.Windows.MessageBox;
// System.Windows.Shapes.Path conflicts with System.IO.Path — prefer IO
global using Path              = System.IO.Path;
global using File              = System.IO.File;
global using Directory         = System.IO.Directory;

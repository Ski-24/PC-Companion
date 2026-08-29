using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PCCompanion;

public partial class App
{
    private const string MutexName = "PCCompanion_SingleInstance";
    private const string PipeName  = "PCCompanion_CmdPipe";

    private Mutex? _mutex;
    private TrayManager? _tray;
    private PopupWindow? _popup;
    private Thread? _pipeThread;
    private volatile bool _shuttingDown;

    // Commands accepted from the CLI / Stream Deck. Returns the normalized command,
    // or null if no recognized action flag is present.
    private static string? ParseCommand(string[] args)
    {
        foreach (var a in args)
        {
            switch (a.ToLowerInvariant())
            {
                case "--show-popup":
                case "--toggle-gopher":
                case "--switch-audio":
                case "--toggle-hdr":
                case "--toggle-auto-hdr":
                case "--toggle-couch-mode":
                case "--toggle-morning-mode":
                    return a.ToLowerInvariant();
            }

            if (a.StartsWith("--adjust-brightness=", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("--adjust-sdr=", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("--set-brightness=", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("--set-sdr=", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("--set-dim=", StringComparison.OrdinalIgnoreCase))
                return a.ToLowerInvariant();
        }
        return null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        string? command = ParseCommand(e.Args);
        bool    startup = e.Args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));

        _mutex = new Mutex(true, MutexName, out bool isNew);
        if (!isNew)
        {
            // Another instance owns the app. Forward the intent to it, then exit —
            // never create a second visible instance.
            //   - an action command  → send it
            //   - plain manual launch → ask the live instance to show its popup
            //   - --startup           → do nothing (it's already running)
            string toSend = command ?? (startup ? "" : "--show-popup");
            if (!string.IsNullOrEmpty(toSend))
                SendCommand(toSend);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Required for Encoding.GetEncoding(1256) and other non-Unicode codepages in .NET 8
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "PC Companion – Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        StateManager.EnsureDefault(AppPaths.GopherState, "Off");
        StateManager.EnsureDefault(AppPaths.AudioState,  "Device2");
        StateManager.EnsureDefault(AppPaths.HdrState,    "Off");

        _popup = new PopupWindow();
        _tray  = new TrayManager(_popup);

        // Listen for commands from `PCCompanion.exe --<cmd>` launched by Stream Deck / CLI.
        _pipeThread = new Thread(PipeServerLoop) { IsBackground = true, Name = "CmdPipe" };
        _pipeThread.Start();

        Logger.Log("PC Companion started.");

        // Quietly check GitHub for a newer release a few seconds after startup (so launch
        // isn't slowed / network isn't hit immediately). Surfaces a tray balloon if found.
        Task.Delay(TimeSpan.FromSeconds(4)).ContinueWith(_ =>
            Dispatcher.BeginInvoke(new Action(() => _tray?.StartupUpdateCheck())));

        // Honor this launch's own intent.
        if (command is not null)
            Dispatcher.BeginInvoke(new Action(async () => await _popup.HandleCommand(command)));
        else if (!startup)
            Dispatcher.BeginInvoke(new Action(() => _popup.ShowPopup())); // manual launch → open popup
        // startup → stay silent in the tray
    }

    // ── IPC: named pipe ───────────────────────────────────────────────────────

    // Server side (runs in the owning instance): accept one command per connection
    // and dispatch it onto the UI thread.
    private void PipeServerLoop()
    {
        while (!_shuttingDown)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                server.WaitForConnection();

                string? cmd;
                // leaveOpen so disposing the reader doesn't pre-empt our explicit Disconnect.
                using (var reader = new StreamReader(server, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true))
                    cmd = reader.ReadLine();

                // CRITICAL: explicitly tear down this connection before recreating the
                // server. Without it the client's buffered message stays re-readable and
                // the next WaitForConnection can return instantly with the SAME data —
                // an intermittent busy-spin that fires the command many times per send.
                server.Disconnect();

                if (string.IsNullOrWhiteSpace(cmd)) continue;

                string c = cmd.Trim();
                Logger.Log($"PipeServer: received '{c}'");
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try { if (_popup is not null) await _popup.HandleCommand(c); }
                    catch (Exception ex) { Logger.Log($"HandleCommand: {ex.Message}"); }
                }));
            }
            catch (Exception ex)
            {
                if (_shuttingDown) break;
                Logger.Log($"PipeServer: {ex.Message}");
                Thread.Sleep(200); // avoid a tight loop if the pipe is briefly unavailable
            }
        }
    }

    // Client side (runs in the transient instance): send one command line and return.
    private static bool SendCommand(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(command);
            Logger.Log($"SendCommand: sent '{command}'");
            return true;
        }
        catch (Exception ex) { Logger.Log($"SendCommand: {ex.Message}"); return false; }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Log("PC Companion exiting.");
        _shuttingDown = true;
        // Never leave the system-wide display gamma dimmed after the app exits.
        DimOverlay.Reset();
        // Unblock the pipe server's WaitForConnection so the thread can exit promptly.
        try { using var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out); c.Connect(100); }
        catch { /* best-effort */ }
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

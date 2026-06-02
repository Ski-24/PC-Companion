using System.Diagnostics;

namespace PCCompanion;

/// <summary>
/// TEMPORARY diagnostic instrumentation for the "random screen dimming" investigation.
///
/// Logs every ACTUAL display write (monitor brightness, SDR boost, HDR toggle, Auto HDR)
/// with full context so we can prove which path — if any — causes the dimming. The stack
/// trace is the key field: it reveals the real trigger (slider release, HDR click, a timer,
/// a ValueChanged, etc.) even when the write runs on a Task.Run worker thread, because the
/// lambda's frame still names the originating handler.
///
/// Grep the log for "DIAG-WRITE". Remove this file and its call sites once the cause is found.
/// </summary>
static class DisplayDiag
{
    /// <param name="method">The write API being invoked (e.g. "BrightnessService.Set").</param>
    /// <param name="value">The value being written (e.g. "37%", "1.0", "toggle from On").</param>
    /// <param name="trigger">Human label for the call site (e.g. "OnBrightnessSliderUp").</param>
    /// <param name="context">Extra flags, e.g. "wasDragging=true" or "auto-reapply".</param>
    public static void LogWrite(string method, string value, string trigger, string context)
    {
        string hdr;
        try { hdr = HdrDetector.IsEnabled() ? "ON" : "OFF"; }
        catch (Exception ex) { hdr = $"ERR({ex.Message})"; }

        // skipFrames=1 drops this method itself; fNeedFileInfo=true adds file:line (Debug PDBs).
        string stack = new StackTrace(1, true).ToString().TrimEnd();

        Logger.Log(
            $"DIAG-WRITE method={method} value={value} trigger={trigger} " +
            $"hdrAtWrite={hdr} {context} thread={Environment.CurrentManagedThreadId}\n{stack}");
    }
}

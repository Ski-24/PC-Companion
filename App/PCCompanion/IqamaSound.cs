using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace PCCompanion;

// Plays the embedded iqama alarm (LogicalName "iqama.mp3") from memory — no temp file.
// One playback at a time; a new Play() cancels any still-ringing one.
static class IqamaSound
{
    private static WaveOutEvent? _output;
    private static IDisposable?  _reader;
    private static readonly object _gate = new();

    // volume: 0.0 (silent) .. 1.0 (full).
    public static void Play(float volume = 1.0f)
    {
        try
        {
            using var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("iqama.mp3");
            if (res is null) { Logger.Log("IqamaSound: embedded iqama.mp3 not found."); return; }

            // Copy to a seekable MemoryStream so the reader owns the data after this method returns.
            var ms = new MemoryStream();
            res.CopyTo(ms);
            ms.Position = 0;

            var reader = new Mp3FileReader(ms);
            var output = new WaveOutEvent();
            output.Init(reader);
            output.Volume = Math.Clamp(volume, 0f, 1f);
            output.PlaybackStopped += (_, _) =>
            {
                try { output.Dispose(); reader.Dispose(); ms.Dispose(); } catch { }
            };

            lock (_gate)
            {
                try { _output?.Stop(); _output?.Dispose(); _reader?.Dispose(); } catch { }
                _output = output;
                _reader = reader;
            }

            output.Play();
            Logger.Log("IqamaSound: playing iqama alarm.");
        }
        catch (Exception ex) { Logger.Log($"IqamaSound.Play: {ex.Message}"); }
    }
}

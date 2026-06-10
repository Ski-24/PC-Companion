using NAudio.Wave;

namespace PCCompanion;

// Loops a user-supplied background/ambience sound file forever (airplane noise, rain, fan, etc.).
// Like GopherManager, this is a static service the card + (future) commands drive. Nothing is
// bundled with the app — the user picks the file in Settings (AppSettings.BackgroundSoundFile).
//
// Looping is done with a sample-accurate LoopStream (no inserted gap/silence at the wrap point),
// so a long continuous recording loops effectively seamlessly. Volume is applied via the
// AudioFileReader's own gain, independent of the Windows system volume.
static class BackgroundSoundPlayer
{
    private static WaveOutEvent? _out;
    private static AudioFileReader? _reader;
    private static LoopStream? _loop;
    private static readonly object _lock = new();

    public static bool IsPlaying
    {
        get { lock (_lock) return _out?.PlaybackState == PlaybackState.Playing; }
    }

    // Start (or restart) looping the given file at the given volume (0.0–1.0).
    public static void Play(string filePath, float volume)
    {
        lock (_lock)
        {
            try
            {
                Teardown();
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    Logger.Log($"BackgroundSoundPlayer.Play: file missing — {filePath}");
                    return;
                }

                _reader = new AudioFileReader(filePath) { Volume = Math.Clamp(volume, 0f, 1f) };
                _loop   = new LoopStream(_reader);
                _out    = new WaveOutEvent();
                _out.Init(_loop);
                _out.Play();
                Logger.Log($"BackgroundSoundPlayer: playing {filePath} @ {volume:F2}");
            }
            catch (Exception ex)
            {
                Logger.Log($"BackgroundSoundPlayer.Play: {ex.Message}");
                Teardown();
            }
        }
    }

    // Stop playback and release the audio device.
    public static void Pause()
    {
        lock (_lock)
        {
            try { Teardown(); Logger.Log("BackgroundSoundPlayer: paused"); }
            catch (Exception ex) { Logger.Log($"BackgroundSoundPlayer.Pause: {ex.Message}"); }
        }
    }

    // Adjust loudness live while playing (0.0–1.0).
    public static void SetVolume(float volume)
    {
        lock (_lock)
        {
            if (_reader is not null)
                _reader.Volume = Math.Clamp(volume, 0f, 1f);
        }
    }

    // App-exit cleanup.
    public static void Dispose()
    {
        lock (_lock) Teardown();
    }

    private static void Teardown()
    {
        _out?.Dispose();    _out = null;
        _loop?.Dispose();   _loop = null;
        _reader?.Dispose(); _reader = null;
    }

    // Standard NAudio looping wrapper: when the inner stream reaches its end, seek back to the
    // start and keep reading, so playback never stops. No gap is inserted — the only possible
    // artifact is a tiny click if the file's last sample doesn't match its first, which on a long
    // recording happens at most once per full playthrough (a future crossfade could erase it).
    private sealed class LoopStream : WaveStream
    {
        private readonly WaveStream _source;

        public LoopStream(WaveStream source) => _source = source;

        public override WaveFormat WaveFormat => _source.WaveFormat;
        public override long Length => _source.Length;
        public override long Position
        {
            get => _source.Position;
            set => _source.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = _source.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    if (_source.Position == 0) break;   // empty/zero-length source — avoid infinite loop
                    _source.Position = 0;               // wrap to the start and keep going
                }
                totalRead += read;
            }
            return totalRead;
        }

        protected override void Dispose(bool disposing)
        {
            // The AudioFileReader (_source) is owned/disposed by BackgroundSoundPlayer.Teardown,
            // so don't dispose it here.
            base.Dispose(disposing);
        }
    }
}

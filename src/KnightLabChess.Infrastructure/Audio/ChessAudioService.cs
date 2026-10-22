using System.IO;
using System.Media;
using KnightLabChess.Core.Audio;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.Infrastructure.Audio;

/// <summary>
/// Audio service providing instant, asynchronous chess sound effect playback.
/// Loads WAV sound files from the content directory or falls back to in-memory procedural generation.
/// </summary>
public class ChessAudioService : IAudioService, IDisposable
{
    private readonly ILogger<ChessAudioService> _logger;
    private readonly string _contentDirectory;
    private readonly Dictionary<ChessSound, byte[]> _rawAudioCache = new();
    private readonly Dictionary<ChessSound, SoundPlayer> _players = new();
    private readonly object _lock = new();

    private bool _isSoundEnabled = true;
    private int _volume = 80;

    public bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set => _isSoundEnabled = value;
    }

    public int Volume
    {
        get => _volume;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (_volume != clamped)
            {
                _volume = clamped;
                ReloadPlayers();
            }
        }
    }

    public void UpdateSettings(bool isEnabled, int volume)
    {
        _isSoundEnabled = isEnabled;
        Volume = volume;
    }

    public ChessAudioService(string contentDirectory, ILogger<ChessAudioService> logger)
    {
        _contentDirectory = contentDirectory;
        _logger = logger;

        InitializeAudio();
    }

    private void InitializeAudio()
    {
        try
        {
            var soundsDir = ResolveSoundsDirectory();
            _logger.LogInformation("Resolved sounds directory at: {Path}", soundsDir ?? "<in-memory>");

            foreach (ChessSound sound in Enum.GetValues<ChessSound>())
            {
                byte[]? wavData = null;

                if (soundsDir is not null)
                {
                    var fileName = GetSoundFileName(sound);
                    var filePath = Path.Combine(soundsDir, fileName);

                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var bytes = File.ReadAllBytes(filePath);
                            if (bytes.Length > 100)
                            {
                                wavData = bytes;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load audio file {Path}. Using procedural fallback.", filePath);
                        }
                    }
                }

                // If not found on disk or corrupt, synthesize in-memory
                if (wavData is null)
                {
                    wavData = SoundEffectGenerator.Generate(sound);
                    // Try to save to disk if directory exists and is writable
                    if (soundsDir is not null)
                    {
                        try
                        {
                            var filePath = Path.Combine(soundsDir, GetSoundFileName(sound));
                            File.WriteAllBytes(filePath, wavData);
                        }
                        catch
                        {
                            // Ignore disk write errors in read-only environments
                        }
                    }
                }

                _rawAudioCache[sound] = wavData;
            }

            ReloadPlayers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chess audio service.");
        }
    }

    private void ReloadPlayers()
    {
        lock (_lock)
        {
            foreach (var kvp in _players)
            {
                try { kvp.Value.Dispose(); } catch { }
            }
            _players.Clear();

            foreach (var kvp in _rawAudioCache)
            {
                try
                {
                    var scaledBytes = SoundEffectGenerator.AdjustVolume(kvp.Value, _volume);
                    var ms = new MemoryStream(scaledBytes);
                    var player = new SoundPlayer(ms);
                    player.LoadAsync();
                    _players[kvp.Key] = player;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create SoundPlayer for {Sound}", kvp.Key);
                }
            }
        }
    }

    public void Play(ChessSound sound)
    {
        if (!_isSoundEnabled || _volume <= 0) return;

        try
        {
            lock (_lock)
            {
                if (_players.TryGetValue(sound, out var player))
                {
                    player.Play();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Audio playback skipped or failed for {Sound}", sound);
        }
    }

    private string? ResolveSoundsDirectory()
    {
        var current = new DirectoryInfo(_contentDirectory);
        while (current is not null)
        {
            var p1 = Path.Combine(current.FullName, "sounds");
            if (Directory.Exists(p1)) return p1;

            var p2 = Path.Combine(current.FullName, "content", "sounds");
            if (Directory.Exists(p2)) return p2;

            current = current.Parent;
        }

        // Try creating content/sounds in content directory
        try
        {
            var target = Path.Combine(_contentDirectory, "content", "sounds");
            Directory.CreateDirectory(target);
            return target;
        }
        catch
        {
            return null;
        }
    }

    private static string GetSoundFileName(ChessSound sound) => sound switch
    {
        ChessSound.Move => "move.wav",
        ChessSound.Capture => "capture.wav",
        ChessSound.Check => "check.wav",
        ChessSound.Castle => "castle.wav",
        ChessSound.Promote => "promote.wav",
        ChessSound.Checkmate => "checkmate.wav",
        ChessSound.GameOver => "game_over.wav",
        ChessSound.PuzzleSolved => "puzzle_solved.wav",
        _ => "move.wav"
    };

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var player in _players.Values)
            {
                try { player.Dispose(); } catch { }
            }
            _players.Clear();
            _rawAudioCache.Clear();
        }
        GC.SuppressFinalize(this);
    }
}

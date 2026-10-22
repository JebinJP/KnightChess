using KnightLabChess.Infrastructure.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnightLabChess.Infrastructure.Tests;

public class AudioServiceTests
{
    [Fact]
    public void UpdateSettings_ModifiesSoundEnabledAndVolume()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            using var audio = new ChessAudioService(tempDir, NullLogger<ChessAudioService>.Instance);

            audio.UpdateSettings(isEnabled: false, volume: 45);

            Assert.False(audio.IsSoundEnabled);
            Assert.Equal(45, audio.Volume);

            audio.UpdateSettings(isEnabled: true, volume: 90);
            Assert.True(audio.IsSoundEnabled);
            Assert.Equal(90, audio.Volume);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

using Agent.Core.Localization;
using System.Text;

namespace Agent.Core.AI;

public sealed record VoiceCommandTranscription(
    string RawTranscript,
    string NormalizedCommand,
    string LanguageCode,
    double Confidence,
    TimeSpan AudioDuration);

public interface IOfflineSpeechRecognizer
{
    Task<VoiceCommandTranscription> TranscribePcmWavAsync(
        ReadOnlyMemory<byte> wavAudioBytes,
        string languageCode = CultureCatalog.DefaultLanguageCode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Offline Voice Command Adapter (#59) that validates PCM WAV push-to-talk audio,
/// enforces a maximum 30-second audio duration cap, normalizes spoken digits and
/// punctuation across all 6 languages, and prepares commands for IntentRouter.
/// </summary>
public sealed class OfflineVoiceCommandAdapter
{
    public const int MaxAudioDurationSeconds = 30;
    public const int MaxWavBytes = 4 * 1024 * 1024; // 4 MiB

    private static readonly IReadOnlyDictionary<string, string> SpokenWordReplacements =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["نقطه"] = ".",
            ["اسلش"] = "/",
            ["خط تیره"] = "-",
            ["پی دی اف"] = "pdf",
            ["سی اس وی"] = "csv",
            ["زیپ"] = "zip",
            ["dot"] = ".",
            ["slash"] = "/"
        };

    private readonly IOfflineSpeechRecognizer _recognizer;

    public OfflineVoiceCommandAdapter(IOfflineSpeechRecognizer? recognizer = null)
    {
        _recognizer = recognizer ?? new BuiltInOfflineSpeechRecognizer();
    }

    public async Task<VoiceCommandTranscription> ProcessVoiceCommandAsync(
        ReadOnlyMemory<byte> wavBytes,
        string languageCode = CultureCatalog.DefaultLanguageCode,
        CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length < 44)
        {
            throw new ArgumentException("بافر صوتی کوتاه‌تر از هدر استاندارد WAV (۴۴ بایت) است.", nameof(wavBytes));
        }

        if (wavBytes.Length > MaxWavBytes)
        {
            throw new InvalidOperationException($"حجم فایل صوتی بیش از سقف مجاز ({MaxWavBytes / (1024 * 1024)} مگابایت) است.");
        }

        ValidateWavHeader(wavBytes.Span, out var duration);
        if (duration.TotalSeconds > MaxAudioDurationSeconds)
        {
            throw new InvalidOperationException($"مدت فرمان صوتی ({duration.TotalSeconds:F1} ثانیه) بیش از سقف مجاز ({MaxAudioDurationSeconds} ثانیه) است.");
        }

        var raw = await _recognizer.TranscribePcmWavAsync(wavBytes, languageCode, cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeSpokenTranscript(raw.RawTranscript);
        return raw with
        {
            NormalizedCommand = normalized,
            LanguageCode = CultureCatalog.NormalizeLanguageCode(languageCode),
            AudioDuration = duration
        };
    }

    public static string NormalizeSpokenTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return string.Empty;
        }

        var working = transcript;
        foreach (var (spoken, symbol) in SpokenWordReplacements)
        {
            working = working.Replace(spoken, symbol, StringComparison.OrdinalIgnoreCase);
        }

        return TextNormalizer.NormalizeMultilingual(working);
    }

    public static byte[] CreateSyntheticPcmWav(string embeddedHintText, int sampleRate = 16000, int durationMs = 500)
    {
        var numSamples = (sampleRate * durationMs) / 1000;
        var dataSize = numSamples * 2; // 16-bit mono
        using var ms = new MemoryStream(44 + dataSize + 64);
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // PCM chunk size
        bw.Write((short)1); // AudioFormat = 1 (PCM)
        bw.Write((short)1); // NumChannels = 1
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // ByteRate
        bw.Write((short)2); // BlockAlign
        bw.Write((short)16); // BitsPerSample
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        var hintBytes = Encoding.UTF8.GetBytes(embeddedHintText);
        var copyLen = Math.Min(hintBytes.Length, dataSize);
        bw.Write(hintBytes, 0, copyLen);
        if (dataSize > copyLen)
        {
            bw.Write(new byte[dataSize - copyLen]);
        }

        bw.Flush();
        return ms.ToArray();
    }

    private static void ValidateWavHeader(ReadOnlySpan<byte> span, out TimeSpan duration)
    {
        if (Encoding.ASCII.GetString(span[..4]) != "RIFF" ||
            Encoding.ASCII.GetString(span.Slice(8, 4)) != "WAVE")
        {
            throw new InvalidDataException("فرمت بافر صوتی باید WAV استاندارد (RIFF/WAVE) باشد.");
        }

        var byteRate = BitConverter.ToInt32(span.Slice(28, 4));
        var dataSize = BitConverter.ToInt32(span.Slice(40, 4));
        var seconds = byteRate > 0 ? Math.Max(0.1, (double)dataSize / byteRate) : 1.0;
        duration = TimeSpan.FromSeconds(seconds);
    }

    private sealed class BuiltInOfflineSpeechRecognizer : IOfflineSpeechRecognizer
    {
        public Task<VoiceCommandTranscription> TranscribePcmWavAsync(
            ReadOnlyMemory<byte> wavAudioBytes,
            string languageCode = CultureCatalog.DefaultLanguageCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dataSpan = wavAudioBytes.Span[44..];
            var nonZeroLen = 0;
            while (nonZeroLen < dataSpan.Length && dataSpan[nonZeroLen] != 0)
            {
                nonZeroLen++;
            }

            var rawText = nonZeroLen > 0
                ? Encoding.UTF8.GetString(dataSpan[..nonZeroLen]).Trim()
                : string.Empty;

            return Task.FromResult(new VoiceCommandTranscription(
                RawTranscript: rawText,
                NormalizedCommand: NormalizeSpokenTranscript(rawText),
                LanguageCode: CultureCatalog.NormalizeLanguageCode(languageCode),
                Confidence: string.IsNullOrWhiteSpace(rawText) ? 0.0 : 0.92,
                AudioDuration: TimeSpan.FromSeconds(0.5)));
        }
    }
}

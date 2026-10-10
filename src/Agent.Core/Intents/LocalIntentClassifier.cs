using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agent.Core.Intents;

public enum IntentLabel
{
    CopyFiles,
    MoveFiles,
    DeleteFiles,
    OpenApp,
    Other
}

public sealed record IntentClassification(IntentLabel Label, double Confidence, double Margin);

/// <summary>
/// A tiny, dependency-free multinomial Naive Bayes classifier over word and
/// character n-grams. It is trained only from bundled examples and never learns
/// from user files or sends text off-device.
/// </summary>
public sealed class LocalIntentClassifier
{
    private const string TrainingResource = "Agent.Core.Intents.Data.intent-examples-fa.json";
    private const double Smoothing = 0.35;
    private const int MaximumTextLength = 512;
    private static readonly int[] CharacterNgramLengths = [2, 3, 4];
    private static readonly Regex WordPattern = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IntentLabel[] _labels = Enum.GetValues<IntentLabel>();
    private readonly Dictionary<IntentLabel, Dictionary<string, int>> _featureCounts = new();
    private readonly Dictionary<IntentLabel, int> _totalFeatureCounts = new();
    private readonly HashSet<string> _vocabulary = new(StringComparer.Ordinal);
    private int _exampleCount;

    public LocalIntentClassifier()
    {
        TrainFromBundledExamples();
    }

    public IntentClassification Classify(string? text)
    {
        var input = text ?? string.Empty;
        if (input.Length > MaximumTextLength)
        {
            input = input[..MaximumTextLength];
        }

        var normalized = IntentRouter.NormalizePersian(input);

        var features = ExtractFeatures(normalized);
        if (features.Count == 0 || _exampleCount == 0)
        {
            return new IntentClassification(IntentLabel.Other, 0, 0);
        }

        var vocabularySize = Math.Max(1, _vocabulary.Count + 1);
        var scores = new Dictionary<IntentLabel, double>(_labels.Length);

        foreach (var label in _labels)
        {
            var counts = _featureCounts[label];
            var totalCount = _totalFeatureCounts[label];
            var score = 0d; // Uniform class prior; sample imbalance cannot dominate.
            var denominator = totalCount + Smoothing * vocabularySize;

            foreach (var (feature, frequency) in features)
            {
                counts.TryGetValue(feature, out var featureCount);
                score += frequency * Math.Log((featureCount + Smoothing) / denominator);
            }

            scores[label] = score;
        }

        var ranked = scores.OrderByDescending(pair => pair.Value).ToArray();
        var maximum = ranked[0].Value;
        var exponentials = ranked.Select(pair => Math.Exp(pair.Value - maximum)).ToArray();
        var normalization = exponentials.Sum();
        var confidence = exponentials[0] / normalization;
        var secondConfidence = exponentials.Length > 1 ? exponentials[1] / normalization : 0;

        return new IntentClassification(ranked[0].Key, confidence, confidence - secondConfidence);
    }

    private void TrainFromBundledExamples()
    {
        using var stream = typeof(LocalIntentClassifier).Assembly.GetManifestResourceStream(TrainingResource)
            ?? throw new InvalidOperationException("دادهٔ دسته‌بند محلی در بستهٔ برنامه پیدا نشد.");

        var examples = JsonSerializer.Deserialize<IntentTrainingExample[]>(stream, JsonOptions) ?? Array.Empty<IntentTrainingExample>();
        if (examples.Length == 0)
        {
            throw new InvalidOperationException("مجموعهٔ نمونه‌های دسته‌بند خالی است.");
        }

        foreach (var label in _labels)
        {
            _featureCounts[label] = new Dictionary<string, int>(StringComparer.Ordinal);
            _totalFeatureCounts[label] = 0;
        }

        foreach (var example in examples)
        {
            if (!Enum.TryParse(example.Intent, ignoreCase: true, out IntentLabel label) ||
                !_featureCounts.ContainsKey(label))
            {
                throw new InvalidOperationException($"برچسب نامعتبر در دادهٔ دسته‌بند: {example.Intent}");
            }

            var normalized = IntentRouter.NormalizePersian(example.Text);
            foreach (var (feature, frequency) in ExtractFeatures(normalized))
            {
                _featureCounts[label].TryGetValue(feature, out var currentCount);
                _featureCounts[label][feature] = currentCount + frequency;
                _totalFeatureCounts[label] += frequency;
                _vocabulary.Add(feature);
            }

            _exampleCount++;
        }
    }

    private static Dictionary<string, int> ExtractFeatures(string normalizedText)
    {
        var features = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in WordPattern.Matches(normalizedText))
        {
            var word = match.Value;
            AddFeature(features, $"w:{word}");

            // Character n-grams tolerate common suffixes, spacing, and minor typos.
            var paddedWord = $"^{word}$";
            foreach (var ngramLength in CharacterNgramLengths)
            {
                for (var index = 0; index <= paddedWord.Length - ngramLength; index++)
                {
                    AddFeature(features, $"c{ngramLength}:{paddedWord.Substring(index, ngramLength)}");
                }
            }
        }

        return features;
    }

    private static void AddFeature(Dictionary<string, int> features, string feature)
    {
        features.TryGetValue(feature, out var currentCount);
        features[feature] = currentCount + 1;
    }

}

public sealed record IntentTrainingExample(string Intent, string Text);

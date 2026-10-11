using Agent.Core.Tools;
using System.Text.Json;

namespace Agent.Core.Storage;

/// <summary>
/// Allows users or enterprise administrators to register custom or internal
/// HTTPS AI tool endpoints locally without modifying the embedded 200-tool catalog.
/// </summary>
public sealed class CustomAiToolStore
{
    public const int MaximumCustomTools = 50;
    private const int MaximumFileBytes = 128 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CustomAiToolStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboticAgent",
            "custom-ai-tools.json");
    }

    public async Task<IReadOnlyList<AiTool>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AiTool>> AddOrUpdateAsync(AiTool customTool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customTool);
        ValidateCustomTool(customTool);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = (await LoadInternalAsync(cancellationToken).ConfigureAwait(false)).ToList();
            existing.RemoveAll(item => string.Equals(item.Id, customTool.Id, StringComparison.OrdinalIgnoreCase));

            if (existing.Count >= MaximumCustomTools)
            {
                throw new InvalidOperationException($"حداکثر {MaximumCustomTools} ابزار سفارشی قابل ثبت است.");
            }

            var category = AiToolCatalog.Categories.FirstOrDefault(cat =>
                string.Equals(cat.Id, customTool.CategoryId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(cat.Id, "all", StringComparison.OrdinalIgnoreCase))
                ?? AiToolCatalog.Categories.First(cat => !string.Equals(cat.Id, "all", StringComparison.OrdinalIgnoreCase));

            var normalizedTool = customTool with
            {
                Id = customTool.Id.Trim(),
                CategoryId = category.Id,
                Name = customTool.Name.Trim(),
                DescriptionFa = customTool.DescriptionFa.Trim(),
                LoginUrl = customTool.LoginUrl.Trim(),
                IconText = customTool.IconText.Trim().ToUpperInvariant(),
                CategoryName = category.Name,
                CategoryNames = category.Names,
                CategoryIconGeometry = category.IconGeometry,
                IsCustomTool = true
            };

            existing.Add(normalizedTool);
            await SaveInternalAsync(existing, cancellationToken).ConfigureAwait(false);
            return existing;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AiTool>> RemoveAsync(string toolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = (await LoadInternalAsync(cancellationToken).ConfigureAwait(false)).ToList();
            existing.RemoveAll(item => string.Equals(item.Id, toolId, StringComparison.OrdinalIgnoreCase));
            await SaveInternalAsync(existing, cancellationToken).ConfigureAwait(false);
            return existing;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void ValidateCustomTool(AiTool tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Id) ||
            string.IsNullOrWhiteSpace(tool.Name) ||
            string.IsNullOrWhiteSpace(tool.DescriptionFa) ||
            string.IsNullOrWhiteSpace(tool.IconText) ||
            tool.IconText.Trim().Length > 6)
        {
            throw new ArgumentException("شناسه، نام، توضیح و نشان حروفی (حداکثر ۶ نویسه) برای ابزار سفارشی الزامی است.", nameof(tool));
        }

        if (!AiToolCatalog.TryGetSafeWebUri(tool.LoginUrl, out _))
        {
            throw new ArgumentException("نشانی ابزار سفارشی باید HTTPS معتبر باشد.", nameof(tool));
        }
    }

    private async Task<IReadOnlyList<AiTool>> LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath) || new FileInfo(_filePath).Length > MaximumFileBytes)
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var tools = JsonSerializer.Deserialize<List<AiTool>>(json, JsonOptions) ?? [];
            return tools
                .Where(tool =>
                    !string.IsNullOrWhiteSpace(tool.Id) &&
                    !string.IsNullOrWhiteSpace(tool.Name) &&
                    !string.IsNullOrWhiteSpace(tool.DescriptionFa) &&
                    !string.IsNullOrWhiteSpace(tool.IconText) &&
                    AiToolCatalog.TryGetSafeWebUri(tool.LoginUrl, out _))
                .Select(tool => tool with { IsCustomTool = true })
                .Take(MaximumCustomTools)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveInternalAsync(IReadOnlyList<AiTool> tools, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("مسیر ذخیرهٔ ابزارهای سفارشی معتبر نیست.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(tools, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

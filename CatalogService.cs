using System.Text.Json;

namespace DownloadHub;

/// <summary>Загрузка и сохранение каталога в формате JSON.</summary>
public static class CatalogService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<Catalog> LoadFromFileAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл каталога не найден: {path}", path);

        await using var stream = File.OpenRead(path);
        var catalog = await JsonSerializer.DeserializeAsync<Catalog>(stream, Options, ct);
        return Validate(catalog);
    }

    public static async Task<Catalog> LoadFromUrlAsync(HttpClient http, string url, CancellationToken ct = default)
    {
        await using var stream = await http.GetStreamAsync(url, ct);
        var catalog = await JsonSerializer.DeserializeAsync<Catalog>(stream, Options, ct);
        return Validate(catalog);
    }

    public static async Task SaveAsync(Catalog catalog, string path, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, catalog, Options, ct);
    }

    private static Catalog Validate(Catalog? catalog)
    {
        if (catalog is null)
            throw new InvalidDataException("Каталог пустой или имеет неверный формат JSON.");

        catalog.Items ??= new List<CatalogItem>();
        foreach (var item in catalog.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                item.Name = "(без названия)";
            if (string.IsNullOrWhiteSpace(item.Category))
                item.Category = "Прочее";
        }

        return catalog;
    }
}

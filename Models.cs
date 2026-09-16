using System.Text.Json.Serialization;

namespace DownloadHub;

/// <summary>Корневой объект файла catalog.json.</summary>
public sealed class Catalog
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("updated")]
    public string? Updated { get; set; }

    [JsonPropertyName("items")]
    public List<CatalogItem> Items { get; set; } = new();
}

/// <summary>Одна позиция каталога.</summary>
public sealed class CatalogItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Прочее";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    /// <summary>Прямая ссылка на файл.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    /// <summary>Страница проекта (открывается в браузере).</summary>
    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    /// <summary>Имя, под которым сохранить файл. Если пусто — берётся из URL.</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Необязательная контрольная сумма для проверки после скачивания.</summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>Имя файла назначения с запасным вариантом из URL.</summary>
    public string ResolveFileName()
    {
        if (!string.IsNullOrWhiteSpace(FileName))
            return FileName!;

        try
        {
            var name = Path.GetFileName(new Uri(Url).LocalPath);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch (UriFormatException) { /* падаем в вариант ниже */ }

        var safe = string.Join("_", Name.Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(safe) ? Id + ".bin" : safe + ".bin";
    }

    public override string ToString() => Name;
}

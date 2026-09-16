using System.Security.Cryptography;

namespace DownloadHub;

public readonly record struct DownloadProgress(long Received, long Total)
{
    public int Percent => Total > 0 ? (int)Math.Clamp(Received * 100 / Total, 0, 100) : 0;
}

/// <summary>Скачивание файлов по прямой ссылке с отчётом о прогрессе.</summary>
public sealed class Downloader
{
    private readonly HttpClient _http;

    public Downloader(HttpClient http) => _http = http;

    /// <summary>Скачивает элемент каталога в указанную папку и возвращает путь к файлу.</summary>
    public async Task<string> DownloadAsync(
        CatalogItem item,
        string targetDir,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(item.Url))
            throw new InvalidOperationException("У элемента не указана ссылка (url).");

        Directory.CreateDirectory(targetDir);

        var finalPath = Path.Combine(targetDir, item.ResolveFileName());
        var tempPath = finalPath + ".part";

        using var response = await _http.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? item.SizeBytes;
        var received = 0L;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                progress?.Report(new DownloadProgress(received, total));
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Sha256))
        {
            var actual = await ComputeSha256Async(tempPath, ct);
            if (!actual.Equals(item.Sha256!.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                throw new InvalidDataException(
                    $"Контрольная сумма не совпала.\nОжидалось: {item.Sha256}\nПолучено:  {actual}");
            }
        }

        if (File.Exists(finalPath))
            File.Delete(finalPath);
        File.Move(tempPath, finalPath);

        return finalPath;
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

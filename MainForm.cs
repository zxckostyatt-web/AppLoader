using System.Diagnostics;
using System.Text;

namespace DownloadHub;

public sealed class MainForm : Form
{
    private const string AllCategories = "Все категории";

    private readonly HttpClient _http;
    private readonly Downloader _downloader;

    private readonly ListBox _categories = new();
    private readonly ListView _list = new();
    private readonly TextBox _search = new();
    private readonly TextBox _details = new();
    private readonly Button _downloadButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _homepageButton = new();
    private readonly ProgressBar _progress = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _status = new();

    private Catalog _catalog = new();
    private CancellationTokenSource? _cts;

    /// <summary>Каталог загружается отсюда при каждом старте/обновлении.</summary>
    private const string CatalogUrl =
        "https://raw.githubusercontent.com/zxckostyatt-web/AppLoader/refs/heads/main/catalog.json";

    /// <summary>Локальная копия — подстраховка, если сети нет.</summary>
    private readonly string _catalogCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadHub", "catalog.json");

    private readonly string _downloadDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads", "DownloadHub");

    public MainForm()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DownloadHub/1.0");
        _downloader = new Downloader(_http);

        BuildUi();
        Load += async (_, _) => await LoadCatalogAsync();
    }

    // ---------- интерфейс ----------

    private void BuildUi()
    {
        Text = "DownloadHub — каталог программ";
        MinimumSize = new Size(900, 560);
        Size = new Size(1080, 680);
        StartPosition = FormStartPosition.CenterScreen;

        // Нижняя панель с описанием и кнопками
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 170, Padding = new Padding(10) };

        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Dock = DockStyle.Fill;
        _details.BorderStyle = BorderStyle.FixedSingle;
        _details.BackColor = SystemColors.Window;
        _details.Text = "Выберите элемент из списка.";

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0)
        };

        _downloadButton.Text = "⬇  Скачать";
        _downloadButton.Width = 130;
        _downloadButton.Height = 30;
        _downloadButton.Enabled = false;
        _downloadButton.Click += async (_, _) => await DownloadSelectedAsync();

        _cancelButton.Text = "Отмена";
        _cancelButton.Width = 90;
        _cancelButton.Height = 30;
        _cancelButton.Enabled = false;
        _cancelButton.Click += (_, _) => _cts?.Cancel();

        _homepageButton.Text = "Страница проекта";
        _homepageButton.Width = 150;
        _homepageButton.Height = 30;
        _homepageButton.Enabled = false;
        _homepageButton.Click += (_, _) => OpenHomepage();

        _progress.Width = 300;
        _progress.Height = 24;
        _progress.Margin = new Padding(12, 3, 0, 0);

        actions.Controls.AddRange(new Control[] { _downloadButton, _cancelButton, _homepageButton, _progress });

        bottom.Controls.Add(_details);
        bottom.Controls.Add(actions);

        // Список категорий слева
        _categories.Dock = DockStyle.Left;
        _categories.Width = 200;
        _categories.IntegralHeight = false;
        _categories.BorderStyle = BorderStyle.FixedSingle;
        _categories.SelectedIndexChanged += (_, _) => RefreshList();

        // Основной список
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.GridLines = true;
        _list.Columns.Add("Название", 280);
        _list.Columns.Add("Категория", 150);
        _list.Columns.Add("Версия", 90);
        _list.Columns.Add("Размер", 90);
        _list.Columns.Add("Автор", 140);
        _list.SelectedIndexChanged += (_, _) => ShowDetails();
        _list.DoubleClick += async (_, _) => await DownloadSelectedAsync();

        // Верхняя панель инструментов
        var top = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(10, 9, 10, 9) };

        _search.Dock = DockStyle.Left;
        _search.Width = 320;
        _search.PlaceholderText = "Поиск по названию, описанию, тегам…";
        _search.TextChanged += (_, _) => RefreshList();

        var reload = new Button { Text = "Обновить каталог", Width = 140, Dock = DockStyle.Right };
        reload.Click += async (_, _) => await LoadCatalogAsync();

        var openFolder = new Button { Text = "Папка загрузок", Width = 130, Dock = DockStyle.Right };
        openFolder.Click += (_, _) => OpenDownloadsFolder();

        top.Controls.Add(_search);
        top.Controls.Add(openFolder);
        top.Controls.Add(reload);

        // Статус-строка
        _status.Spring = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _statusStrip.Items.Add(_status);
        _statusStrip.Dock = DockStyle.Bottom;

        // Порядок важен: Fill добавляем первым
        Controls.Add(_list);
        Controls.Add(bottom);
        Controls.Add(_categories);
        Controls.Add(_statusStrip);
        Controls.Add(top);
    }

    // ---------- каталог ----------

    private async Task LoadCatalogAsync()
    {
        try
        {
            SetStatus("Загрузка каталога с GitHub…");
            _catalog = await CatalogService.LoadFromUrlAsync(_http, CatalogUrl);

            // Обновили кэш, чтобы приложение работало и без сети в следующий раз.
            try { await CatalogService.SaveAsync(_catalog, _catalogCachePath); }
            catch { /* кэш необязателен, не мешаем основному сценарию */ }

            FillCategories();
            RefreshList();
            SetStatus($"Загружено элементов: {_catalog.Items.Count}. Источник: GitHub.");
        }
        catch (Exception ex)
        {
            SetStatus("Нет сети или GitHub недоступен — пробуем локальный кэш…");
            await LoadCatalogFromCacheAsync(ex);
        }
    }

    private async Task LoadCatalogFromCacheAsync(Exception networkError)
    {
        try
        {
            _catalog = await CatalogService.LoadFromFileAsync(_catalogCachePath);
            FillCategories();
            RefreshList();
            SetStatus($"Загружено из локального кэша ({_catalogCachePath}). Элементов: {_catalog.Items.Count}");
        }
        catch (Exception cacheEx)
        {
            SetStatus("Каталог недоступен.");
            MessageBox.Show(this,
                $"Не удалось загрузить каталог по сети:\n{networkError.Message}\n\n" +
                $"Локальный кэш тоже недоступен:\n{cacheEx.Message}",
                "Ошибка каталога", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void FillCategories()
    {
        var selected = _categories.SelectedItem as string;

        _categories.BeginUpdate();
        _categories.Items.Clear();
        _categories.Items.Add(AllCategories);
        foreach (var category in _catalog.Items
                     .Select(i => i.Category)
                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(c => c, StringComparer.CurrentCulture))
        {
            _categories.Items.Add(category);
        }
        _categories.EndUpdate();

        var index = selected is null ? 0 : _categories.Items.IndexOf(selected);
        _categories.SelectedIndex = index >= 0 ? index : 0;
    }

    private void RefreshList()
    {
        var query = _search.Text.Trim();
        var category = _categories.SelectedItem as string ?? AllCategories;

        var filtered = _catalog.Items.Where(item =>
        {
            if (category != AllCategories &&
                !string.Equals(item.Category, category, StringComparison.CurrentCultureIgnoreCase))
                return false;

            if (query.Length == 0)
                return true;

            return Contains(item.Name, query)
                   || Contains(item.Description, query)
                   || Contains(item.Author, query)
                   || item.Tags.Any(t => Contains(t, query));
        });

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in filtered.OrderBy(i => i.Name, StringComparer.CurrentCulture))
        {
            var row = new ListViewItem(new[]
            {
                item.Name,
                item.Category,
                item.Version,
                FormatSize(item.SizeBytes),
                item.Author
            })
            { Tag = item };
            _list.Items.Add(row);
        }
        _list.EndUpdate();

        ShowDetails();
    }

    private static bool Contains(string? source, string query) =>
        source is not null && source.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    // ---------- детали и загрузка ----------

    private CatalogItem? Selected =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as CatalogItem : null;

    private void ShowDetails()
    {
        var item = Selected;
        var busy = _cts is not null;

        if (item is null)
        {
            _details.Text = "Выберите элемент из списка.";
            _downloadButton.Enabled = false;
            _homepageButton.Enabled = false;
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(item.Name + (string.IsNullOrWhiteSpace(item.Version) ? "" : $"  v{item.Version}"));
        sb.AppendLine(new string('-', 60));
        sb.AppendLine(item.Description);
        sb.AppendLine();
        sb.AppendLine($"Категория: {item.Category}");
        if (!string.IsNullOrWhiteSpace(item.Author)) sb.AppendLine($"Автор: {item.Author}");
        if (item.Tags.Count > 0) sb.AppendLine($"Теги: {string.Join(", ", item.Tags)}");
        sb.AppendLine($"Размер: {FormatSize(item.SizeBytes)}");
        sb.AppendLine($"Файл: {item.ResolveFileName()}");
        sb.AppendLine($"Ссылка: {item.Url}");
        if (!string.IsNullOrWhiteSpace(item.Sha256)) sb.AppendLine($"SHA-256: {item.Sha256}");

        _details.Text = sb.ToString();
        _downloadButton.Enabled = !busy && !string.IsNullOrWhiteSpace(item.Url);
        _homepageButton.Enabled = !string.IsNullOrWhiteSpace(item.Homepage);
    }

    private async Task DownloadSelectedAsync()
    {
        var item = Selected;
        if (item is null || _cts is not null)
            return;

        _cts = new CancellationTokenSource();
        _downloadButton.Enabled = false;
        _cancelButton.Enabled = true;
        _progress.Value = 0;

        var progress = new Progress<DownloadProgress>(p =>
        {
            _progress.Value = p.Percent;
            SetStatus($"Скачивание «{item.Name}» — {FormatSize(p.Received)} из {FormatSize(p.Total)} ({p.Percent}%)");
        });

        try
        {
            var path = await _downloader.DownloadAsync(item, _downloadDir, progress, _cts.Token);
            _progress.Value = 100;
            SetStatus($"Готово: {path}");

            if (MessageBox.Show(this, $"Файл сохранён:\n{path}\n\nОткрыть папку?",
                    "Загрузка завершена", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                SelectInExplorer(path);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Загрузка отменена.");
            _progress.Value = 0;
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка загрузки.");
            _progress.Value = 0;
            MessageBox.Show(this, ex.Message, "Ошибка загрузки",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _cancelButton.Enabled = false;
            ShowDetails();
        }
    }

    // ---------- вспомогательное ----------

    private void OpenHomepage()
    {
        var url = Selected?.Homepage;
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось открыть ссылку",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenDownloadsFolder()
    {
        Directory.CreateDirectory(_downloadDir);
        Process.Start(new ProcessStartInfo(_downloadDir) { UseShellExecute = true });
    }

    private static void SelectInExplorer(string path)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private void SetStatus(string text) => _status.Text = text;

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}

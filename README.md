# DownloadHub

Настольное приложение на C# (WinForms, .NET 8) — каталог программ с загрузкой файлов.

## Возможности

- Каталог берётся из `catalog.json` (локально или по URL)
- Фильтр по категориям + поиск по названию, описанию, автору и тегам
- Скачивание с прогрессом, отменой и докачкой во временный `.part`-файл
- Проверка SHA-256 после загрузки (если указана в каталоге)
- Файлы сохраняются в `%USERPROFILE%\Downloads\DownloadHub`

## Сборка и запуск

Нужен .NET SDK 8 и Windows.

```bash
cd DownloadHub
dotnet restore
dotnet run
```

Сборка одного exe:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Структура проекта

| Файл | Назначение |
|---|---|
| `Program.cs` | точка входа |
| `MainForm.cs` | окно: список, поиск, детали, кнопки |
| `Models.cs` | классы `Catalog` и `CatalogItem` |
| `CatalogService.cs` | чтение/запись JSON |
| `Downloader.cs` | HTTP-загрузка с прогрессом и SHA-256 |
| `Prompt.cs` | диалог ввода строки |
| `catalog.json` | сам каталог |

## Формат catalog.json

```json
{
  "version": 1,
  "updated": "2026-09-16",
  "items": [
    {
      "id": "unique-id",
      "name": "Название",
      "category": "Утилиты",
      "description": "Описание в одну-две строки",
      "version": "1.0",
      "author": "Автор",
      "url": "https://server/file.exe",
      "homepage": "https://project.site",
      "fileName": "file.exe",
      "sizeBytes": 1048576,
      "sha256": "abc123…",
      "tags": ["тег1", "тег2"]
    }
  ]
}
```

Обязательны только `name` и `url`. Поля `fileName`, `sha256`, `homepage`, `tags` необязательны:
если `fileName` пустое, имя берётся из URL; если указан `sha256`, файл с несовпавшей суммой удаляется.

Кнопка «Каталог по URL» загружает JSON с сервера и перезаписывает локальный `catalog.json` —
так каталог можно обновлять централизованно, не пересобирая приложение.

## Замечание

В примере каталога стоят ссылки-заглушки на `example.com` — подставьте свои.
Размещайте только те файлы, на распространение которых у вас есть право; читы в сетевых
играх нарушают правила сервисов и приводят к блокировке аккаунта.

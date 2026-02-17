# RoslynMcp

MCP-сервер для помощи агенту при рефакторинге C#: доступ к **Roslyn** (синтаксические деревья, семантика, символы, find usages, rename, code actions). Цель — стабильный тул без глюков (альтернатива Bifrost).

**Текущий статус:** скелет (один инструмент-заглушка `roslyn_ping`). Дальше: `find_usages`, `rename`, `get_symbol_at_position`, `get_document_symbols` и т.п.

## Требования

- .NET 10

## Сборка и запуск

```bash
cd roslyn-mcp
dotnet build
dotnet run
```

Сервер работает по **stdio** (MCP-клиент запускает процесс и общается через stdin/stdout).

## Публикация exe (для MCP в Cursor/IDE)

```bash
dotnet publish -c Release -r win-x64 --self-contained -o publish
```

В конфиге MCP укажи **command** — путь к `RoslynMcp.exe` в папке `publish`. Рабочая директория должна быть корнем репозитория или solution, чтобы Roslyn мог открыть проекты.

## Дальнейшие инструменты (план)

- `find_usages` — все использования символа
- `rename` — переименование с учётом семантики
- `get_symbol_at_position` — символ в позиции (файл + строка/столбец)
- `get_document_symbols` — структура документа (классы, методы, поля)

## Лицензия

Планируется open source (лицензия уточняется).

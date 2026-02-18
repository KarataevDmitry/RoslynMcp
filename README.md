# RoslynMcp

MCP-сервер для помощи агенту при рефакторинге C#: доступ к **Roslyn** (синтаксические деревья, семантика, символы, find usages, rename, code actions). Цель — стабильный тул без глюков (альтернатива Bifrost).

**Текущий статус:** работают `roslyn_get_document_symbols`, `roslyn_get_symbol_at_position`, `roslyn_find_usages`, `roslyn_rename`, `roslyn_get_code_actions`, `roslyn_apply_code_action`.

## Требования

- .NET 10

## Сборка

```bash
cd roslyn-mcp
dotnet build
dotnet run
```

## Публикация exe (для MCP в Cursor)

Чтобы Cursor запускал MCP из **exe**, а не из проекта, сборка не будет блокироваться запущенным процессом.

**Рекомендуется** — самодостаточная сборка (рантайм в папке, не зависит от установленного .NET в системе):

```bash
cd roslyn-mcp
dotnet publish -c Release -r win-x64 --self-contained -o publish
```

Альтернатива — без рантайма (нужен установленный .NET 10 x64 в системе):

```bash
dotnet publish -c Release -o publish
```

Exe появится в `roslyn-mcp/publish/RoslynMcp.exe`. В конфиге MCP в Cursor укажи этот exe (command — полный путь к нему, args — `[]`).

Обновить exe после правок: снова выполни ту же команду publish, затем перезапусти MCP или Cursor.

Сервер работает по **stdio**. Для `roslyn_get_document_symbols` достаточно пути к .cs файлу; для будущих find_usages/rename рабочая директория — корень репо или solution.

## Инструменты (tools)

| Инструмент | Описание |
|------------|----------|
| `roslyn_ping` | Проверка доступности сервера. |
| `roslyn_get_document_symbols` | Структура C# файла: namespace, class, method, property, field, enum, enum_member, delegate с номерами строк. Параметр: `file_path`. |
| `roslyn_get_symbol_at_position` | Символ в позиции (файл + строка + столбец, 1-based): kind и имя. Опционально `solution_or_project_path` — тогда в ответе Qualified (полное имя). Параметры: `file_path`, `line`, `column`. |
| `roslyn_find_usages` | Все ссылки на символ в solution/project. В выводе: квалифицированное имя (FullyQualifiedFormat), место определения (Definition:), затем список ссылок. Параметры: `solution_or_project_path`, `file_path`, `line`, `column` (на идентификаторе). |
| `roslyn_rename` | Переименование символа по solution. Параметры: `solution_or_project_path`, `file_path`, `line`, `column`, `new_name`, опционально `apply` (превью/запись), `rename_in_comments`, `rename_in_strings`, `rename_overloads`, `rename_file` (аналог опций в VS). |
| `roslyn_get_code_actions` | Список Quick Actions / рефакторингов в позиции (как лампочка в VS). Параметры: `solution_or_project_path`, `file_path`, `line`, `column`. Возвращает нумерованный список. |
| `roslyn_apply_code_action` | Применить выбранное code action. Параметры: `solution_or_project_path`, `file_path`, `line`, `column`, `action_index` (0-based). Опционально `fix_all_scope`: `"document"` \| `"project"` \| `"solution"` — Fix all в файле/проекте/решении (только для code fixes с поддержкой Fix All). |

## Лицензия

Планируется open source (лицензия уточняется).

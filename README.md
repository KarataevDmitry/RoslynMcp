# RoslynMcp

MCP-сервер для помощи агенту при рефакторинге C#: доступ к **Roslyn** (синтаксические деревья, семантика, символы, find usages, rename, code actions). Цель — стабильный тул без глюков (альтернатива Bifrost).

**Текущий статус:** работают `roslyn_ping`, `roslyn_get_document_symbols`, `roslyn_get_symbol_at_position`, `roslyn_find_usages`, `roslyn_go_to_definition`, `roslyn_rename`, `roslyn_get_code_actions`, `roslyn_apply_code_action`, `roslyn_get_diagnostics`.

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

В **csproj** заданы `RuntimeIdentifier=win-x64` и `SelfContained=true` — самодостаточная сборка (рантайм в папке, не зависит от установленного .NET в системе). Достаточно:

```bash
cd roslyn-mcp
dotnet publish -c Release -o publish
```

Exe появится в `roslyn-mcp/publish/RoslynMcp.exe`. В конфиге MCP в Cursor укажи этот exe (command — полный путь к нему, args — `[]`).

Обновить exe после правок: снова выполни ту же команду publish, затем перезапусти MCP или Cursor.

## VS Code + Claude Code

Сервер работает по **stdio**. Подключение через [Claude Code](https://code.claude.com/docs/en/mcp) (VS Code с расширением Claude).

**Требования:** .NET 10 (или собранный exe), установленный [Claude Code CLI](https://docs.anthropic.com/en/docs/build-with-claude/claude-code).

### Вариант 1: через exe (рекомендуется)

Собери exe (см. «Публикация exe» выше), затем добавь MCP:

```bash
claude mcp add --transport stdio roslyn -- C:\path\to\roslyn-mcp\publish\RoslynMcp.exe
```

На macOS/Linux подставь свой путь к exe, например `~/projects/roslyn-mcp/publish/RoslynMcp`.

### Вариант 2: через dotnet run (из папки проекта)

Если не собираешь exe, можно запускать из папки репо:

```bash
cd /path/to/roslyn-mcp
claude mcp add --transport stdio roslyn -- dotnet run --project .
```

(При первом запуске Claude Code выполнит `dotnet run --project .` в текущей папке — открой проект так, чтобы текущим каталогом была папка `roslyn-mcp`, или укажи полный путь: `dotnet run --project /path/to/roslyn-mcp/RoslynMcp.csproj`.)

### Общий конфиг в проекте (.mcp.json)

Чтобы все в команде использовали один и тот же MCP, положи в корень C#-проекта файл `.mcp.json`:

```json
{
  "mcpServers": {
    "roslyn": {
      "type": "stdio",
      "command": "C:\\path\\to\\roslyn-mcp\\publish\\RoslynMcp.exe",
      "args": []
    }
  }
}
```

Путь к `command` каждый подставляет свой (или используй переменные окружения, если Claude Code их поддерживает в `.mcp.json`). После сохранения Claude Code предложит доверять серверу; в чате можно проверить статус командой `/mcp`.

## Инструменты (tools)

| Инструмент | Описание |
|------------|----------|
| `roslyn_ping` | Проверка доступности сервера. |
| `roslyn_get_document_symbols` | Структура C# файла: namespace, class, method, property, field, enum, enum_member, delegate с номерами строк. Параметр: `file_path`. |
| `roslyn_get_symbol_at_position` | Символ в позиции (файл + строка + столбец, 1-based): kind и имя. Опционально `solution_or_project_path` — тогда в ответе Qualified (полное имя). Параметры: `file_path`, `line`, `column`. |
| `roslyn_find_usages` | Все ссылки на символ в solution/project. В выводе: квалифицированное имя (FullyQualifiedFormat), место определения (Definition:), затем список ссылок. Параметры: `solution_or_project_path`, `file_path`, `line`, `column` (на идентификаторе). |
| `roslyn_go_to_definition` | Переход к определению символа: по позиции возвращает file:line:column объявления (для partial — несколько строк). Параметры: `solution_or_project_path`, `file_path`, `line`, `column`. |
| `roslyn_rename` | Переименование символа по solution. Параметры: `solution_or_project_path`, `file_path`, `line`, `column`, `new_name`, опционально `apply` (превью/запись), `rename_in_comments`, `rename_in_strings`, `rename_overloads`, `rename_file` (аналог опций в VS). |
| `roslyn_get_code_actions` | Список Quick Actions / рефакторингов в позиции (как лампочка в VS). Параметры: `solution_or_project_path`, `file_path`, `line`, `column`. Опционально `end_line`, `end_column` — диапазон выделения для рефакторингов вроде Extract method / Extract local function. Возвращает нумерованный список. |
| `roslyn_apply_code_action` | Применить выбранное code action. Параметры: `solution_or_project_path`, `file_path`, `line`, `column`, `action_index` (0-based). Опционально: `end_line`, `end_column` (тот же диапазон, что при get); `fix_all_scope`: `"document"` \| `"project"` \| `"solution"`; `constant_name` — для Introduce constant; `action_options` — JSON-объект опций для действий с диалогом (Extract interface, Extract base class: имена членов и т.д.). Подробнее: [REFACTORINGS.md](REFACTORINGS.md). |
| `roslyn_get_diagnostics` | Диагностики компиляции и анализаторов по solution/project. Предпочтительно использовать вместо разбора логов сборки. Чтобы исправить: вызвать `roslyn_get_code_actions` по file:line:column из ответа, затем `roslyn_apply_code_action` (или fix_all_scope). Параметры: `solution_or_project_path`, опционально `file_path` — только по одному файлу. |
| `roslyn_get_solution_structure` | Список проектов в solution (имя, путь к .csproj). Параметр: `solution_or_project_path` (.sln или .csproj). Для реп с только .slnx: передай путь к главному .csproj — вернётся список подгруженных проектов. |
| `roslyn_generate_interface_from_class` | Сгенерировать C# интерфейс по классу **без диалогов** (обходной путь для Extract Interface). Позиция на класс: `solution_or_project_path`, `file_path`, `line`, `column`. Опционально: `interface_name`, `output_file_path`, `member_names` (массив). Дальше вручную или через code action добавь классу `: IName` и примени «Implement interface». |
| `roslyn_generate_base_class_from_class` | Сгенерировать абстрактный базовый класс по классу **без диалогов** (обходной путь для Extract Base Class). Позиция на класс: `solution_or_project_path`, `file_path`, `line`, `column`. Опционально: `base_class_name`, `output_file_path`, `member_names` (массив). Дальше: добавить классу `: BaseName` и проставить `override` у членов. |

## Лицензия

MIT License. См. [LICENSE](LICENSE).

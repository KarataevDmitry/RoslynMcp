using System.Text.Json;

namespace RoslynMcp.Mcp;

/// <summary>JSON-схемы параметров инструментов MCP.</summary>
public static class ToolSchemas
{
    private static JsonElement ToElement(object schema) =>
        JsonSerializer.SerializeToElement(schema);

    public static JsonElement Ping() =>
        ToElement(new { type = "object", properties = new { } });

    public static JsonElement GetDocumentSymbols() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "Путь к .cs файлу." }
            },
            required = new[] { "file_path" }
        });

    public static JsonElement GetSymbolAtPosition() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                file_path = new { type = "string", description = "Путь к .cs файлу." },
                line = new { type = "integer", description = "Номер строки (1-based)." },
                column = new { type = "integer", description = "Номер столбца (1-based)." },
                solution_or_project_path = new { type = "string", description = "Опционально: путь к .sln или .csproj — тогда в ответе будет Qualified (полное имя символа)." }
            },
            required = new[] { "file_path", "line", "column" }
        });

    public static JsonElement FindUsages() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj (.slnx не поддерживается)." },
                file_path = new { type = "string", description = "Путь к .cs файлу." },
                line = new { type = "integer", description = "Строка (1-based). Позиция на идентификаторе символа (имя метода/класса/свойства), не на типе — иначе найдёт ссылки на тип." },
                column = new { type = "integer", description = "Столбец (1-based)." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column" }
        });

    public static JsonElement GoToDefinition() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Путь к .cs файлу." },
                line = new { type = "integer", description = "Строка (1-based), позиция на идентификаторе символа." },
                column = new { type = "integer", description = "Столбец (1-based)." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column" }
        });

    public static JsonElement Rename() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Путь к .cs файлу." },
                line = new { type = "integer", description = "Строка (1-based), позиция на идентификаторе символа." },
                column = new { type = "integer", description = "Столбец (1-based)." },
                new_name = new { type = "string", description = "Новое имя символа." },
                apply = new { type = "boolean", description = "true — записать изменения в файлы; false (по умолчанию) — только превью." },
                rename_in_comments = new { type = "boolean", description = "Переименовывать вхождения в комментариях (как в VS «Include comments»)." },
                rename_in_strings = new { type = "boolean", description = "Переименовывать вхождения в строковых литералах." },
                rename_overloads = new { type = "boolean", description = "Для метода — переименовать и перегрузки." },
                rename_file = new { type = "boolean", description = "Для типа — переименовать файл с объявлением." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column", "new_name" }
        });

    public static JsonElement GetCodeActions() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Путь к .cs файлу." },
                line = new { type = "integer", description = "Строка начала (1-based)." },
                column = new { type = "integer", description = "Столбец начала (1-based)." },
                end_line = new { type = "integer", description = "Опционально. Строка конца выделения (1-based). Если заданы end_line и end_column — refactoring получает диапазон (например Extract method / Extract local function)." },
                end_column = new { type = "integer", description = "Опционально. Столбец конца выделения (1-based). Задавать вместе с end_line." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column" }
        });

    public static JsonElement ApplyCodeAction() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Путь к .cs файлу." },
                line = new { type = "integer", description = "Строка (1-based), та же позиция, что при get_code_actions." },
                column = new { type = "integer", description = "Столбец (1-based)." },
                end_line = new { type = "integer", description = "Опционально. Та же end_line, что при get_code_actions, если использовался диапазон." },
                end_column = new { type = "integer", description = "Опционально. Та же end_column, что при get_code_actions." },
                action_index = new { type = "integer", description = "Индекс действия (0-based) из списка roslyn_get_code_actions." },
                fix_all_scope = new { type = "string", description = "Опционально. Scope применения: \"document\" | \"project\" | \"solution\" — Fix all в файле/проекте/решении. Только для code fixes с поддержкой Fix All." },
                constant_name = new { type = "string", description = "Опционально. Имя константы для действий с опциями (например Introduce constant). Если не задано, провайдер использует значение по умолчанию (например V)." },
                action_options = new { type = "object", description = "Опционально. JSON-объект опций для действий с диалогом (Extract interface, Extract base class и т.д.): ключи — имена свойств типа опций, значения — строки, числа, bool или массив строк (например member_names). См. REFACTORINGS.md." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column", "action_index" }
        });

    public static JsonElement GetDiagnostics() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj. Предпочтительный способ проверки C# кода (неиспользуемые переменные, предупреждения) — вызывать этот тул вместо опоры только на ReadLints." },
                file_path = new { type = "string", description = "Опционально. Путь к .cs файлу — тогда только диагностики этого файла. Иначе — по всему решению/проекту." }
            },
            required = new[] { "solution_or_project_path" }
        });

    public static JsonElement GetSolutionStructure() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj. Если в репо только .slnx (формат не поддерживается): передай путь к главному .csproj (например UI или стартовый проект) — вернётся список всех подгруженных проектов (включая ссылки). Иначе: список проектов (имя и путь к .csproj) для передачи в остальные тулы." }
            },
            required = new[] { "solution_or_project_path" }
        });

    public static JsonElement SyncNamespaces() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj (.slnx не поддерживается). После переименования/перемещения папок вызвать сначала с dry_run для превью." },
                project_path = new { type = "string", description = "Опционально. Для solution с несколькими проектами — путь к .csproj, чтобы синхронизировать только этот проект." },
                dry_run = new { type = "boolean", description = "Опционально. true — только отчёт о планируемых изменениях, без записи. Рекомендуется сначала dry_run, затем без dry_run для применения." }
            },
            required = new[] { "solution_or_project_path" }
        });

    public static JsonElement ResolveBreakpoint() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Путь к .cs файлу, в котором искать символ." },
                symbol_name = new { type = "string", description = "Имя символа: метод, конструктор, свойство. Для индексатора передать \"this\". Возвращается file:line первой исполняемой строки (место для брейкпоинта)." }
            },
            required = new[] { "solution_or_project_path", "file_path", "symbol_name" }
        });

    public static JsonElement GenerateInterfaceFromClass() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Путь к .cs файлу с классом." },
                line = new { type = "integer", description = "Строка (1-based): курсор на имени класса или внутри тела." },
                column = new { type = "integer", description = "Столбец (1-based)." },
                interface_name = new { type = "string", description = "Опционально. Имя интерфейса (по умолчанию I + имя класса)." },
                output_file_path = new { type = "string", description = "Опционально. Путь к .cs файлу для записи интерфейса. Иначе возвращается только текст." },
                member_names = new { type = "array", items = new { type = "string" }, description = "Опционально. Массив имён методов/свойств для включения в интерфейс. Если не задан — все public instance методы и свойства." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column" }
        });
}

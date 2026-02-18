using System.Text.Json;

namespace RoslynMcp;

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
                line = new { type = "integer", description = "Строка (1-based)." },
                column = new { type = "integer", description = "Столбец (1-based)." }
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
                action_index = new { type = "integer", description = "Индекс действия (0-based) из списка roslyn_get_code_actions." },
                fix_all_scope = new { type = "string", description = "Опционально. Scope применения: \"document\" | \"project\" | \"solution\" — Fix all в файле/проекте/решении. Только для code fixes с поддержкой Fix All." },
                constant_name = new { type = "string", description = "Опционально. Имя константы для действий с опциями (например Introduce constant). Если не задано, провайдер использует значение по умолчанию (например V)." }
            },
            required = new[] { "solution_or_project_path", "file_path", "line", "column", "action_index" }
        });

    public static JsonElement GetDiagnostics() =>
        ToElement(new
        {
            type = "object",
            properties = new
            {
                solution_or_project_path = new { type = "string", description = "Путь к .sln или .csproj." },
                file_path = new { type = "string", description = "Опционально. Путь к .cs файлу — тогда только диагностики этого файла. Иначе — по всему решению/проекту." }
            },
            required = new[] { "solution_or_project_path" }
        });
}

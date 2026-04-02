using ModelContextProtocol.Protocol;
using RoslynMcp.Mcp;
using Tool = ModelContextProtocol.Protocol.Tool;

/// <summary>Каталог MCP-тулов. Согласован с <c>mcp-tools.manifest.json</c> и <c>docs/MCP-TOOLS.md</c> (генерация: <c>tools/ExportMcpManifest</c>, тесты <c>RoslynMcp.Tests</c>).</summary>
internal static class ToolCatalog
{
    internal static List<Tool> Build() =>
    [
        new() { Name = "roslyn_ping", Description = "Проверка доступности сервера.", InputSchema = ToolSchemas.Ping() },
        new()
        {
            Name = "roslyn_get_document_symbols",
            Description = "Структура C# файла: namespace, class, method, property, field с номерами строк.",
            InputSchema = ToolSchemas.GetDocumentSymbols()
        },
        new()
        {
            Name = "roslyn_get_symbol_at_position",
            Description = "Символ в позиции (файл, строка, столбец — 1-based): kind и имя.",
            InputSchema = ToolSchemas.GetSymbolAtPosition()
        },
        new()
        {
            Name = "roslyn_find_usages",
            Description =
                "Все ссылки на символ в solution/project. line/column — на идентификаторе (имя метода, класса и т.д.), не на типе в сигнатуре.",
            InputSchema = ToolSchemas.FindUsages()
        },
        new()
        {
            Name = "roslyn_go_to_definition",
            Description =
                "Переход к определению символа: по позиции возвращает file:line:column объявления (1-based). Параметры: solution_or_project_path, file_path, line, column.",
            InputSchema = ToolSchemas.GoToDefinition()
        },
        new()
        {
            Name = "roslyn_rename",
            Description =
                "Переименовать символ по solution. apply: false — превью; true — записать. Чтобы затронуть комментарии/строки/перегрузки/файл — передай rename_in_comments, rename_in_strings, rename_overloads, rename_file (bool).",
            InputSchema = ToolSchemas.Rename()
        },
        new()
        {
            Name = "roslyn_get_code_actions",
            Description =
                "Список Quick Actions / рефакторингов в позиции (как лампочка в VS). Параметры: solution_or_project_path, file_path, line, column. Возвращает нумерованный список; применить — roslyn_apply_code_action с action_index.",
            InputSchema = ToolSchemas.GetCodeActions()
        },
        new()
        {
            Name = "roslyn_apply_code_action",
            Description =
                "Применить выбранное code action по индексу из roslyn_get_code_actions. Параметры: solution_or_project_path, file_path, line, column, action_index (0-based).",
            InputSchema = ToolSchemas.ApplyCodeAction()
        },
        new()
        {
            Name = "roslyn_get_diagnostics",
            Description =
                "Диагностики компиляции и анализаторов по solution/project (file:line:column, severity, id, message). Предпочтительно использовать вместо ReadLints/ручного просмотра для поиска неиспользуемых переменных (CS0219), предупреждений компилятора и анализаторов. Чтобы исправить: roslyn_get_code_actions по file:line:column из ответа, затем roslyn_apply_code_action (или fix_all_scope). Параметры: solution_or_project_path, опционально file_path.",
            InputSchema = ToolSchemas.GetDiagnostics()
        },
        new()
        {
            Name = "roslyn_get_solution_structure",
            Description =
                "Структура solution: список проектов (имя, путь к .csproj). Параметр: solution_or_project_path (.sln или .csproj). Обходной путь для реп с .slnx: передай путь к главному .csproj — вернётся полный список подгруженных проектов. Использовать, чтобы узнать состав решения и пути для остальных тулов.",
            InputSchema = ToolSchemas.GetSolutionStructure()
        },
        new()
        {
            Name = "roslyn_sync_namespaces",
            Description =
                "Привести объявления namespace к структуре папок (RootNamespace + путь). Вызывать после переименования или перемещения папок в C# проекте: сначала dry_run для превью, затем без dry_run для применения. Обновляет также using в остальных файлах. Параметры: solution_or_project_path; опционально project_path, dry_run.",
            InputSchema = ToolSchemas.SyncNamespaces()
        },
        new()
        {
            Name = "roslyn_resolve_breakpoint",
            Description =
                "По имени символа (метод, свойство, конструктор) в файле вернуть file:line первой исполняемой строки — место для брейкпоинта. Параметры: solution_or_project_path, file_path, symbol_name.",
            InputSchema = ToolSchemas.ResolveBreakpoint()
        },
        new()
        {
            Name = "roslyn_generate_interface_from_class",
            Description =
                "Сгенерировать C# интерфейс по классу (без диалогов Roslyn). Позиция на класс в file_path:line:column. Опционально: interface_name, output_file_path, member_names (массив).",
            InputSchema = ToolSchemas.GenerateInterfaceFromClass()
        },
        new()
        {
            Name = "roslyn_generate_base_class_from_class",
            Description =
                "Сгенерировать абстрактный базовый класс по классу (без диалогов). Выбранные public-члены — abstract в новом классе. Опционально: base_class_name, output_file_path, member_names.",
            InputSchema = ToolSchemas.GenerateBaseClassFromClass()
        },
        new()
        {
            Name = "roslyn_generate_overrides",
            Description =
                "Generate Overrides без диалога: по позиции на класс генерирует override виртуальных/абстрактных членов базового типа. Опционально: member_names, insert_into_file.",
            InputSchema = ToolSchemas.GenerateOverrides()
        },
        new()
        {
            Name = "roslyn_generate_constructor_from_members",
            Description =
                "Generate constructor from members без диалога: по позиции на класс — конструктор по полям/свойствам. Опционально: member_names, insert_into_file.",
            InputSchema = ToolSchemas.GenerateConstructorFromMembers()
        },
        new()
        {
            Name = "roslyn_generate_equals_gethashcode",
            Description =
                "Generate Equals and GetHashCode без диалога: по позиции на класс — override по выбранным полям/свойствам. Опционально: member_names, insert_into_file.",
            InputSchema = ToolSchemas.GenerateEqualsGetHashCode()
        }
    ];
}

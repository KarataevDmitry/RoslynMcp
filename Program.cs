using System.Collections.Frozen;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynMcp.Mcp;
using Tool = ModelContextProtocol.Protocol.Tool;

var toolsList = new List<Tool> { new() { Name = "roslyn_ping", Description = "Проверка доступности сервера.", InputSchema = ToolSchemas.Ping() }, new() { Name = "roslyn_get_document_symbols", Description = "Структура C# файла: namespace, class, method, property, field с номерами строк.", InputSchema = ToolSchemas.GetDocumentSymbols() }, new() { Name = "roslyn_get_symbol_at_position", Description = "Символ в позиции (файл, строка, столбец — 1-based): kind и имя.", InputSchema = ToolSchemas.GetSymbolAtPosition() }, new() { Name = "roslyn_find_usages", Description = "Все ссылки на символ в solution/project. line/column — на идентификаторе (имя метода, класса и т.д.), не на типе в сигнатуре.", InputSchema = ToolSchemas.FindUsages() }, new() { Name = "roslyn_go_to_definition", Description = "Переход к определению символа: по позиции возвращает file:line:column объявления (1-based). Параметры: solution_or_project_path, file_path, line, column.", InputSchema = ToolSchemas.GoToDefinition() }, new() { Name = "roslyn_rename", Description = "Переименовать символ по solution. apply: false — превью; true — записать. Чтобы затронуть комментарии/строки/перегрузки/файл — передай rename_in_comments, rename_in_strings, rename_overloads, rename_file (bool).", InputSchema = ToolSchemas.Rename() }, new() { Name = "roslyn_get_code_actions", Description = "Список Quick Actions / рефакторингов в позиции (как лампочка в VS). Параметры: solution_or_project_path, file_path, line, column. Возвращает нумерованный список; применить — roslyn_apply_code_action с action_index.", InputSchema = ToolSchemas.GetCodeActions() }, new() { Name = "roslyn_apply_code_action", Description = "Применить выбранное code action по индексу из roslyn_get_code_actions. Параметры: solution_or_project_path, file_path, line, column, action_index (0-based).", InputSchema = ToolSchemas.ApplyCodeAction() }, new() { Name = "roslyn_get_diagnostics", Description = "Диагностики компиляции и анализаторов по solution/project (file:line:column, severity, id, message). Предпочтительно использовать вместо ReadLints/ручного просмотра для поиска неиспользуемых переменных (CS0219), предупреждений компилятора и анализаторов. Чтобы исправить: roslyn_get_code_actions по file:line:column из ответа, затем roslyn_apply_code_action (или fix_all_scope). Параметры: solution_or_project_path, опционально file_path.", InputSchema = ToolSchemas.GetDiagnostics() }, new() { Name = "roslyn_get_solution_structure", Description = "Структура solution: список проектов (имя, путь к .csproj). Параметр: solution_or_project_path (.sln или .csproj). Обходной путь для реп с .slnx: передай путь к главному .csproj — вернётся полный список подгруженных проектов. Использовать, чтобы узнать состав решения и пути для остальных тулов.", InputSchema = ToolSchemas.GetSolutionStructure() }, new() { Name = "roslyn_sync_namespaces", Description = "Привести объявления namespace к структуре папок (RootNamespace + путь). Вызывать после переименования или перемещения папок в C# проекте: сначала dry_run для превью, затем без dry_run для применения. Обновляет также using в остальных файлах. Параметры: solution_or_project_path; опционально project_path, dry_run.", InputSchema = ToolSchemas.SyncNamespaces() }, new() { Name = "roslyn_resolve_breakpoint", Description = "По имени символа (метод, свойство, конструктор) в файле вернуть file:line первой исполняемой строки — место для брейкпоинта. Параметры: solution_or_project_path, file_path, symbol_name.", InputSchema = ToolSchemas.ResolveBreakpoint() }, new() { Name = "roslyn_generate_interface_from_class", Description = "Сгенерировать C# интерфейс по классу (без диалогов Roslyn). Позиция на класс в file_path:line:column. Опционально: interface_name, output_file_path, member_names (массив).", InputSchema = ToolSchemas.GenerateInterfaceFromClass() }, new() { Name = "roslyn_generate_base_class_from_class", Description = "Сгенерировать абстрактный базовый класс по классу (без диалогов). Выбранные public-члены — abstract в новом классе. Опционально: base_class_name, output_file_path, member_names.", InputSchema = ToolSchemas.GenerateBaseClassFromClass() }, };

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "RoslynMcp", Version = "0.5.0" },
    ProtocolVersion = "2024-11-05",
    Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = toolsList }),

        CallToolHandler = async (request, cancellationToken) =>
        {
            var name = request.Params?.Name ?? "";
            var args = request.Params?.Arguments is IReadOnlyDictionary<string, JsonElement> a
                ? a
                : FrozenDictionary<string, JsonElement>.Empty;
            try
            {
                var text = await ToolHandlers.HandleAsync(name, args, cancellationToken).ConfigureAwait(false);
                return new CallToolResult { Content = [new TextContentBlock { Text = text }] };
            }
            catch (ArgumentException ex)
            {
                return new CallToolResult { Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }], IsError = true };
            }
            catch (Exception ex)
            {
                return new CallToolResult { Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }], IsError = true };
            }
        }
    }
};

var transport = new StdioServerTransport("RoslynMcp");
await using var server = McpServer.Create(transport, options);
await server.RunAsync();
return 0;

using System.Text.Json;
using RoslynMcp.ServiceLayer;

namespace RoslynMcp.Mcp;

/// <summary>Обработчики вызовов инструментов MCP. Один вход — имя + аргументы, выход — текст результата или исключение.</summary>
public static class ToolHandlers
{
    public static Task<string> HandleAsync(
        string name,
        IReadOnlyDictionary<string, JsonElement> args,
        CancellationToken cancellationToken)
    {
        return name switch
        {
            "roslyn_ping" => Task.FromResult(Ping()),
            "roslyn_get_document_symbols" => Task.FromResult(GetDocumentSymbols(args, cancellationToken)),
            "roslyn_get_symbol_at_position" => GetSymbolAtPositionAsync(args, cancellationToken),
            "roslyn_find_usages" => FindUsagesAsync(args, cancellationToken),
            "roslyn_go_to_definition" => GoToDefinitionAsync(args, cancellationToken),
            "roslyn_rename" => RenameAsync(args, cancellationToken),
            "roslyn_get_code_actions" => GetCodeActionsAsync(args, cancellationToken),
            "roslyn_apply_code_action" => ApplyCodeActionAsync(args, cancellationToken),
            "roslyn_get_diagnostics" => GetDiagnosticsAsync(args, cancellationToken),
            "roslyn_get_solution_structure" => GetSolutionStructureAsync(args, cancellationToken),
            "roslyn_sync_namespaces" => SyncNamespacesAsync(args, cancellationToken),
            "roslyn_resolve_breakpoint" => ResolveBreakpointAsync(args, cancellationToken),
            "roslyn_generate_interface_from_class" => GenerateInterfaceFromClassAsync(args, cancellationToken),
            "roslyn_generate_base_class_from_class" => GenerateBaseClassFromClassAsync(args, cancellationToken),
            "roslyn_generate_overrides" => GenerateOverridesAsync(args, cancellationToken),
            "roslyn_generate_constructor_from_members" => GenerateConstructorFromMembersAsync(args, cancellationToken),
            "roslyn_generate_equals_gethashcode" => GenerateEqualsGetHashCodeAsync(args, cancellationToken),
            _ => throw new ArgumentException($"Unknown tool: {name}.", nameof(name))
        };
    }

    private static string Ping() =>
        $"OK {DateTime.UtcNow:O} — RoslynMcp. Tools: roslyn_get_document_symbols, roslyn_get_symbol_at_position, …";

    private static string GetDocumentSymbols(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        return DocumentSymbols.GetDocumentSymbols(filePath!, ct);
    }

    private static Task<string> GetSymbolAtPositionAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        var solutionPath = args.TryGetValue("solution_or_project_path", out var solEl) && solEl.ValueKind == JsonValueKind.String
            ? solEl.GetString()
            : null;
        return SymbolAtPosition.GetSymbolAtPositionAsync(filePath!, line, column, solutionPath, ct);
    }

    private static async Task<string> FindUsagesAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        return await FindUsages.FindUsagesAsync(solutionPath!, filePath!, line, column, ct).ConfigureAwait(false);
    }

    private static Task<string> GoToDefinitionAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        return GoToDefinition.GoToDefinitionAsync(solutionPath!, filePath!, line, column, ct);
    }

    private static async Task<string> RenameAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        if (!TryGetString(args, "new_name", out var newName))
            throw new ArgumentException("new_name (string) is required.");
        var apply = args.TryGetValue("apply", out var applyEl) && applyEl.ValueKind == JsonValueKind.True;
        var renameInComments = args.TryGetValue("rename_in_comments", out var ricEl) && ricEl.ValueKind == JsonValueKind.True;
        var renameInStrings = args.TryGetValue("rename_in_strings", out var risEl) && risEl.ValueKind == JsonValueKind.True;
        var renameOverloads = args.TryGetValue("rename_overloads", out var roEl) && roEl.ValueKind == JsonValueKind.True;
        var renameFile = args.TryGetValue("rename_file", out var rfEl) && rfEl.ValueKind == JsonValueKind.True;
        return await RenameSymbol.RenameAsync(solutionPath!, filePath!, line, column, newName!, apply, renameInComments, renameInStrings, renameOverloads, renameFile, ct).ConfigureAwait(false);
    }

    private static Task<string> GetCodeActionsAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        int? endLine = TryGetInt(args, "end_line", out var el) && el >= 1 ? el : null;
        int? endColumn = TryGetInt(args, "end_column", out var ec) && ec >= 1 ? ec : null;
        return CodeActions.GetCodeActionsAsync(solutionPath!, filePath!, line, column, endLine, endColumn, ct);
    }

    private static Task<string> ApplyCodeActionAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        if (!TryGetInt(args, "action_index", out var actionIndex) || actionIndex < 0)
            throw new ArgumentException("action_index (integer >= 0) is required.");
        int? endLineApply = TryGetInt(args, "end_line", out var ela) && ela >= 1 ? ela : null;
        int? endColumnApply = TryGetInt(args, "end_column", out var eca) && eca >= 1 ? eca : null;
        TryGetString(args, "fix_all_scope", out var fixAllScope);
        TryGetString(args, "constant_name", out var constantName);
        var actionOptions = TryGetActionOptions(args);
        return CodeActions.ApplyCodeActionAsync(solutionPath!, filePath!, line, column, actionIndex, endLineApply, endColumnApply, fixAllScope, constantName, actionOptions, ct);
    }

    /// <summary>Парсит action_options из args: JSON object → Dictionary (string, int, bool, string[]).</summary>
    private static IReadOnlyDictionary<string, object?>? TryGetActionOptions(IReadOnlyDictionary<string, JsonElement> args)
    {
        if (!args.TryGetValue("action_options", out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            object? val = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetInt32(out var i32) ? i32 : prop.Value.TryGetInt64(out var i64) ? i64 : (object?)prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => GetStringArray(prop.Value),
                _ => null
            };
            if (val != null || prop.Value.ValueKind == JsonValueKind.Null || prop.Value.ValueKind == JsonValueKind.String)
                dict[prop.Name] = val;
        }
        return dict.Count == 0 ? null : dict;
    }

    private static object? GetStringArray(JsonElement arr)
    {
        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                list.Add(s);
        }
        return list.Count == 0 ? null : list.ToArray();
    }

    private static Task<string> GetDiagnosticsAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        TryGetString(args, "file_path", out var filePath);
        return GetDiagnostics.GetDiagnosticsAsync(solutionPath!, filePath, ct);
    }

    private static Task<string> GetSolutionStructureAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        return GetSolutionStructure.GetStructureAsync(solutionPath!, ct);
    }

    private static Task<string> SyncNamespacesAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        TryGetString(args, "project_path", out var projectPath);
        var dryRun = args.TryGetValue("dry_run", out var dryRunEl) && dryRunEl.ValueKind == JsonValueKind.True;
        return SyncNamespaces.SyncAsync(solutionPath!, dryRun, projectPath, cancellationToken: ct);
    }

    private static Task<string> ResolveBreakpointAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetString(args, "symbol_name", out var symbolName))
            throw new ArgumentException("symbol_name (string) is required.");
        return ResolveBreakpoint.ResolveAsync(solutionPath!, filePath!, symbolName!, ct);
    }

    private static Task<string> GenerateInterfaceFromClassAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        TryGetString(args, "interface_name", out var interfaceName);
        TryGetString(args, "output_file_path", out var outputFilePath);
        IReadOnlyList<string>? memberNames = null;
        if (args.TryGetValue("member_names", out var mnEl) && mnEl.ValueKind == JsonValueKind.Array && GetStringArray(mnEl) is string[] arr && arr.Length > 0)
            memberNames = arr;
        return GenerateInterface.GenerateInterfaceFromClassAsync(solutionPath!, filePath!, line, column, interfaceName, outputFilePath, memberNames, ct);
    }

    private static Task<string> GenerateBaseClassFromClassAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        TryGetString(args, "base_class_name", out var baseClassName);
        TryGetString(args, "output_file_path", out var outputFilePath);
        IReadOnlyList<string>? memberNames = null;
        if (args.TryGetValue("member_names", out var mnEl) && mnEl.ValueKind == JsonValueKind.Array && GetStringArray(mnEl) is string[] arr && arr.Length > 0)
            memberNames = arr;
        return GenerateBaseClass.GenerateBaseClassFromClassAsync(solutionPath!, filePath!, line, column, baseClassName, outputFilePath, memberNames, ct);
    }

    private static Task<string> GenerateOverridesAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        IReadOnlyList<string>? memberNames = null;
        if (args.TryGetValue("member_names", out var mnEl) && mnEl.ValueKind == JsonValueKind.Array && GetStringArray(mnEl) is string[] arr && arr.Length > 0)
            memberNames = arr;
        var insertIntoFile = args.TryGetValue("insert_into_file", out var insEl) && insEl.ValueKind == JsonValueKind.True;
        return GenerateOverrides.GenerateOverridesAsync(solutionPath!, filePath!, line, column, memberNames, insertIntoFile, ct);
    }

    private static Task<string> GenerateConstructorFromMembersAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        IReadOnlyList<string>? memberNames = null;
        if (args.TryGetValue("member_names", out var mnEl) && mnEl.ValueKind == JsonValueKind.Array && GetStringArray(mnEl) is string[] arr && arr.Length > 0)
            memberNames = arr;
        var insertIntoFile = args.TryGetValue("insert_into_file", out var insEl) && insEl.ValueKind == JsonValueKind.True;
        return GenerateConstructor.GenerateConstructorFromMembersAsync(solutionPath!, filePath!, line, column, memberNames, insertIntoFile, ct);
    }

    private static Task<string> GenerateEqualsGetHashCodeAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        if (!TryGetString(args, "file_path", out var filePath))
            throw new ArgumentException("file_path (string) is required.");
        if (!TryGetInt(args, "line", out var line) || line < 1)
            throw new ArgumentException("line (integer >= 1) is required.");
        if (!TryGetInt(args, "column", out var column) || column < 1)
            throw new ArgumentException("column (integer >= 1) is required.");
        IReadOnlyList<string>? memberNames = null;
        if (args.TryGetValue("member_names", out var mnEl) && mnEl.ValueKind == JsonValueKind.Array && GetStringArray(mnEl) is string[] arr && arr.Length > 0)
            memberNames = arr;
        var insertIntoFile = args.TryGetValue("insert_into_file", out var insEl) && insEl.ValueKind == JsonValueKind.True;
        return GenerateEqualsGetHashCode.GenerateEqualsGetHashCodeAsync(solutionPath!, filePath!, line, column, memberNames, insertIntoFile, ct);
    }

    private static bool TryGetString(IReadOnlyDictionary<string, JsonElement> args, string key, out string? value)
    {
        value = null;
        if (!args.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return true;
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, JsonElement> args, string key, out int value)
    {
        value = 0;
        if (!args.TryGetValue(key, out var el))
            return false;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            value = n;
            return true;
        }
        return false;
    }
}

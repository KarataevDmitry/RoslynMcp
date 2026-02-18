using System.Text.Json;

namespace RoslynMcp;

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
            "roslyn_rename" => RenameAsync(args, cancellationToken),
            "roslyn_get_code_actions" => GetCodeActionsAsync(args, cancellationToken),
            "roslyn_apply_code_action" => ApplyCodeActionAsync(args, cancellationToken),
            "roslyn_get_diagnostics" => GetDiagnosticsAsync(args, cancellationToken),
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
        return CodeActions.GetCodeActionsAsync(solutionPath!, filePath!, line, column, ct);
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
        TryGetString(args, "fix_all_scope", out var fixAllScope);
        TryGetString(args, "constant_name", out var constantName);
        return CodeActions.ApplyCodeActionAsync(solutionPath!, filePath!, line, column, actionIndex, fixAllScope, constantName, ct);
    }

    private static Task<string> GetDiagnosticsAsync(IReadOnlyDictionary<string, JsonElement> args, CancellationToken ct)
    {
        if (!TryGetString(args, "solution_or_project_path", out var solutionPath))
            throw new ArgumentException("solution_or_project_path (string) is required.");
        TryGetString(args, "file_path", out var filePath);
        return GetDiagnostics.GetDiagnosticsAsync(solutionPath!, filePath, ct);
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

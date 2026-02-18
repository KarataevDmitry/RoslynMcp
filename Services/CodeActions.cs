using System.Collections.Immutable;
using System.Reflection;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace RoslynMcp.Services;

/// <summary>Поставляет диагностики из компиляции для FixAllContext (по документу/проекту).</summary>
internal sealed class CompilationDiagnosticProvider : FixAllContext.DiagnosticProvider
{
    public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
    {
        return GetDocumentDiagnosticsCoreAsync(document, cancellationToken);
    }

    private static async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsCoreAsync(Document document, CancellationToken ct)
    {
        var comp = await document.Project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (comp is null) { return []; }
        var docPath = document.FilePath is null ? null : Path.GetFullPath(document.FilePath.Trim());
        if (docPath is null) { return []; }
        var diag = comp.GetDiagnostics(ct);
        return diag.Where(d => d.Location.SourceTree?.FilePath != null
            && string.Equals(Path.GetFullPath(d.Location.SourceTree.FilePath.Trim()), docPath, StringComparison.OrdinalIgnoreCase));
    }

    public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        return GetProjectDiagnosticsCoreAsync(project, projectLevelOnly: true, cancellationToken);
    }

    public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        return GetProjectDiagnosticsCoreAsync(project, projectLevelOnly: false, cancellationToken);
    }

    private static async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsCoreAsync(Project project, bool projectLevelOnly, CancellationToken ct)
    {
        var comp = await project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (comp is null) { return []; }
        var diag = comp.GetDiagnostics(ct);
        if (projectLevelOnly)
        {
            return diag.Where(d => d.Location.Kind == LocationKind.None || d.Location.SourceTree is null);
        }
        return diag;
    }
}

/// <summary>Список code actions (рефакторинги/фиксы) в позиции и применение выбранного. Провайдеры загружаются через рефлексию из Features-сборок.</summary>
public static class CodeActions
{
    private static string NormalizePath(string path)
    {
        var p = Path.GetFullPath(path.Trim());
        if (p.EndsWith(Path.DirectorySeparatorChar)) p = p.TrimEnd(Path.DirectorySeparatorChar);
        return p;
    }

    private static IEnumerable<CodeRefactoringProvider> GetRefactoringProviders()
    {
        var list = new List<CodeRefactoringProvider>();
        var assemblies = new[] { typeof(CodeRefactoringProvider).Assembly, Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"), Assembly.Load("Microsoft.CodeAnalysis.Features") };
        foreach (var asm in assemblies.Distinct())
        {
            foreach (var type in asm.GetTypes())
            {
                if (type.IsAbstract || !typeof(CodeRefactoringProvider).IsAssignableFrom(type)) continue;
                try
                {
                    if (Activator.CreateInstance(type) is CodeRefactoringProvider provider)
                        list.Add(provider);
                }
                catch { /* skip */ }
            }
        }
        return list;
    }

    private static IEnumerable<CodeFixProvider> GetCodeFixProviders()
    {
        var list = new List<CodeFixProvider>();
        var assemblies = new[] { typeof(CodeFixProvider).Assembly, Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"), Assembly.Load("Microsoft.CodeAnalysis.Features") };
        foreach (var asm in assemblies.Distinct())
        {
            foreach (var type in asm.GetTypes())
            {
                if (type.IsAbstract || !typeof(CodeFixProvider).IsAssignableFrom(type)) continue;
                try
                {
                    if (Activator.CreateInstance(type) is CodeFixProvider provider)
                        list.Add(provider);
                }
                catch { /* skip */ }
            }
        }
        return list;
    }

    /// <summary>Возвращает вложенные действия (подменю), если есть; иначе пустой массив. Использует свойство NestedActions или рефлексию.</summary>
    private static ImmutableArray<CodeAction> GetNestedActions(CodeAction action)
    {
        if (action is null) return default;
        try
        {
            var prop = action.GetType().GetProperty("NestedActions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop is null) return default;
            var val = prop.GetValue(action);
            if (val is ImmutableArray<CodeAction> nested && !nested.IsDefault && nested.Length > 0)
                return nested;
        }
        catch { /* ignore */ }
        return default;
    }

    /// <summary>Для действий с опциями (например Introduce constant): вызвать GetOperationsAsync(options, ct), подставив имя константы. Возвращает true, если вызов удался.</summary>
    private static bool TryGetOperationsWithOptions(CodeAction action, string constantName, CancellationToken ct, out IEnumerable<CodeActionOperation>? operations)
    {
        operations = null;
        try
        {
            var type = action.GetType();
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == nameof(CodeAction.GetOperationsAsync) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(CancellationToken));
            if (method is null) return false;
            var optionsParam = method.GetParameters()[0];
            var optionsType = optionsParam.ParameterType;
            if (optionsType == typeof(object))
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Name"] = constantName, ["ConstantName"] = constantName };
                var boxed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Name"] = constantName, ["ConstantName"] = constantName };
                foreach (var tryOptions in new object[] { boxed, dict })
                {
                    try
                    {
                        var task = method.Invoke(action, [tryOptions, ct]);
                        if (TryUnwrapOperationsTask(task, out operations)) return operations != null;
                    }
                    catch { /* skip */ }
                }
                return false;
            }
            var optionsInstance = Activator.CreateInstance(optionsType);
            if (optionsInstance is null) return false;
            foreach (var propName in new[] { "Name", "ConstantName" })
            {
                var prop = optionsType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop?.CanWrite == true) { prop.SetValue(optionsInstance, constantName); break; }
            }
            var task2 = method.Invoke(action, [optionsInstance, ct]);
            return TryUnwrapOperationsTask(task2, out operations);
        }
        catch { /* ignore */ }
        return false;
    }

    private static bool TryUnwrapOperationsTask(object? taskObj, out IEnumerable<CodeActionOperation>? operations)
    {
        operations = null;
        if (taskObj is not Task task || !task.GetType().IsGenericType) return false;
        try
        {
            var awaiter = task.GetType().GetMethod("GetAwaiter")!.Invoke(task, null)!;
            var result = awaiter.GetType().GetMethod("GetResult")!.Invoke(awaiter, null);
            if (result is IEnumerable<CodeActionOperation> ops) { operations = ops; return true; }
        }
        catch { /* ignore */ }
        return false;
    }

    private static bool TryParseFixAllScope(string value, out FixAllScope scope)
    {
        scope = FixAllScope.Document;
        if (string.IsNullOrEmpty(value)) return false;
        if (string.Equals(value, "document", StringComparison.OrdinalIgnoreCase)) { scope = FixAllScope.Document; return true; }
        if (string.Equals(value, "project", StringComparison.OrdinalIgnoreCase)) { scope = FixAllScope.Project; return true; }
        if (string.Equals(value, "solution", StringComparison.OrdinalIgnoreCase)) { scope = FixAllScope.Solution; return true; }
        return false;
    }

    /// <summary>Разворачивает действие: если есть вложенные — возвращает пары (родитель > дочерний, дочернее действие), иначе одну пару (заголовок, само действие).</summary>
    private static IEnumerable<(string title, CodeAction action)> FlattenAction(CodeAction a)
    {
        var nested = GetNestedActions(a);
        if (!nested.IsDefault && nested.Length > 0)
        {
            var parentTitle = a.Title ?? "(no title)";
            foreach (var child in nested)
            {
                var childTitle = child.Title;
                if (string.IsNullOrEmpty(childTitle)) childTitle = "(no title)";
                yield return ($"{parentTitle} > {childTitle}", child);
            }
        }
        else
        {
            var title = a.Title;
            if (string.IsNullOrEmpty(title)) title = "(no title)";
            yield return (title, a);
        }
    }

    public static async Task<string> GetCodeActionsAsync(
        string solutionOrProjectPath,
        string filePath,
        int line,
        int column,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionOrProjectPath))
            return $"Error: solution/project not found: {solutionOrProjectPath}";
        if (!File.Exists(filePath))
            return $"Error: file not found: {filePath}";

        var targetPath = NormalizePath(filePath);
        Solution? solution = null;
        try
        {
            var workspace = MSBuildWorkspace.Create();
            if (string.Equals(Path.GetExtension(solutionOrProjectPath), ".sln", StringComparison.OrdinalIgnoreCase))
                solution = await workspace.OpenSolutionAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            else
                solution = (await workspace.OpenProjectAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false)).Solution;

            if (solution is null)
                return "Error: failed to open solution.";

            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => string.Equals(NormalizePath(d.FilePath ?? ""), targetPath, StringComparison.OrdinalIgnoreCase));
            if (document is null)
                return $"Error: file not found in solution: {filePath}";

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var lines = sourceText.Lines;
            if (line < 1 || line > lines.Count)
                return $"Error: line {line} out of range (1..{lines.Count}).";
            var lineInfo = lines[line - 1];
            var columnIndex = column - 1;
            var lineLen = lineInfo.Span.Length;
            if (columnIndex < 0)
                return $"Error: column {column} must be >= 1.";
            var position = lineLen == 0
                ? lineInfo.Start
                : lineInfo.Start + Math.Min(columnIndex, lineLen);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var span = root is null
                ? new TextSpan(position, 0)
                : root.FindToken(position, findInsideTrivia: true).Span;

            var docPath = document.FilePath ?? filePath;
            var docInfo = $"# Document: {docPath} (total_lines={lines.Count}, line_{line}_length={lineLen})";

            var actions = new List<(int index, string title, CodeAction action)>();
            var index = 0;

            foreach (var provider in GetRefactoringProviders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var collected = new List<CodeAction>();
                var context = new CodeRefactoringContext(document, span, a => collected.Add(a), cancellationToken);
                try
                {
                    await provider.ComputeRefactoringsAsync(context).ConfigureAwait(false);
                }
                catch { /* skip */ }
                foreach (var a in collected)
                    foreach (var (title, action) in FlattenAction(a))
                        actions.Add((index++, title, action));
            }

            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            foreach (var provider in GetCodeFixProviders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fixableIds = provider.FixableDiagnosticIds;
                var diagnostics = compilation?.GetDiagnostics(cancellationToken).Where(d => d.Location.SourceTree?.FilePath != null && NormalizePath(d.Location.SourceTree.FilePath) == targetPath && d.Location.SourceSpan.IntersectsWith(span)).ToImmutableArray() ?? ImmutableArray<Diagnostic>.Empty;
                foreach (var diagnostic in diagnostics)
                {
                    if (!fixableIds.Contains(diagnostic.Id)) continue;
                    var collected = new List<CodeAction>();
                    var context = new CodeFixContext(document, diagnostic, (a, _) => collected.Add(a), cancellationToken);
                    try
                    {
                        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
                    }
                    catch { /* skip */ }
                    foreach (var a in collected)
                        foreach (var (title, action) in FlattenAction(a))
                            actions.Add((index++, title, action));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(docInfo);
            sb.AppendLine("# Code actions at position");
            sb.AppendLine();
            if (actions.Count == 0)
            {
                sb.AppendLine("(no code actions at this position)");
                return sb.ToString();
            }
            foreach (var (i, title, _) in actions)
                sb.AppendLine($"{i}\t{title}");
            sb.AppendLine().AppendLine($"Total: {actions.Count}. Use roslyn_apply_code_action with action_index (0-based). For code fixes, optional fix_all_scope: document | project | solution.");
            return sb.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("slnx") || ex.Message.Contains("Slnx"))
        {
            return "Error: .slnx format is not supported. Use .sln or .csproj.";
        }
        finally
        {
            solution?.Workspace.Dispose();
        }
    }

    public static async Task<string> ApplyCodeActionAsync(
        string solutionOrProjectPath,
        string filePath,
        int line,
        int column,
        int actionIndex,
        string? fixAllScope = null,
        string? constantName = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionOrProjectPath))
            return $"Error: solution/project not found: {solutionOrProjectPath}";
        if (!File.Exists(filePath))
            return $"Error: file not found: {filePath}";

        var targetPath = NormalizePath(filePath);
        Solution? solution = null;
        try
        {
            var workspace = MSBuildWorkspace.Create();
            if (string.Equals(Path.GetExtension(solutionOrProjectPath), ".sln", StringComparison.OrdinalIgnoreCase))
                solution = await workspace.OpenSolutionAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            else
                solution = (await workspace.OpenProjectAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false)).Solution;

            if (solution is null)
                return "Error: failed to open solution.";

            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => string.Equals(NormalizePath(d.FilePath ?? ""), targetPath, StringComparison.OrdinalIgnoreCase));
            if (document is null)
                return $"Error: file not found in solution: {filePath}";

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var lines = sourceText.Lines;
            if (line < 1 || line > lines.Count)
                return $"Error: line {line} out of range.";
            var lineInfo = lines[line - 1];
            var columnIndex = column - 1;
            var lineLen = lineInfo.Span.Length;
            if (columnIndex < 0)
                return $"Error: column {column} must be >= 1.";
            var position = lineLen == 0
                ? lineInfo.Start
                : lineInfo.Start + Math.Min(columnIndex, lineLen);
            var rootApply = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var span = rootApply is null
                ? new TextSpan(position, 0)
                : rootApply.FindToken(position, findInsideTrivia: true).Span;

            var docPathApply = document.FilePath ?? filePath;
            var docInfoApply = $"# Document: {docPathApply} (total_lines={lines.Count}, line_{line}_length={lineLen})";

            var actionsWithOrigin = new List<(CodeAction action, CodeFixProvider? provider, Diagnostic? diagnostic)>();

            foreach (var provider in GetRefactoringProviders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var collected = new List<CodeAction>();
                var context = new CodeRefactoringContext(document, span, a => collected.Add(a), cancellationToken);
                try { await provider.ComputeRefactoringsAsync(context).ConfigureAwait(false); } catch { }
                foreach (var a in collected)
                    foreach (var (_, action) in FlattenAction(a))
                        actionsWithOrigin.Add((action, null, null));
            }

            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var diagnostics = compilation?.GetDiagnostics(cancellationToken).Where(d => d.Location.SourceTree?.FilePath != null && NormalizePath(d.Location.SourceTree.FilePath) == targetPath && d.Location.SourceSpan.IntersectsWith(span)).ToImmutableArray() ?? ImmutableArray<Diagnostic>.Empty;

            foreach (var provider in GetCodeFixProviders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fixableIds = provider.FixableDiagnosticIds;
                foreach (var diagnostic in diagnostics)
                {
                    if (!fixableIds.Contains(diagnostic.Id)) continue;
                    var collected = new List<CodeAction>();
                    var context = new CodeFixContext(document, diagnostic, (a, _) => collected.Add(a), cancellationToken);
                    try { await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false); } catch { }
                    foreach (var a in collected)
                        foreach (var (_, action) in FlattenAction(a))
                            actionsWithOrigin.Add((action, provider, diagnostic));
                }
            }

            if (actionIndex < 0 || actionIndex >= actionsWithOrigin.Count)
                return $"Error: action_index {actionIndex} out of range (0..{actionsWithOrigin.Count - 1}).";

            var (chosen, codeFixProvider, triggerDiagnostic) = actionsWithOrigin[actionIndex];

            if (!string.IsNullOrWhiteSpace(fixAllScope) && codeFixProvider != null && triggerDiagnostic != null)
            {
                var fixAllProvider = codeFixProvider.GetFixAllProvider();
                if (fixAllProvider != null && TryParseFixAllScope(fixAllScope.Trim(), out var scope))
                {
                    var equivalenceKey = chosen.EquivalenceKey;
                    var diagnosticIds = codeFixProvider.FixableDiagnosticIds;
                    var diagnosticProvider = new CompilationDiagnosticProvider();
                    try
                    {
                        var fixAllContext = new FixAllContext(document, codeFixProvider, scope, equivalenceKey, diagnosticIds, diagnosticProvider, cancellationToken);
                        var fixAllAction = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
                        if (fixAllAction != null)
                        {
                            var operations = await fixAllAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
                            foreach (var op in operations)
                            {
                                if (op is ApplyChangesOperation applyOp)
                                {
                                    var newSolution = applyOp.ChangedSolution;
                                    foreach (var projectId in newSolution.ProjectIds)
                                    {
                                        var project = newSolution.GetProject(projectId);
                                        if (project is null) continue;
                                        foreach (var docId in project.DocumentIds)
                                        {
                                            var doc = newSolution.GetDocument(docId);
                                            if (doc?.FilePath is null) continue;
                                            var newText = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                                            await File.WriteAllTextAsync(doc.FilePath, newText.ToString(), cancellationToken).ConfigureAwait(false);
                                        }
                                    }
                                    return $"{docInfoApply}\nApplied: {fixAllAction.Title} (Fix all in {scope}).\nFiles updated.";
                                }
                            }
                            return $"{docInfoApply}\nApplied: {fixAllAction.Title} (Fix all in {scope}, no document changes).";
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"{docInfoApply}\nError: Fix all failed: {ex.Message}";
                    }
                }
            }

            IEnumerable<CodeActionOperation>? operationsSingle = null;
            if (!string.IsNullOrWhiteSpace(constantName) && TryGetOperationsWithOptions(chosen, constantName.Trim(), cancellationToken, out var opsWithOptions))
                operationsSingle = opsWithOptions;
            if (operationsSingle is null)
                operationsSingle = await chosen.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var op in operationsSingle)
            {
                if (op is ApplyChangesOperation applyOp)
                {
                    var newSolution = applyOp.ChangedSolution;
                    foreach (var projectId in newSolution.ProjectIds)
                    {
                        var project = newSolution.GetProject(projectId);
                        if (project is null) continue;
                        foreach (var docId in project.DocumentIds)
                        {
                            var doc = newSolution.GetDocument(docId);
                            if (doc?.FilePath is null) continue;
                            var newText = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                            await File.WriteAllTextAsync(doc.FilePath, newText.ToString(), cancellationToken).ConfigureAwait(false);
                        }
                    }
                    return $"{docInfoApply}\nApplied: {chosen.Title}\nFiles updated.";
                }
            }
            return $"{docInfoApply}\nApplied: {chosen.Title} (no document changes).";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("slnx") || ex.Message.Contains("Slnx"))
        {
            return "Error: .slnx format is not supported.";
        }
        finally
        {
            solution?.Workspace.Dispose();
        }
    }
}

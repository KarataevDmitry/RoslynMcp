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

namespace RoslynMcp;

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
            var span = new TextSpan(position, 0);

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
                {
                    var title = a.Title;
                    if (string.IsNullOrEmpty(title)) title = "(no title)";
                    actions.Add((index++, title, a));
                }
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
                    try
                    {
                        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
                    }
                    catch { /* skip */ }
                    foreach (var a in collected)
                    {
                        var title = a.Title;
                        if (string.IsNullOrEmpty(title)) title = "(no title)";
                        actions.Add((index++, title, a));
                    }
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
            sb.AppendLine().AppendLine($"Total: {actions.Count}. Use roslyn_apply_code_action with action_index (0-based).");
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
            var span = new TextSpan(position, 0);

            var docPathApply = document.FilePath ?? filePath;
            var docInfoApply = $"# Document: {docPathApply} (total_lines={lines.Count}, line_{line}_length={lineLen})";

            var actions = new List<CodeAction>();

            foreach (var provider in GetRefactoringProviders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var collected = new List<CodeAction>();
                var context = new CodeRefactoringContext(document, span, a => collected.Add(a), cancellationToken);
                try { await provider.ComputeRefactoringsAsync(context).ConfigureAwait(false); } catch { }
                actions.AddRange(collected);
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
                    actions.AddRange(collected);
                }
            }

            if (actionIndex < 0 || actionIndex >= actions.Count)
                return $"Error: action_index {actionIndex} out of range (0..{actions.Count - 1}).";

            var chosen = actions[actionIndex];
            var operations = await chosen.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
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

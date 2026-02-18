using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace RoslynMcp.Services;

/// <summary>Диагностики компиляции (ошибки, предупреждения) по solution/project. Опционально — только по одному файлу.</summary>
public static class GetDiagnostics
{
    private static string NormalizePath(string path)
    {
        var p = Path.GetFullPath(path.Trim());
        if (p.EndsWith(Path.DirectorySeparatorChar))
            p = p.TrimEnd(Path.DirectorySeparatorChar);
        return p;
    }

    public static async Task<string> GetDiagnosticsAsync(
        string solutionOrProjectPath,
        string? filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionOrProjectPath))
            return $"Error: solution/project not found: {solutionOrProjectPath}";

        string? targetPath = null;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            if (!File.Exists(filePath))
                return $"Error: file not found: {filePath}";
            targetPath = NormalizePath(filePath);
        }

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

            var allDiagnostics = new List<Diagnostic>();
            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var comp = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                if (comp is null) continue;
                foreach (var d in comp.GetDiagnostics(cancellationToken))
                {
                    if (d.Location.SourceTree?.FilePath is null) continue;
                    var treePath = NormalizePath(d.Location.SourceTree.FilePath);
                    if (targetPath is not null && !string.Equals(treePath, targetPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    allDiagnostics.Add(d);
                }
                var analyzers = project.AnalyzerReferences
                    .SelectMany(r => r.GetAnalyzers(project.Language))
                    .ToImmutableArray();
                if (!analyzers.IsEmpty)
                {
                    var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
                    var options = new CompilationWithAnalyzersOptions(
                        analyzerOptions,
                        onAnalyzerException: (_, _, _) => { },
                        concurrentAnalysis: true,
                        logAnalyzerExecutionTime: false,
                        reportSuppressedDiagnostics: false);
                    var cwa = new CompilationWithAnalyzers(comp, analyzers, options);
                    var result = await cwa.GetAnalysisResultAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var d in result.GetAllDiagnostics())
                    {
                        if (d.Location.SourceTree?.FilePath is null) continue;
                        var treePath = NormalizePath(d.Location.SourceTree.FilePath);
                        if (targetPath is not null && !string.Equals(treePath, targetPath, StringComparison.OrdinalIgnoreCase))
                            continue;
                        allDiagnostics.Add(d);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Diagnostics (compiler + analyzers)");
            if (targetPath is not null)
                sb.AppendLine($"# File: {filePath}");
            sb.AppendLine("# Format: file:line:column severity id — message");
            sb.AppendLine();

            foreach (var d in allDiagnostics.OrderBy(x => x.Location.SourceTree?.FilePath ?? "").ThenBy(x => x.Location.SourceSpan.Start))
            {
                var tree = d.Location.SourceTree;
                var file = tree?.FilePath ?? "(no file)";
                var line = 0;
                var column = 0;
                if (tree is not null && d.Location.IsInSource)
                {
                    var lineSpan = d.Location.GetLineSpan();
                    line = lineSpan.StartLinePosition.Line + 1;
                    column = lineSpan.StartLinePosition.Character + 1;
                }
                var severity = d.Severity switch
                {
                    DiagnosticSeverity.Error => "error",
                    DiagnosticSeverity.Warning => "warning",
                    DiagnosticSeverity.Info => "info",
                    DiagnosticSeverity.Hidden => "hidden",
                    _ => "unknown"
                };
                sb.AppendLine($"{file}:{line}:{column} {severity} {d.Id} — {d.GetMessage()}");
            }

            sb.AppendLine();
            sb.AppendLine("# Prefer this over parsing build logs for compiler/analyzer errors.");
            sb.AppendLine("# To fix: use file:line:column with roslyn_get_code_actions, then roslyn_apply_code_action (or fix_all_scope).");
            sb.AppendLine();
            if (allDiagnostics.Count == 0)
                sb.AppendLine("(no diagnostics)");
            else
                sb.AppendLine($"Total: {allDiagnostics.Count}");

            return sb.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("slnx") || ex.Message.Contains("Slnx"))
        {
            return "Error: .slnx format is not supported. Use .sln or open by .csproj.";
        }
        finally
        {
            solution?.Workspace.Dispose();
        }
    }
}

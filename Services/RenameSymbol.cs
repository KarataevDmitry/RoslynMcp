using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace RoslynMcp.Services;

/// <summary>Переименование символа по solution. preview (apply=false) — только список изменений; apply=true — запись в файлы.</summary>
public static class RenameSymbol
{
    private static string NormalizePath(string path)
    {
        var p = Path.GetFullPath(path.Trim());
        if (p.EndsWith(Path.DirectorySeparatorChar))
            p = p.TrimEnd(Path.DirectorySeparatorChar);
        return p;
    }

    public static async Task<string> RenameAsync(
        string solutionOrProjectPath,
        string filePath,
        int line,
        int column,
        string newName,
        bool apply,
        bool renameInComments = false,
        bool renameInStrings = false,
        bool renameOverloads = false,
        bool renameFile = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionOrProjectPath))
            return $"Error: solution/project not found: {solutionOrProjectPath}";
        if (!File.Exists(filePath))
            return $"Error: file not found: {filePath}";
        if (string.IsNullOrWhiteSpace(newName))
            return "Error: new_name is required.";

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

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (root is null || semanticModel is null)
                return "Error: could not get syntax/semantic model.";

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var lines = sourceText.Lines;
            if (line < 1 || line > lines.Count)
                return $"Error: line {line} out of range (1..{lines.Count}).";
            var lineInfo = lines[line - 1];
            var columnIndex = column - 1;
            if (columnIndex < 0)
                return $"Error: column {column} must be >= 1.";
            var lineLen = lineInfo.Span.Length;
            var position = lineLen == 0 ? lineInfo.Start : lineInfo.Start + Math.Min(columnIndex, lineLen);

            var node = root.FindToken(position, findInsideTrivia: true).Parent;
            ISymbol? symbol = null;
            while (node != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken) ?? semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
                if (symbol != null) break;
                node = node.Parent;
            }
            if (symbol is null)
                return $"No symbol at {filePath}:{line}:{column}.";

            var options = new SymbolRenameOptions(renameOverloads, renameInStrings, renameInComments, renameFile);
            var newSolution = await Renamer.RenameSymbolAsync(solution, symbol, options, newName, cancellationToken).ConfigureAwait(false);

            var sb = new StringBuilder();
            var docPath = document.FilePath ?? filePath;
            sb.AppendLine($"# Document: {docPath} (total_lines={lines.Count}, line_{line}_length={lineLen})");
            sb.AppendLine($"# Rename {symbol.Kind} {symbol.Name} → {newName}");
            sb.AppendLine();

            var changed = new List<Document>();
            foreach (var project in newSolution.Projects)
            {
                foreach (var doc in project.Documents)
                {
                    if (doc.FilePath is null) continue;
                    var oldDoc = solution.GetDocument(doc.Id);
                    if (oldDoc is null) continue;
                    var oldText = await oldDoc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var newText = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    if (!oldText.ContentEquals(newText))
                        changed.Add(doc);
                }
            }

            if (changed.Count == 0)
            {
                sb.AppendLine("(no changes)");
                return sb.ToString();
            }

            var applied = new List<string>();
            foreach (var doc in changed)
            {
                if (apply)
                {
                    var newText = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    await File.WriteAllTextAsync(doc.FilePath!, newText.ToString(), cancellationToken).ConfigureAwait(false);
                    applied.Add(doc.FilePath!);
                }
                else
                    sb.AppendLine(doc.FilePath);
            }

            if (apply)
            {
                sb.AppendLine("Applied to:");
                foreach (var p in applied)
                    sb.AppendLine("  " + p);
            }
            else
            {
                sb.AppendLine().AppendLine($"Total: {changed.Count} file(s). Call with apply: true to write.");
            }

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

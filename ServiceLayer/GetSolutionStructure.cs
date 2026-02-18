using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RoslynMcp.ServiceLayer;

/// <summary>Структура solution: список проектов (имя, путь к .csproj). Только чтение, без загрузки компиляции.</summary>
public static class GetSolutionStructure
{
    public static async Task<string> GetStructureAsync(
        string solutionOrProjectPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionOrProjectPath))
            return $"Error: solution/project not found: {solutionOrProjectPath}";

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

            var sb = new StringBuilder();
            sb.AppendLine("# Solution structure");
            sb.AppendLine($"# Path: {solutionOrProjectPath}");
            sb.AppendLine("# Projects (name, path to .csproj) — use solution_or_project_path in other tools.");
            sb.AppendLine();

            var index = 0;
            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = project.FilePath ?? "";
                sb.AppendLine($"{index}. {project.Name}");
                sb.AppendLine($"   {path}");
                sb.AppendLine();
                index++;
            }

            sb.AppendLine($"Total: {solution.ProjectIds.Count} project(s).");
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
}

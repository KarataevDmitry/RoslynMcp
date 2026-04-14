using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using RoslynMcp.ServiceLayer;

namespace RoslynMcp.Tests;

/// <summary>
/// Интеграция с реальным solution (CascadeIDE): MVVM Toolkit генерирует свойства; без MSBuildLocator + GetSourceGeneratedDocuments
/// <see cref="GetDiagnostics"/> мог бы выдавать ложный CS1061.
/// </summary>
public sealed class GetDiagnosticsSourceGeneratorsIntegrationTests
{
    static GetDiagnosticsSourceGeneratorsIntegrationTests()
    {
        // Один раз на процесс: после загрузки сборок MSBuild повторный RegisterDefaults падает.
        MSBuildLocator.RegisterDefaults();
    }

    private static string? TryGetCascadeIdePaths()
    {
        // RoslynMcp.Tests/bin/Debug/net10.0 → …/open (родитель roslyn-mcp)
        var openDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var sln = Path.Combine(openDir, "cascade-ide", "CascadeIDE.sln");
        var file = Path.Combine(openDir, "cascade-ide", "Services", "CockpitSurfaceSnapshotBuilder.cs");
        return File.Exists(sln) && File.Exists(file) ? $"{sln}|{file}" : null;
    }

    [Fact]
    public async Task CascadeIDE_semantic_model_has_no_cs1061_for_mvvm_generated_property_access()
    {
        var paths = TryGetCascadeIdePaths();
        if (paths is null)
            return;

        var parts = paths.Split('|');
        var solutionPath = parts[0];
        var filePath = parts[1];

        var workspace = MSBuildWorkspace.Create(RoslynMcpWorkspaceProperties.MsBuild);
        try
        {
            var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: CancellationToken.None).ConfigureAwait(true);
            var document = solution.Projects.SelectMany(p => p.Documents)
                .First(d => string.Equals(Normalize(d.FilePath), Normalize(filePath), StringComparison.OrdinalIgnoreCase));
            var projectId = document.Project.Id;
            await workspace.CurrentSolution.GetProject(projectId)!.GetSourceGeneratedDocumentsAsync(CancellationToken.None).ConfigureAwait(true);
            document = workspace.CurrentSolution.GetDocument(document.Id)
                ?? throw new InvalidOperationException("Document missing after source generation.");
            var semantic = await document.GetSemanticModelAsync(CancellationToken.None).ConfigureAwait(true);
            Assert.NotNull(semantic);
            var diags = semantic.GetDiagnostics();
            Assert.DoesNotContain(diags, d => d.Id == "CS1061" && d.GetMessage().Contains("UiMode", StringComparison.Ordinal));
        }
        finally
        {
            workspace.Dispose();
        }

        static string Normalize(string? p) =>
            string.IsNullOrEmpty(p) ? "" : Path.GetFullPath(p);
    }

    [Fact]
    public async Task CascadeIDE_GetDiagnostics_text_matches_semantic_model_no_false_cs1061()
    {
        var paths = TryGetCascadeIdePaths();
        if (paths is null)
            return;

        var parts = paths.Split('|');
        var solutionPath = parts[0];
        var filePath = parts[1];

        var text = await GetDiagnostics.GetDiagnosticsAsync(solutionPath, filePath, CancellationToken.None).ConfigureAwait(true);

        Assert.False(text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase), text);
        Assert.DoesNotContain("CS1061", text, StringComparison.Ordinal);
    }
}

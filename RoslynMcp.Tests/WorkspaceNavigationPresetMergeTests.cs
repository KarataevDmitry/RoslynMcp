using RoslynMcp.ServiceLayer.WorkspaceNavigation;

namespace RoslynMcp.Tests;

public sealed class WorkspaceNavigationPresetMergeTests
{
    [Fact]
    public void Merge_peers_only_yields_include_partial_and_project_peer()
    {
        var (inc, exc, err) = WorkspaceNavigationPresetMerge.Merge(
            "peers_only",
            BundledWorkspaceNavigationPresets.Json,
            requestInclude: null,
            requestExclude: null);
        Assert.Null(err);
        Assert.NotNull(inc);
        Assert.Contains(WorkspaceNavigationRelatedKinds.PartialPeer, inc);
        Assert.Contains(WorkspaceNavigationRelatedKinds.ProjectPeer, inc);
        Assert.Equal(2, inc!.Count);
        Assert.Empty(exc!);
    }

    [Fact]
    public void Merge_unknown_preset_returns_error()
    {
        var (_, _, err) = WorkspaceNavigationPresetMerge.Merge(
            "no_such_preset",
            BundledWorkspaceNavigationPresets.Json,
            null,
            null);
        Assert.NotNull(err);
        Assert.Contains("Неизвестный пресет", err, StringComparison.Ordinal);
    }
}

# Publish Release (win-x64, self-contained) and mirror to a fixed path for Cursor MCP.
# Run from repo:  cd ...\roslyn-mcp  ;  .\publish-and-deploy.ps1
# Optional: -Target "D:\roslyn-mcp"
[CmdletBinding()]
param(
    [string] $Target = "D:\roslyn-mcp"
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$csproj = Join-Path $here "RoslynMcp.csproj"
if (-not (Test-Path -LiteralPath $csproj)) {
    Write-Error "RoslynMcp.csproj not found. Run this script from the roslyn-mcp directory (PSScriptRoot=$here)."
    exit 1
}

Push-Location $here
try {
    # Keep docs/manifests in sync with ToolCatalog.
    & dotnet run --project (Join-Path $here "tools\\ExportMcpManifest\\ExportMcpManifest.csproj") -- --write | Out-Null
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Prefer local tool (repo-pinned), but global install works too.
    if (Test-Path -LiteralPath (Join-Path $here ".config\\dotnet-tools.json")) {
        & dotnet tool restore | Out-Null
        & dotnet aid-publish -Project $csproj -Target $Target -Runtime "win-x64" -Configuration "Release" -SelfContained -KillRunning `
            -RequireMirrorFile BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll
    } else {
        & aid-publish -Project $csproj -Target $Target -Runtime "win-x64" -Configuration "Release" -SelfContained -KillRunning `
            -RequireMirrorFile BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Microsoft.CodeAnalysis.Workspaces.MSBuild 4.9+: out-of-process BuildHost must sit next to the exe.
    # aid-publish mirrors the full publish tree — if BuildHost-netcore was ever missing here, MCP failed at roslyn_* with "build host could not be found".
    $buildHostDll = Join-Path $Target "BuildHost-netcore\Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll"
    if (-not (Test-Path -LiteralPath $buildHostDll)) {
        Write-Error "Publish incomplete: MSBuild Workspace BuildHost missing: $buildHostDll`nRun dotnet publish manually with -r win-x64 --self-contained true or fix SDK output.`nIf aid-publish is older than 0.1.2, dotnet tool restore and pin AIGuiders.DotnetTools.PublishFixedTarget 0.1.2+."
        exit 1
    }

    $exe = Join-Path $Target "RoslynMcp.exe"
    $exeJson = $exe.Replace('\', '\\')
    Write-Host ""
    Write-Host "Cursor MCP: paste into mcp.json ->"
    Write-Host @"
  "roslyn-mcp": {
    "command": "$exeJson",
    "args": []
  }
"@
    Write-Host ""
} finally {
    Pop-Location
}


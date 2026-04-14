# Source generators и `roslyn_get_diagnostics` (Community Toolkit MVVM и др.)

## Симптом

По файлам, которые ссылаются на **члены, сгенерированные** Roslyn source generators (например `[ObservableProperty]` / `[RelayCommand]` в **CommunityToolkit.Mvvm**), `roslyn_get_diagnostics` мог выдавать **ложные** ошибки вроде:

`CS1061: 'MainWindowViewModel' does not contain a definition for 'UiMode'…`

При этом `dotnet build` того же проекта проходит.

## Почему так

- Сгенерированные деревья не входят в `Project.Documents`; их нужно получать через `Project.GetSourceGeneratedDocumentsAsync` (см. [документацию API](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.project.getsourcegenerateddocumentsasync)).
- После вызова генераторов обновляется **`Workspace.CurrentSolution`**. Старый снимок `Project` остаётся **без** сгенерированных синтаксических деревьев; компиляция и семантическая модель должны браться из **актуального** решения (`workspace.CurrentSolution.GetProject(projectId)` и далее `GetCompilationAsync` / `GetSemanticModelAsync`).
- Загрузка solution через `MSBuildWorkspace` должна быть согласована с установленным SDK: пакеты **`Microsoft.CodeAnalysis.*` / `Microsoft.CodeAnalysis.Workspaces.MSBuild`** (в RoslynMcp — **5.3.0**) выровнены под **.NET 10 / SDK 10**; устаревший Workspaces.MSBuild мог давать компиляцию без корректного конвейёра source generators для целевых проектов.
- Для MSBuild: те же глобальные свойства, что передаёт любой вызов `MSBuildWorkspace` в RoslynMcp — см. **`ServiceLayer/RoslynMcpWorkspaceProperties.cs`** (`DesignTimeBuild=false`, `SkipCompilerExecution=false`, `RoslynMcpWorkspace=true`). Регистрация **`Microsoft.Build.Locator`** до открытия workspace — в `Program.cs`.

## Реализация в RoslynMcp

`ServiceLayer/GetDiagnostics.cs`: после `GetSourceGeneratedDocumentsAsync` проект перезапрашивается из `workspace.CurrentSolution`, затем строится `Compilation` и диагностики.

## Временной обходной путь для агента

Если вывод инструмента всё же противоречит `dotnet build`, ориентироваться на результат сборки.

## Опционально в целевом приложении (отладка генераторов)

В `.csproj` можно включить запись сгенерированных файлов на диск (удобно людям, не заменяет корректную интеграцию в workspace):

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

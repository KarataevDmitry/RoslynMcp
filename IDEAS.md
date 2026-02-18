# Идеи для Roslyn MCP

## Поиск символа по имени по solution (как VS 2022 Code Search)

**Источник:** Visual Studio 2022 Code Search (Ctrl+Q) — фильтры `t:` (тип), `m:` (метод), `x:` (текст).

**Идея:** тул в духе **roslyn_find_symbol** (или **roslyn_search_symbol**): по имени найти определение по всему решению, без подбора file:line:column. Параметры: `solution_or_project_path`, `query` (имя или подстрока), опционально `kind` (type | method | all). Ответ — список `file:line:column` определений.

**Зачем:** агенту не нужно угадывать позицию; можно запросить «тип GoToDefinition» / «метод GoToDefinitionAsync» и по результату вызвать go_to_definition или прочитать код.

**Реализация:** в Roslyn есть SymbolFinder или обход по Compilation.GlobalNamespace; фильтрация по Name и Kind (NamedType, Method), сбор Locations в исходниках. Сложность средняя.

---

## Работа с .sln (структура solution)

**Смысл:** сейчас все тулы принимают путь к .sln или .csproj, но агент не знает «что в solution» без ручного парсинга .sln. Полезно дать тул «структура solution».

**Идея (read-only):** **roslyn_get_solution_structure** (или **roslyn_list_projects**): параметр `solution_path` (.sln). Ответ: список проектов — имя, путь к .csproj, опционально Id. Без загрузки компиляции — только открыть solution через MSBuildWorkspace и пройти `solution.Projects` (Project.Name, Project.FilePath). Агент сразу видит, какие проекты в решении и какой путь передавать в остальные тулы.

**Реализация:** тот же паттерн, что в GetDiagnostics/FindUsages — открыть solution, обойти Projects, вернуть JSON/текст. Сложность низкая.

**Редактирование .sln (add/remove project):** отдельная задача. Для **чтения** .sln API есть: **Microsoft.Build.Construction.SolutionFile** (пакет Microsoft.Build) — `SolutionFile.Parse(path)` возвращает объект с `ProjectsInOrder`, `ProjectsByGuid`, `SolutionConfigurations`; ручной парсинг не нужен. Для **записи** (добавить/удалить проект и сохранить .sln) в публичном API MSBuild готового метода нет: SolutionFile read-only, сериализации обратно в файл не экспонируется. Варианты: формирование .sln по формату вручную, или другой API (например EnvDTE в контексте VS). Имеет смысл только при появлении сценариев «добавь/убери проект в solution».

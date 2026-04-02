# Roslyn MCP — каталог тулов

<!-- GENERATED:ToolCatalog START -->

> Автогенерация из `ToolCatalog.Build()` в репозитории. Не править этот блок вручную.
>
> Обновление: из каталога `roslyn-mcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.
>
> Тексты совпадают с полем `description` у инструментов MCP; полная схема аргументов — в `inputSchema` (например через `list_tools`).

### `roslyn_ping`

Проверка доступности сервера.

### `roslyn_get_document_symbols`

Структура C# файла: namespace, class, method, property, field с номерами строк.

### `roslyn_get_symbol_at_position`

Символ в позиции (файл, строка, столбец — 1-based): kind и имя.

### `roslyn_find_usages`

Все ссылки на символ в solution/project. line/column — на идентификаторе (имя метода, класса и т.д.), не на типе в сигнатуре.

### `roslyn_go_to_definition`

Переход к определению символа: по позиции возвращает file:line:column объявления (1-based). Параметры: solution_or_project_path, file_path, line, column.

### `roslyn_rename`

Переименовать символ по solution. apply: false — превью; true — записать. Чтобы затронуть комментарии/строки/перегрузки/файл — передай rename_in_comments, rename_in_strings, rename_overloads, rename_file (bool).

### `roslyn_get_code_actions`

Список Quick Actions / рефакторингов в позиции (как лампочка в VS). Параметры: solution_or_project_path, file_path, line, column. Возвращает нумерованный список; применить — roslyn_apply_code_action с action_index.

### `roslyn_apply_code_action`

Применить выбранное code action по индексу из roslyn_get_code_actions. Параметры: solution_or_project_path, file_path, line, column, action_index (0-based).

### `roslyn_get_diagnostics`

Диагностики компиляции и анализаторов по solution/project (file:line:column, severity, id, message). Предпочтительно использовать вместо ReadLints/ручного просмотра для поиска неиспользуемых переменных (CS0219), предупреждений компилятора и анализаторов. Чтобы исправить: roslyn_get_code_actions по file:line:column из ответа, затем roslyn_apply_code_action (или fix_all_scope). Параметры: solution_or_project_path, опционально file_path.

### `roslyn_get_solution_structure`

Структура solution: список проектов (имя, путь к .csproj). Параметр: solution_or_project_path (.sln или .csproj). Обходной путь для реп с .slnx: передай путь к главному .csproj — вернётся полный список подгруженных проектов. Использовать, чтобы узнать состав решения и пути для остальных тулов.

### `roslyn_sync_namespaces`

Привести объявления namespace к структуре папок (RootNamespace + путь). Вызывать после переименования или перемещения папок в C# проекте: сначала dry_run для превью, затем без dry_run для применения. Обновляет также using в остальных файлах. Параметры: solution_or_project_path; опционально project_path, dry_run.

### `roslyn_resolve_breakpoint`

По имени символа (метод, свойство, конструктор) в файле вернуть file:line первой исполняемой строки — место для брейкпоинта. Параметры: solution_or_project_path, file_path, symbol_name.

### `roslyn_generate_interface_from_class`

Сгенерировать C# интерфейс по классу (без диалогов Roslyn). Позиция на класс в file_path:line:column. Опционально: interface_name, output_file_path, member_names (массив).

### `roslyn_generate_base_class_from_class`

Сгенерировать абстрактный базовый класс по классу (без диалогов). Выбранные public-члены — abstract в новом классе. Опционально: base_class_name, output_file_path, member_names.

### `roslyn_generate_overrides`

Generate Overrides без диалога: по позиции на класс генерирует override виртуальных/абстрактных членов базового типа. Опционально: member_names, insert_into_file.

### `roslyn_generate_constructor_from_members`

Generate constructor from members без диалога: по позиции на класс — конструктор по полям/свойствам. Опционально: member_names, insert_into_file.

### `roslyn_generate_equals_gethashcode`

Generate Equals and GetHashCode без диалога: по позиции на класс — override по выбранным полям/свойствам. Опционально: member_names, insert_into_file.

<!-- GENERATED:ToolCatalog END -->


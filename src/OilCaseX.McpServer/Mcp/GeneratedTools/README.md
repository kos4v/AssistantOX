# Generated MCP tools

`OilCaseXApiToolCatalog` строит типизированные `ApiToolDescriptor` из выражений методов
сгенерированного `OilCaseXApiClientGenerated`. В `CreateDescriptors` указываются только
клиент и вызываемый метод; имя MCP tool, title, описание, JSON Schema аргументов и
безопасные флаги (`readOnly`, `destructive`, `idempotent`) выводятся автоматически.

Текущий allow-list публикует:

- `ListWellpadsAsync` → `list_wellpads`;
- `GetWellpadAsync` → `get_wellpad`;
- `GetBoreholeAsync` → `get_borehole`.

Для операций, требующих явного подтверждения, descriptor задаёт политику
`ConfirmationPreparation`. Общий `ConfirmationToolDecorator` выполняет preflight,
создаёт payload hash, audit record и временный confirmation. Сейчас так публикуется
`prepare_create_borehole`; для добавления следующей операции не нужен отдельный
MCP wrapper-класс.

`OilCaseXGenericTools` регистрирует созданные descriptors одним MCP executor-ом. Перед
вызовом он применяет фильтры allow-list/read-only/non-destructive, получает concrete
generated client из request scope, передаёт `CancellationToken`, преобразует аргументы
по сигнатуре метода и применяет стандартный projection результата.

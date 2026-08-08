# Agent Service MCP contract baseline

Источник истины: `OilCaseX.McpServer` через `initialize` и `tools/list`.

Обязательные группы tools для MVP:

| Группа | Tools | Использование |
|---|---|---|
| Read | `list_wellpads`, `get_wellpad`, `get_borehole` | разрешение сущностей и ответы |
| PrepareWrite | `prepare_create_borehole` | preflight и preview |
| ExecuteWrite | `execute_create_borehole` | только explicit confirmation flow |

## Проверки при startup

- MCP handshake успешен;
- required tool names существуют;
- input schemas не стали шире ожидаемого baseline;
- `execute_create_borehole` имеет `confirmationId` и не получает draft payload;
- запрещённые names (`delete`, `reset`, `restore`, `admin`) отсутствуют;
- MCP endpoint соответствует фиксированной конфигурации.

При несовместимом контракте Agent Service становится `NotReady` и не принимает
agent turns.

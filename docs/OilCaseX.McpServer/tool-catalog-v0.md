# Curated MCP tool catalog v0

Каталог ограничивает поверхность, видимую LLM. Swagger не публикуется автоматически.

## Read-only MVP

| MCP name | Тип | API reference baseline | Доступ |
|---|---|---|---|
| `list_wellpads` | Read | `listWellpads` → `GET /Api/V1/Purchased/Wellpad` | роль пользователя + team scope |
| `get_wellpad` | Read | `getWellpad` → `GET /Api/V1/Purchased/Wellpad/{wellpadId}` | роль пользователя + team scope |
| `list_boreholes` | Read | `listBoreholes` → `GET /Api/V1/Purchased/Borehole/All` | роль пользователя + team scope |
| `get_borehole` | Read | `getBorehole` → `GET /Api/V1/Purchased/Borehole/BoreholeInfo/{boreholeId}` | роль пользователя + team scope |
| `get_borehole_production` | Read | `getBoreholeProduction` → `GET /Api/V1/Production/Info/Borehole/{boreholeId}` | роль пользователя + team scope |

## Safe write candidate — пока заблокирован контрактом API

| MCP name | Тип | API reference | Условие включения |
|---|---|---|---|
| `prepare_create_borehole` | PrepareWrite | `createPurchasedBorehole` → `POST /Api/V1/Purchased/Borehole` требует отдельного preflight | API validate/preflight contract |
| `execute_create_borehole` | ExecuteWrite | `createPurchasedBorehole` → `POST /Api/V1/Purchased/Borehole` | confirmation, idempotency и API contract |

Prepare не выполняет запись. Execute нельзя публиковать в production catalog до проверки
прав, payload hash, одноразового confirmation и end-to-end idempotency.

## Исключено из v0

- reset/restore/delete;
- admin и initial configuration;
- password/login/token operations;
- произвольные `Views/*` routes;
- экспорт больших файлов и необрезанные binary payloads;
- dynamic tool names, URL и HTTP method от модели.

## Общий контракт tool

Каждый включённый tool обязан иметь:

- стабильное MCP name и короткое описание;
- минимальную input JSON Schema;
- output contract без внутренних DB/API полей;
- тип `Read`, `PrepareWrite` или `ExecuteWrite`;
- required roles/scopes;
- trusted API mapping;
- timeout, response size limit и audit policy.

# OilCaseX.McpServer — архитектура и план разработки

## 1. Идея

`OilCaseX.McpServer` — отдельный ASP.NET Core сервис, который предоставляет AI-агенту небольшой набор безопасных MCP-tools и преобразует их в вызовы существующего OilCaseX REST API.

Основной сценарий:

1. Пользователь описывает новую скважину естественным языком.
2. Agent Service извлекает параметры и уточняет недостающие значения.
3. Agent вызывает типизированный MCP-tool.
4. MCP проверяет JSON Schema, пользователя, policy и актуальность данных.
5. MCP получает domain validation и preview через OilCaseX REST API.
6. Пользователь подтверждает операцию.
7. MCP вызывает REST API создания скважины.
8. MCP возвращает только фактически подтверждённый API-результат и пишет audit record.

MCP Server не содержит LLM, не принимает продуктовые решения, не имеет доступа к продуктовой БД и не вызывает `OilCaseX.Domain.Services` напрямую.

## 2. Цели

- предоставить агенту устойчивые предметные tools вместо 215 сырых Swagger operations;
- сохранить OilCaseX REST API единственной точкой доступа к бизнес-логике;
- обеспечить типизированные запросы и ответы;
- исключить произвольные URL, endpoints и HTTP-параметры от LLM;
- безопасно делегировать identity пользователя в OilCaseX API;
- разделить read и write операции;
- обеспечить `prepare → preview → confirm → execute`;
- защититься от повторного исполнения через idempotency;
- обеспечить audit и сквозную OpenTelemetry-трассировку;
- обнаруживать несовместимые изменения Swagger до deployment.

## 3. Не входит в ответственность MCP Server

- распознавание естественного языка;
- выбор стратегии диалога и формулирование уточняющих вопросов;
- хранение полного chat history;
- RAG и генерация пользовательского ответа;
- прямой доступ к `OilCaseXContext` или PostgreSQL OilCaseX;
- повторная реализация правил `BoreholeDevelopmentService`;
- автоматическая публикация всех Swagger endpoints как tools;
- выполнение произвольных HTTP-запросов;
- хранение пользовательских паролей или выдача JWT;
- admin/reset/restore/delete операции в первом production scope.

## 4. Архитектурная граница

```text
ASP.NET Core Agent Service
  - LLM orchestration
  - context/clarification
  - MCP client
              |
              | MCP request + delegated user context
              v
OilCaseX.McpServer
  - MCP transport
  - curated tool registry
  - schema validation
  - authorization/policies
  - confirmation/idempotency
  - typed OilCaseX API client
  - response normalization
  - audit/telemetry
              |
              | HTTPS + Bearer JWT + traceparent
              v
OilCaseX REST API
  - authentication/authorization
  - business validation
  - transactions
  - Domain Services
              |
              v
OilCaseX PostgreSQL / RabbitMQ / MinIO
```

Правило зависимостей:

```text
OilCaseX.McpServer → HTTP/OpenAPI contract → OilCaseX.Api

OilCaseX.McpServer -X→ OilCaseX.Domain
OilCaseX.McpServer -X→ OilCaseX.Domain.Services
OilCaseX.McpServer -X→ OilCaseXContext
OilCaseX.McpServer -X→ product PostgreSQL
```

Запрет должен проверяться архитектурным тестом или CI-анализом project references.

## 5. Внутренние компоненты

### 5.1. MCP Transport

Отвечает только за протокол:

- инициализацию MCP session;
- negotiation закреплённой версии протокола;
- получение `tools/list` и `tools/call`;
- request cancellation;
- ограничение размера request/response;
- timeout и correlation ID;
- network transport, поддерживаемый выбранным .NET MCP SDK.

Transport не содержит бизнес-логики и не вызывает OilCaseX API напрямую.

### 5.2. Tool Registry

Хранит явный curated allow-list:

- имя MCP-tool;
- описание для модели;
- input JSON Schema;
- output contract;
- тип операции: `Read`, `PrepareWrite`, `ExecuteWrite`;
- required permissions;
- связанный OpenAPI `operationId`;
- handler;
- timeout и максимальный размер ответа;
- audit policy.

Каталог создаётся в коде. LLM, Swagger или пользователь не могут динамически добавить tool.

### 5.3. Tool Dispatcher

Единый pipeline выполнения:

```text
MCP request
  → tool existence check
  → JSON Schema validation
  → user context validation
  → tool policy
  → rate/concurrency limit
  → confirmation/idempotency check
  → tool handler
  → OilCaseX API adapter
  → response normalization
  → audit + telemetry
  → MCP response
```

Каждый handler получает типизированный command и `McpExecutionContext`, а не сырой JSON и не `HttpContext`.

### 5.4. Authentication Context

Agent Service передаёт MCP Server пользовательский access token вне LLM prompt/tool arguments.

MCP Server:

- проверяет наличие и формат delegated identity;
- не логирует JWT;
- извлекает минимальные claims: `sub`, role, team ID, token expiry;
- использует identity при проверке tool policy;
- передаёт исходный Bearer token в OilCaseX REST API;
- считает OilCaseX API окончательной точкой авторизации;
- не использует service account вместо пользователя для пользовательских операций.

Нужно защититься от confused deputy: confirmation, idempotency и audit всегда связываются с конкретными `sub` и `teamId`.

### 5.5. Policy Engine

Policy не зависит от решения LLM.

Проверки:

- tool находится в allow-list;
- роль имеет право видеть/выполнять tool;
- операция разрешена в текущем environment;
- read tool не может вызвать write endpoint;
- execute tool требует подтверждение;
- admin/destructive endpoint запрещён;
- arguments не содержат URL, SQL или неизвестных полей;
- пользователь и confirmation относятся к одной команде;
- request не превышает rate/concurrency limits.

### 5.6. OilCaseX API Client

Требования:

- typed client через `HttpClientFactory`;
- контракт генерируется или проверяется по versioned OpenAPI snapshot;
- фиксированный `BaseAddress` из trusted configuration;
- URI строится только кодом handler;
- Bearer token добавляется delegating handler;
- `traceparent`, correlation ID и idempotency key передаются отдельными headers;
- JSON serialization согласована с OilCaseX API;
- response body имеет жёсткий size limit;
- internal HTML/stack trace не возвращается LLM;
- timeout задаётся отдельно для каждого класса операции;
- automatic retry разрешён только для безопасных GET;
- POST не повторяется автоматически без end-to-end idempotency в API.

### 5.7. Confirmation Manager

Write tools работают в два этапа.

#### Prepare

1. Проверить shape входных данных.
2. Получить актуальные данные из OilCaseX API.
3. Вызвать validate/preflight endpoint API.
4. Сформировать нормализованный preview.
5. Создать короткоживущий confirmation token.

#### Execute

1. Проверить подпись/наличие token.
2. Проверить expiry и одноразовость.
3. Сопоставить `sub`, `teamId`, tool name и payload hash.
4. При необходимости повторить preflight.
5. Передать idempotency key в OilCaseX API.
6. Выполнить POST.
7. Пометить confirmation как использованный.
8. Вернуть подтверждённый API-результат.

Confirmation record содержит:

- `confirmationId`/`jti`;
- `subjectId`;
- `teamId`;
- `toolName`;
- hash канонического payload;
- preview;
- API resource version/ETag, если доступен;
- `createdAt`, `expiresAt`, `usedAt`;
- idempotency key;
- trace ID.

Для MVP допустим memory store только в single-instance development. Для staging/production нужен отдельный operational store MCP, например Redis или отдельная схема PostgreSQL, не являющаяся продуктовой БД OilCaseX.

### 5.8. Idempotency

MCP-level защита недостаточна: запрос может дойти до API, а ответ потеряться. Поэтому idempotency должна быть end-to-end.

Требования к OilCaseX API:

- принимать `Idempotency-Key` для write endpoint;
- атомарно сохранять ключ и результат вместе с транзакцией;
- при повторном ключе возвращать тот же результат;
- отклонять повторный ключ с другим payload hash;
- иметь retention policy.

До появления API-level idempotency write tool нельзя считать production-ready.

### 5.9. Audit

Audit record создаётся для:

- prepare;
- confirm/cancel;
- execute success;
- execute failure;
- blocked policy action;
- invalid/replayed confirmation;
- API authorization failure.

Audit содержит идентификаторы, но не JWT, passwords и чувствительные response bodies.

### 5.10. Observability

Сквозной trace:

```text
Agent request
  → MCP tools/call
  → policy/validation
  → OilCaseX HTTP request
  → OilCaseX controller/domain service
  → response normalization
```

Метрики:

- MCP requests и duration;
- tool calls по tool/status;
- validation/policy failures;
- OilCaseX API latency/status;
- confirmation created/expired/used/replayed;
- idempotency hits/conflicts;
- response truncation;
- active requests и rate-limit rejections.

Запрещённые telemetry-поля:

- JWT;
- Authorization header;
- passwords/secrets;
- полный prompt;
- полный payload без redaction;
- внутренний stack trace в пользовательском ответе.

## 6. Контракты создания скважины

Текущий OilCaseX endpoint:

```http
POST /Api/V1/Purchased/Borehole
Authorization: Bearer <user-token>
Content-Type: application/json

{
  "wellpadId": 123,
  "orderId": 2
}
```

Текущий API сразу создаёт скважину. Для безопасного preview нужен новый endpoint в OilCaseX API:

```http
POST /Api/V1/Purchased/Borehole/Validate
```

Он должен выполнить те же проверки, что создание, но не делать запись, и вернуть:

```json
{
  "valid": true,
  "wellpadId": 123,
  "wellpadName": "Куст 7",
  "orderId": 2,
  "generatedBoreholeName": "...",
  "estimatedCost": null,
  "warnings": [],
  "resourceVersion": "optional-etag"
}
```

Важно: MCP не копирует проверки существования площадки и занятости `orderId`. Их выполняет OilCaseX API через тот же domain layer, который используется при фактическом создании.

## 7. MCP-tools для MVP

### 7.1. `list_wellpads`

Назначение: получить площадки текущей команды для разрешения имени в ID.

API mapping:

```http
GET /Api/V1/Purchased/WellPad
```

Input:

```json
{
  "nameContains": "optional client-side filter",
  "limit": 20
}
```

Output должен быть сокращён до необходимых полей: ID, name, доступные позиции, признак завершённого плана и минимальные статусы.

### 7.2. `get_wellpad`

API mapping:

```http
GET /Api/V1/Purchased/WellPad/{wellpadId}
```

Используется перед prepare для актуализации данных.

### 7.3. `prepare_create_borehole`

Input:

```json
{
  "wellpadId": 123,
  "orderId": 2
}
```

Поведение:

- проверить JSON Schema;
- вызвать API preflight;
- вернуть preview;
- выпустить confirmation token;
- не создавать скважину.

Output:

```json
{
  "status": "confirmation_required",
  "confirmationId": "...",
  "expiresAt": "...",
  "preview": {
    "action": "create_borehole",
    "wellpadId": 123,
    "wellpadName": "Куст 7",
    "orderId": 2,
    "generatedBoreholeName": "...",
    "warnings": []
  }
}
```

### 7.4. `execute_create_borehole`

Input:

```json
{
  "confirmationId": "..."
}
```

MCP берёт исходный payload из confirmation store. LLM не должна повторно передавать изменяемые `wellpadId` и `orderId` на execute.

API mapping:

```http
POST /Api/V1/Purchased/Borehole
Idempotency-Key: <stable-key>
```

Output:

```json
{
  "status": "created",
  "boreholeId": 456,
  "wellpadId": 123,
  "orderId": 2,
  "traceId": "..."
}
```

### 7.5. `get_borehole`

После создания MCP запрашивает созданный объект по API и подтверждает результат реальными данными, если подходящий read endpoint возвращает достаточный контракт.

## 8. Ошибки MCP

HTTP-ошибки нормализуются в стабильные коды:

| HTTP/API ситуация | MCP code | Поведение агента |
|---|---|---|
| 400/JSON validation | `invalid_arguments` | исправить аргументы |
| 401 | `authentication_required` | запросить повторный вход, не retry |
| 403 | `forbidden` | сообщить об отсутствии права |
| 404 | `resource_not_found` | уточнить ID/имя |
| 409, занята позиция | `domain_conflict` | показать конфликт, предложить другую позицию |
| 409, idempotency mismatch | `idempotency_conflict` | остановить действие |
| 422/preflight | `domain_validation_failed` | показать validation issues |
| 429 | `rate_limited` | controlled retry/read only |
| timeout/503 | `upstream_unavailable` | не утверждать успех |
| неизвестная ошибка | `internal_error` | trace ID без stack trace |

Любой неизвестный исход write-запроса считается `status_unknown`, а не failure с безопасным автоматическим повтором. Сначала выполняется idempotency/status reconciliation.

## 9. Предлагаемая структура проекта

```text
OilCaseX.McpServer/
  OilCaseX.McpServer.csproj
  Program.cs
  appsettings.json
  appsettings.Development.json
  Contracts/
    Common/
    Wellpads/
    Boreholes/
  Transport/
    McpEndpoint.cs
    McpSessionContext.cs
  Tools/
    ToolDescriptor.cs
    ToolRegistry.cs
    ToolDispatcher.cs
    Wellpads/
      ListWellpadsTool.cs
      GetWellpadTool.cs
    Boreholes/
      PrepareCreateBoreholeTool.cs
      ExecuteCreateBoreholeTool.cs
      GetBoreholeTool.cs
  Policies/
    ToolPolicy.cs
    ToolAuthorizationService.cs
    ToolRisk.cs
  Validation/
    JsonSchemaValidator.cs
    ContractValidator.cs
  Confirmation/
    ConfirmationRecord.cs
    IConfirmationStore.cs
    ConfirmationManager.cs
    PayloadCanonicalizer.cs
  Idempotency/
    IdempotencyKeyFactory.cs
  ApiClient/
    IOilCaseXApiClient.cs
    OilCaseXApiClient.cs
    OilCaseXApiClientOptions.cs
    Handlers/
      DelegatedAuthorizationHandler.cs
      TraceContextHandler.cs
  Security/
    CurrentUserContext.cs
    SecretRedactor.cs
  Errors/
    McpError.cs
    OilCaseXErrorMapper.cs
  Audit/
    AuditRecord.cs
    IAuditWriter.cs
  Observability/
    McpTelemetry.cs
    McpMetrics.cs
  Health/
    OilCaseXApiHealthCheck.cs

OilCaseX.McpServer.Tests/
  Unit/
  Integration/
  Contract/
  Architecture/
  Security/
```

Конкретные имена могут меняться, но направления зависимостей должны сохраниться.

## 10. Конфигурация

Пример групп настроек:

```text
Mcp
  ProtocolVersion
  RequestTimeout
  MaxRequestBytes
  MaxResponseBytes

OilCaseXApi
  BaseUrl
  OpenApiContractVersion
  ReadTimeout
  WriteTimeout

Confirmation
  LifetimeSeconds
  Store

Security
  AllowedAudiences
  AllowedRoles
  RedactionEnabled

RateLimits
  ReadsPerMinute
  PreparePerMinute
  ExecutePerMinute
```

Secrets не хранятся в `appsettings.json` и репозитории.

## План разработки

Подробный план разработки вынесен в отдельный документ: [OilCaseX.McpServer — план разработки](OilCaseX.McpServer-development-plan.md).

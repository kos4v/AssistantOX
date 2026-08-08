# OilCaseX.McpServer — план разработки

Архитектурные решения и границы сервиса описаны в [архитектурном документе](OilCaseX.McpServer-plan.md).


## Этап 0. Зафиксировать решения

Статус: **завершён 2026-08-07**.

Артефакты:

- [ADR-0001: REST API boundary](adr/ADR-0001-rest-api-boundary.md);
- [ADR-0002: MCP protocol, SDK и transport](adr/ADR-0002-mcp-transport-sdk.md);
- [ADR-0003: delegated auth, confirmation и audit](adr/ADR-0003-auth-confirmation-audit.md);
- [Trust boundaries](trust-boundaries.md);
- [OpenAPI baseline](openapi-baseline.md);
- [MVP tool catalog](tool-catalog-v0.md);
- [Environment configuration](environment-configuration.md).

### Задачи

- создать ADR: MCP работает только поверх REST API;
- выбрать и закрепить версию MCP protocol/.NET SDK;
- выбрать network transport;
- определить схему delegated authentication;
- выбрать confirmation/audit store для staging/production;
- согласовать MVP tool catalog;
- определить API base URL для environments.

### Definition of Done

- нет открытого вопроса о прямой ссылке на Domain Services;
- есть диаграмма trust boundaries;
- зафиксированы owner и version OpenAPI snapshot.

Проверка DoD:

- прямой доступ к Domain Services/DbContext/PostgreSQL запрещён ADR-0001;
- trust boundary описана в `trust-boundaries.md`;
- owner, URL, OpenAPI `3.0.4`, document version `v1` и SHA-256 зафиксированы в
  `openapi-baseline.md`;
- обнаружены и явно записаны текущие contract gaps: нет `operationId`, `servers`,
  security scheme и preflight endpoint.

## Этап 1. Подготовить OilCaseX OpenAPI

Статус: **реализован 2026-08-07 через локальный curated contract pipeline**.

Артефакты: `../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.raw.json`,
`../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.mcp.json`,
`../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.mcp.manifest.json`,
`../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.mcp.nswag`,
`../../src/OilCaseX.McpServer/generated/OilCaseXApiClient.g.cs`,
`../../src/OilCaseX.McpServer/scripts/update-openapi-snapshot.ps1`,
`../../src/OilCaseX.McpServer/scripts/validate-openapi.ps1`,
`../../src/OilCaseX.McpServer/scripts/check-openapi-compatibility.ps1` и
`.github/workflows/openapi-contract.yml`, а также compile-smoke проект
`../../src/OilCaseX.McpServer.ContractCompile`.

Внешний staging Swagger не изменяется из MCP repository. Поэтому operation IDs, Bearer
security, `servers`, summaries/descriptions и standard error responses добавляются в
versioned curated overlay. Raw snapshot остаётся неизменённым для drift-контроля.

### Задачи

- добавить стабильные `operationId` для используемых endpoints;
- добавить Bearer security scheme и `servers`;
- уточнить schemas и response codes;
- исправить отсутствующие `summary`/`description` для MVP операций;
- сохранить OpenAPI snapshot в MCP repository;
- настроить генерацию/проверку typed client;
- добавить compatibility check в CI.

### Definition of Done

- все MVP tools сопоставлены с одним `operationId`;
- typed client собирается из snapshot;
- breaking contract change обнаруживается автоматически.

Проверка DoD:

- curated snapshot содержит 6 operation IDs и mapping в manifest;
- NSwag 14.7.1 успешно генерирует
  `../../src/OilCaseX.McpServer/generated/OilCaseXApiClient.g.cs`;
- validation проверяет `operationId`, summary, description, responses, Bearer и `servers`;
- compatibility script и CI workflow обнаруживают удаление route/operation, смену
  operationId и удаление обязательного параметра;
- `createPurchasedBorehole` помечен `blocked-until-preflight`, поэтому он не входит в
  production MCP allow-list до изменения API-контракта.

## Этап 2. Создать каркас MCP Server

Статус: **завершён 2026-08-07**.

Реализовано:

- ASP.NET Core `net10.0` project и MCP SDK `1.4.1`;
- Streamable HTTP endpoint `/mcp` в stateless mode;
- `/health/live` и `/health/ready` с проверкой MCP и OilCaseX API;
- DI, `McpServerOptions`, data annotations и startup validation;
- JSON structured logging и ActivitySource для OpenTelemetry с opt-in OTLP exporter;
- Kestrel request limit и response-size guard;
- диагностический `mcp_server_ping` для проверки `tools/list`;
- Dockerfile для запуска в .NET 10 runtime image.
- self-hosted deployment workflow для runner с label `W10534` и health smoke-check.

Проверка DoD: локальный `dotnet build` проходит без предупреждений; MCP handshake и
`tools/list` возвращают `mcp_server_ping`; readiness показывает `mcp` и `oilcasex_api`.
Docker build подготовлен, но локальная проверка требует запущенного Docker Desktop daemon.

### Задачи

- создать ASP.NET Core project;
- подключить MCP SDK и transport;
- добавить health/readiness;
- реализовать DI и options validation;
- добавить structured logging и базовый OpenTelemetry;
- установить request/response size limits;
- реализовать tool registry с пустым/тестовым tool;
- добавить Dockerfile.

### Definition of Done

- MCP client подключается и получает `tools/list`;
- health показывает состояние MCP и OilCaseX API;
- container запускается с configuration validation;
- secrets не выводятся в лог.

## Этап 3. Реализовать API client и read tools

Статус: **завершён 2026-08-07**.

Реализовано:

- typed NSwag client через `HttpClientFactory` с фиксированным OilCaseX `BaseAddress`;
- delegated Bearer JWT и propagation `traceparent`/`X-Correlation-ID`;
- response-size guard, стабильный error mapper и отсутствие upstream response body в tool errors;
- GET-only retry/circuit breaker без повторения write-запросов;
- read-only tools `list_wellpads`, `get_wellpad`, `get_borehole`;
- curated output projections без `UserData`, cost и необязательных внутренних полей;
- unit, MCP integration и OpenAPI contract tests в CI.

Проверка DoD: `dotnet build` проходит без предупреждений; `dotnet test` — 12 тестов;
MCP `tools/list` публикует три read tools; запрос без delegated JWT возвращает стабильный
`unauthorized`, а не stack trace. Проверка с реальным staging JWT требует credentials
из защищённого окружения и выполняется deployment/integration pipeline.

### Задачи

- реализовать `HttpClientFactory` client;
- реализовать delegated JWT handler;
- реализовать propagation `traceparent`/correlation ID;
- добавить response size guard и error mapper;
- реализовать `list_wellpads`;
- реализовать `get_wellpad`;
- реализовать `get_borehole`;
- добавить output projection без лишних API-полей;
- добавить safe GET retry/circuit breaker;
- написать unit, integration и contract tests.

### Definition of Done

- tools работают со staging от имени пользователя;
- данные другой команды недоступны;
- MCP не может обратиться к route вне кода handler;
- API timeout возвращает `upstream_unavailable`;
- JWT отсутствует в logs/traces.

## Этап 4. Добавить API preflight создания скважины

Статус: **завершён локально 2026-08-07**.

Реализовано в `projects/OilCaseX`:

- `POST /Api/V1/Purchased/Borehole/Validate` с preview, машинными кодами ошибок и ETag-снимком;
- общая проверка wellpad, диапазона `orderId` и занятой позиции переиспользуется create-сценарием;
- preflight использует `AsNoTracking` и не вызывает `SaveChanges`;
- занятая позиция возвращает HTTP 409 с кодом `borehole_position_occupied`;
- Swagger получает endpoint из атрибутов контроллера;
- MCP получает endpoint в сгенерированном `OilCaseXApiClientGenerated`; он используется
  только внутри confirmation prepare pipeline;
- добавлен локальный OpenAPI stage-4 overlay до обновления staging Swagger.

Полный API integration test с боевой БД и staging JWT ещё требует CI/deployment окружения.

Этот этап меняет OilCaseX API, но не переносит domain logic в MCP.

### Задачи

- спроектировать `POST /Api/V1/Purchased/Borehole/Validate`;
- вынести общую domain validation create/validate внутри OilCaseX application/domain layer;
- гарантировать отсутствие записи при validate;
- вернуть preview и machine-readable validation issues;
- добавить resource version/ETag, если возможно;
- описать endpoint в Swagger;
- добавить API unit/integration tests;
- обновить OpenAPI snapshot и MCP client.

### Definition of Done

- validate и create используют одинаковые бизнес-проверки;
- validate не меняет продуктовые данные;
- занятый `orderId` возвращается как стабильный domain conflict;
- MCP не знает, как самостоятельно проверить занятость позиции.

## Этап 5. Реализовать prepare

Статус: **завершён локально 2026-08-07**.

Реализовано в MCP Server:

- `prepare_create_borehole` описан в `OilCaseXApiToolCatalog` как descriptor с
  `ConfirmationPreparation`, отдельного MCP wrapper-класса для него нет;
- tool вызывает только `ValidatePurchasedBoreholeAsync` и не вызывает create route;
- canonical payload `{orderId,wellpadId}` и SHA-256 `payloadHash`;
- in-memory confirmation store с TTL из `McpServer:ConfirmationTtlSeconds`;
- confirmation связан с fingerprint делегированного пользователя, ресурсом wellpad,
  tool name, payload hash и preview;
- общий `ConfirmationToolDecorator` создаёт confirmation и audit events
  `confirmation_prepare` для `prepared` и `validation_failed`;
- публичный MCP tool `validate_borehole_purchase` не публикуется: preflight является
  внутренней частью prepare.

Store пока in-memory и предназначен для этапа 5; durable storage и одноразовое execute
будут добавлены на этапе 6.

Все текущие tools публикуются descriptor-based generic wrapper-ом. Descriptors проходят
allow-list и non-destructive filters, после чего generic executor вызывает concrete
`OilCaseXApiClientGenerated`, применяет output projection или, при заданной
`ConfirmationPreparation`, передаёт результат в `ConfirmationToolDecorator`.

### Задачи

- создать контракты `PrepareCreateBoreholeRequest/Result`;
- реализовать JSON Schema;
- добавить descriptor `prepare_create_borehole` с confirmation policy;
- вызвать API preflight;
- канонизировать payload и вычислить hash;
- сформировать preview;
- создать confirmation record с TTL;
- добавить audit событий prepare/validation failure;
- протестировать создание confirmation, validation failure и binding confirmation к owner.

### Definition of Done

- prepare никогда не создаёт скважину;
- preview основан на актуальном API-ответе;
- confirmation связан с user/team/payload/tool;
- изменение payload требует нового prepare.

## Этап 6. Реализовать execute и idempotency

### Задачи MCP

- реализовать `execute_create_borehole`;
- принимать только `confirmationId`;
- проверить TTL, owner, team и одноразовость;
- повторить API preflight перед execute;
- создать стабильный idempotency key;
- вызвать create endpoint;
- выполнить result reconciliation через read endpoint;
- записать audit success/failure/unknown;
- исключить автоматический retry write.

### Задачи OilCaseX API

- поддержать `Idempotency-Key`;
- хранить key, payload hash и response атомарно;
- возвращать прежний результат для повторного одинакового запроса;
- возвращать conflict при другом payload;
- добавить retention/cleanup.

### Definition of Done

- повтор execute создаёт не более одной скважины;
- изменённый или просроченный confirmation отклоняется;
- потеря HTTP-ответа не приводит к слепому повтору;
- финальный MCP result основан на API response/reconciliation.

## Этап 7. Security hardening

### Задачи

- добавить role/tool policies;
- добавить rate и concurrency limits;
- запретить dynamic URLs и unknown JSON properties;
- добавить архитектурный тест project references;
- добавить redaction tests;
- проверить prompt injection через имена/API-поля;
- проверить replay confirmation;
- проверить confused deputy между пользователями/командами;
- запретить admin/reset/restore/delete tools;
- провести threat-model review.

### Definition of Done

- попытки вызова endpoint вне allow-list блокируются;
- без confirmation выполнено 0 write operations;
- cross-user/cross-team confirmation выполнено 0 раз;
- MCP binary не содержит product DB connection string;
- security regression tests обязательны в CI.

## Этап 8. Observability и audit

### Задачи

- завершить OpenTelemetry traces/metrics;
- настроить propagation в OilCaseX API;
- реализовать audit writer;
- добавить dashboards;
- добавить alerts для API failures, policy violations и replay;
- добавить trace ID в безопасные error responses;
- создать runbook.

### Definition of Done

- один trace связывает Agent → MCP → OilCaseX API;
- можно определить tool, duration и итог без чтения sensitive payload;
- есть alerts и инструкция диагностики;
- audit позволяет восстановить последовательность write operation.

## Этап 9. Production readiness

### Задачи

- выбрать production confirmation/audit store;
- настроить HA и shared state;
- добавить readiness dependencies;
- провести load tests;
- проверить cancellation и graceful shutdown;
- настроить TLS/service identity/network policies;
- выполнить image/dependency scan;
- добавить deployment manifests/compose profile;
- провести staged rollout и rollback rehearsal.

### Definition of Done

- несколько MCP instances не нарушают одноразовость confirmation;
- write не теряется и не дублируется при restart;
- deployment имеет rollback;
- SLO проверены нагрузочным тестом.

## Тестовая стратегия

### Unit

- tool registry/dispatcher;
- JSON Schema validation;
- policies;
- payload canonicalization/hash;
- confirmation lifecycle;
- error mapping;
- output projection/redaction;
- idempotency key generation.

### Integration

- MCP transport;
- MCP handler + mock OilCaseX API;
- delegated JWT;
- timeout/cancellation;
- confirmation store;
- audit writer;
- OpenTelemetry propagation.

### Contract

- OpenAPI snapshot;
- typed API client serialization;
- operationId mapping;
- request/response examples;
- API error shapes;
- MCP input/output schemas.

### Architecture

- отсутствуют references на Domain/Domain.Services;
- отсутствует EF Core/DbContext registration;
- descriptors и decorator зависят от `OilCaseXApiClientGenerated`, а не от `HttpClient`
  напрямую;
- tool catalog не создаётся динамически из Swagger;
- write handlers проходят confirmation pipeline.

### Security

- no confirmation;
- expired/replayed confirmation;
- changed payload;
- cross-user/cross-team;
- forged role;
- prompt injection в API data;
- oversized response;
- arbitrary URL/path attempt;
- JWT/log redaction;
- rate limiting.

### End-to-end

1. Получить площадки.
2. Prepare корректной скважины.
3. Подтвердить и создать.
4. Проверить созданную скважину.
5. Повторить execute и убедиться, что дубля нет.
6. Попробовать занятую позицию.
7. Попробовать чужой confirmation.
8. Имитировать потерю ответа API.

## CI/CD gates

Каждый pull request:

- restore/build;
- format/analyzers;
- unit tests;
- architecture tests;
- contract tests по snapshot;
- security regression subset;
- secret scan;
- dependency/container scan;
- проверка tool catalog allow-list.

Перед release:

- integration tests со staging-compatible API;
- полный security suite;
- load/smoke tests;
- OpenAPI compatibility report;
- deployment/rollback check.

## Порядок MVP

Минимальный вертикальный срез:

1. Каркас MCP и `tools/list`.
2. Typed API client с delegated JWT.
3. `list_wellpads`.
4. `get_wellpad`.
5. OilCaseX API preflight.
6. `prepare_create_borehole`.
7. Confirmation store.
8. API-level idempotency.
9. `execute_create_borehole`.
10. `get_borehole` и reconciliation.
11. Audit/OpenTelemetry.
12. Security/e2e tests.

Не начинать MVP с автогенерации всех Swagger-tools, RAG или admin-операций.

## Критерии готовности MCP Server

Сервис готов к pilot, когда:

- MCP работает только через OilCaseX REST API;
- нет references и runtime-доступа к Domain Services/продуктовой БД;
- MVP tool catalog мал, типизирован и версионируем;
- identity пользователя доходит до OilCaseX API вне LLM context;
- create проходит `prepare → preview → confirm → execute`;
- domain validation выполняется OilCaseX API;
- idempotency защищает запись end-to-end;
- неизвестный исход write не вызывает слепой retry;
- audit и trace связывают все этапы;
- JWT/secrets не попадают в telemetry;
- contract/security/architecture tests блокируют регрессии;
- контейнер воспроизводимо разворачивается и имеет health/readiness;
- documented runbook описывает сбои API, confirmation store и неизвестный результат write.

## Открытые решения перед кодированием

1. Какой network transport и версия .NET MCP SDK закрепляются?
2. Как Agent Service безопасно передаёт delegated JWT в MCP transport?
3. Где хранить confirmation/idempotency/audit state в production?
4. Можно ли добавить preflight и API-level idempotency в OilCaseX API до write MVP?
5. Какой endpoint использовать для чтения созданной скважины и reconciliation?
6. Какие поля Wellpad API можно безопасно показывать модели?
7. Какие роли получают доступ к prepare/execute?
8. Нужен ли ETag/resource version для защиты от изменений между prepare и execute?
9. Каковы TTL confirmation и retention idempotency/audit records?
10. Какие SLO требуются для read, prepare и execute tools?

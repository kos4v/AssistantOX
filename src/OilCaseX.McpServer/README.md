# OilCaseX.McpServer

Отдельный MCP gateway над существующим OilCaseX REST API.

Текущий статус: этап 6 реализован локально для сценария создания скважины. Этап 1 реализован как contract pipeline — raw и
curated OpenAPI snapshots, operation mapping, typed-client generation и compatibility
checks. Этапы 2–3 добавляют запускаемый ASP.NET Core MCP gateway со Streamable HTTP,
health/readiness, configuration validation, structured logging, OpenTelemetry,
типизированным OilCaseX API client и curated MCP tools. Read tools публикуются через
универсальный descriptor-based wrapper над `OilCaseXApiClientGenerated`; составные tools
`prepare_create_borehole` задаётся descriptor-ом с политикой confirmation: общий
`ConfirmationToolDecorator` выполняет внутренний preflight через
`ValidatePurchasedBoreholeAsync`, создаёт hash, confirmation и аудит. Отдельный MCP tool
для validation не публикуется.

## Принципиальная граница

```text
Agent Service
    │ MCP Streamable HTTP + delegated user context
    ▼
OilCaseX.McpServer
    │ HTTPS + Bearer JWT + traceparent
    ▼
OilCaseX REST API
    ▼
OilCaseX domain services / PostgreSQL
```

MCP Server не ссылается на Domain Services, `DbContext` или PostgreSQL OilCaseX и не
исполняет произвольные HTTP-запросы. Он предоставляет только curated tools и вызывает
фиксированные маршруты REST API.

## Документы этапа 0

- [ADR-0001: REST API boundary](../../docs/OilCaseX.McpServer/adr/ADR-0001-rest-api-boundary.md)
- [ADR-0002: MCP protocol, SDK и transport](../../docs/OilCaseX.McpServer/adr/ADR-0002-mcp-transport-sdk.md)
- [ADR-0003: delegated auth, confirmation и audit](../../docs/OilCaseX.McpServer/adr/ADR-0003-auth-confirmation-audit.md)
- [Trust boundaries](../../docs/OilCaseX.McpServer/trust-boundaries.md)
- [OpenAPI baseline](../../docs/OilCaseX.McpServer/openapi-baseline.md)
- [MVP tool catalog](../../docs/OilCaseX.McpServer/tool-catalog-v0.md)
- [Environment configuration](../../docs/OilCaseX.McpServer/environment-configuration.md)
- [OpenAPI contracts](contracts/openapi/README.md)
- [Generated client](generated/README.md)
- [План разработки](../../docs/OilCaseX.McpServer/OilCaseX.McpServer-development-plan.md)
- [Self-hosted deployment](../../docs/OilCaseX.McpServer/deployment-self-hosted.md)

## Локальный запуск

```powershell
dotnet run --project .\OilCaseX.McpServer.csproj --urls http://127.0.0.1:5089
```

Проверки:

```powershell
Invoke-RestMethod http://127.0.0.1:5089/health/live
Invoke-RestMethod http://127.0.0.1:5089/health/ready
```

MCP Streamable HTTP endpoint: `http://127.0.0.1:5089/mcp`.
Клиент после `initialize` получает `mcp_server_ping`, `list_wellpads`, `get_wellpad`,
`get_borehole`, `prepare_create_borehole` и `execute_create_borehole` через `tools/list`.
Read tools и prepare передают Bearer JWT из входящего MCP
запроса в OilCaseX API, если он присутствует.

API tools публикуются через descriptor-based wrapper над сгенерированным
`OilCaseXApiClientGenerated`. Каталог задаёт только выражения разрешённых методов, а
имена MCP, JSON Schema, безопасные флаги и projection результата формируются по
сигнатуре клиента. Подробности находятся в [GeneratedTools](Mcp/GeneratedTools/README.md).

Конфигурация задаётся через `appsettings.json` или переменные окружения с префиксом
`McpServer__`. Секреты в текущем scaffold не требуются и в логах не выводятся. Bearer JWT
делегируется из входящего MCP HTTP request в OilCaseX API и не попадает в tool arguments.
Write tools дополнительно требуют роль из `McpServer:WriteRoles`; роли должны быть
установлены доверенным authentication middleware или reverse proxy. MCP endpoint
ограничивает размер тела, rate и concurrency.
Для отправки трасс в OpenTelemetry Collector задайте `OpenTelemetry__OtlpEndpoint`;
по умолчанию экспорт отключён.

Сборка контейнера выполняется из корня `AI/AssistantOX`:

```powershell
docker build -f src/OilCaseX.McpServer/Dockerfile -t oilcasex-mcpserver .
```

Тесты:

```powershell
dotnet test .\src\OilCaseX.McpServer.Tests\OilCaseX.McpServer.Tests.csproj --configuration Release
```

## Ограничения текущей реализации

`execute_create_borehole` повторяет API preflight, атомарно потребляет in-memory
confirmation и передаёт стабильный `Idempotency-Key` в OilCaseX API. После успешной
записи выполняется reconciliation через `get_borehole`. Confirmation store пока
in-memory и очищается при перезапуске MCP Server; durable/shared storage относится к
этапу 9.

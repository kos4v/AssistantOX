# OilCaseX.McpServer

Отдельный MCP gateway над существующим OilCaseX REST API.

Текущий статус: этап 0 завершён, этап 1 реализован как contract pipeline — raw и curated
OpenAPI snapshots, operation mapping, typed-client generation и compatibility checks.
Runtime-код MCP Server будет добавляться начиная с этапа 2 плана.

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

## Следующий этап

Следующий блокер — внешний OilCaseX API: официальный Swagger пока не содержит стабильные
`operationId`, security scheme, `servers` и preflight/validate контракт. Локальный curated
overlay позволяет продолжить разработку MCP, но write tools остаются заблокированными до
появления API-level preflight и idempotency.

# Environment configuration baseline

Base URL не берётся из tool arguments и не приходит от LLM. Он задаётся доверенной
конфигурацией deployment.

| Environment | `OilCaseX:Api:BaseUrl` | Confirmation | Audit | Notes |
|---|---|---|---|---|
| local | `http://localhost:5088` mock API или явно заданный dev URL | in-memory | console/file | только development |
| staging | `https://x.stg.oilcase.ru` | PostgreSQL | PostgreSQL | shared integration и contract tests |
| production | secret-managed OilCaseX production URL | PostgreSQL | защищённая PostgreSQL schema/table | никакого staging fallback |

Минимальные настройки будущего сервера:

```text
OilCase__Api__BaseUrl
OilCase__Api__Audience
OilCase__Api__Issuer
Mcp__Transport__EndpointPath=/mcp
Mcp__Transport__ProtocolVersion=2025-06-18
Confirmation__Store=InMemory|Postgres
Confirmation__Lifetime
Audit__ConnectionString
Telemetry__OtlpEndpoint
```

Secrets не хранятся в `appsettings.json`, ADR, Swagger snapshot или репозитории.

## Validation rules

- Base URL обязателен и должен быть absolute `https` для staging/production.
- Host allowlist должен совпадать с environment configuration.
- Production startup завершается ошибкой при staging URL, in-memory confirmation store или
  отсутствующем issuer/audience.
- Local defaults не используются в staging/production.

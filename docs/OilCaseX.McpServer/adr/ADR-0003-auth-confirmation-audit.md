# ADR-0003: delegated authentication, confirmation и audit storage

- Статус: Accepted
- Дата: 2026-08-07
- Владелец решения: OilCaseX AI Agent team

## Delegated authentication

Agent Service передаёт пользовательский access token во внешний HTTP-заголовок MCP:

```http
Authorization: Bearer <user-access-token>
traceparent: <w3c-trace-context>
X-Correlation-Id: <correlation-id>
```

Токен не входит в tool arguments, prompt, MCP structured content или audit payload.

MCP Server:

1. проверяет подпись, issuer, audience, expiry и обязательные scopes;
2. извлекает минимальный контекст: `sub`, `teamId`, roles/scopes;
3. связывает этот контекст с policy и confirmation;
4. передаёт исходный Bearer token в OilCaseX REST API через delegating handler;
5. не пишет JWT в logs/traces.

OilCaseX API остаётся финальной точкой проверки полномочий. MCP не заменяет API
authorization своим service account.

## Confirmation

Для write tool используется протокол:

```text
prepare → current API validation → preview → confirmationId → execute
```

`confirmationId` связан с:

- hash нормализованных arguments;
- tool name;
- `sub` и `teamId`;
- creation/expiry time;
- одноразовым статусом;
- idempotency key.

Execute отклоняется, если token истёк, уже использован, принадлежит другому пользователю/
команде или payload отличается от preview. Delete, reset, restore, password и admin tools
не входят в первый production catalog.

## Хранилища

| Environment | Confirmation store | Audit store | Решение |
|---|---|---|---|
| local | In-memory | Structured local log | Быстрый автономный запуск; не production-семантика |
| staging | PostgreSQL | PostgreSQL append-only table | Проверка expiry, одноразовости, race conditions и отчётности |
| production | PostgreSQL с unique constraints/transactional consume | Отдельная защищённая append-only schema/table | Надёжность, retention, расследование и compliance |

Production confirmation record должен быть consumed атомарно, например транзакцией с row
lock или compare-and-set. In-memory store не допускается в production.

## Audit minimum

Audit record содержит только необходимые метаданные:

- timestamp, trace/correlation ID;
- `sub`, `teamId`, role/scope summary;
- tool name, operation reference и payload hash;
- prepare/execute/result status;
- OilCaseX response ID/status;
- error code без stack trace и secrets.

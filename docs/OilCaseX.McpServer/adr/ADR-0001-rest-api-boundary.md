# ADR-0001: MCP работает только поверх OilCaseX REST API

- Статус: Accepted
- Дата: 2026-08-07
- Владелец решения: OilCaseX AI Agent team
- Владелец API-контракта: OilCaseX API team

## Контекст

MCP должен дать LLM ограниченный набор предметных tools, но бизнес-логика, транзакции и
авторизация уже принадлежат OilCaseX. Прямое подключение MCP к Domain Services или
PostgreSQL создало бы второй путь к данным, дублирование правил и риск обхода API policy.

Текущий staging-контракт опубликован по адресу:
`https://x.stg.oilcase.ru/swagger/v1/swagger.json`.

## Решение

`OilCaseX.McpServer` является anti-corruption layer над HTTP API:

```text
MCP tool → curated registry → policy/validation → typed HttpClient → OilCaseX REST API
```

MCP-проект:

- не ссылается на `OilCaseX.Domain` и `OilCaseX.Domain.Services`;
- не ссылается на `OilCaseXContext`;
- не подключается к PostgreSQL, RabbitMQ или MinIO OilCaseX;
- не принимает произвольный URL, route, SQL или HTTP method из tool arguments;
- строит URI только внутри доверенного handler по заранее заданному mapping;
- возвращает агенту нормализованный контракт, а не внутренние API/DB-модели.

OilCaseX REST API остаётся авторитетным источником для принадлежности данных команде,
бизнес-валидации, транзакций и факта успешного изменения.

## Следствия

Положительные:

- единая бизнес-логика и авторизация;
- независимое версионирование MCP tool contract;
- возможность заменить REST API adapter без изменения Agent Service;
- архитектурный запрет прямого доступа можно проверять в CI.

Ограничения:

- для preview понадобится поддержка validate/preflight в REST API;
- изменения API требуют contract tests и обновления mapping;
- MCP не сможет исправлять некорректные доменные правила локально.

## Отклонённые варианты

1. Прямой вызов Domain Services — отклонён: нарушает границу API и создаёт второй application boundary.
2. Прямой SQL из MCP — отклонён: обход авторизации, транзакций и владельца данных.
3. Автоматическая публикация всего Swagger как tools — отклонена: слишком широкий и небезопасный
   контракт для LLM.

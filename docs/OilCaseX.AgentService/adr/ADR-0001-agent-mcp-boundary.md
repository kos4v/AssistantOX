# ADR-0001: Agent Service работает только через MCP

## Статус

Принято на этапе 0.

## Решение

`OilCaseX.AgentService` не использует OilCaseX REST API напрямую и не ссылается на
`OilCaseX.Domain`, `OilCaseX.Domain.Services` или `OilCaseXContext`. Все продуктовые
read/write операции выполняются только через завершённый `OilCaseX.McpServer`.

Agent Service отвечает за оркестрацию LLM, conversation state, clarification,
локальную tool policy и композицию ответа. MCP Server отвечает за API mapping,
авторизацию, JSON Schema, доменную проверку, confirmation, idempotency и audit.

## Причины

- единая точка authorization и domain validation;
- отсутствие дублирования OilCaseX бизнес-логики;
- независимое обновление агента и API;
- отсутствие product DB credentials в AI-контуре;
- контролируемая поверхность инструментов вместо публикации Swagger.

## Запреты

LLM и Agent Service не могут передавать MCP произвольные URL, HTTP method, SQL,
dynamic tool name или connection string.

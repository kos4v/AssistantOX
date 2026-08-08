# Поэтапный план разработки OilCaseX.AgentService

Архитектура, границы ответственности, agent loop, contracts и правила безопасности
описаны в [основном плане](OilCaseX.AgentService-plan.md).

## Исходные условия

К началу разработки `OilCaseX.McpServer` завершён и предоставляет стабильный MCP
Streamable HTTP endpoint, curated `tools/list`, read tools,
`prepare_create_borehole`, `execute_create_borehole`, делегированную авторизацию,
confirmation, idempotency и нормализованные ошибки.

Agent Service работает только через MCP. В нём не должно быть OilCaseX API client,
ссылок на OilCaseX Domain/Services, product DB connection string или прямых HTTP-вызовов
OilCaseX.

## UI-вертикаль MVP

Добавлен отдельный `OilCaseX.Agent.Ui` на Blazor Web App с интерактивным серверным
рендерингом. UI подключается к защищённому `AgentChatHub` по SignalR и вызывает тот же
`AgentOrchestrator`, поэтому preview/confirmation и allow-list не дублируются на клиенте.
`ChatClientAgent` используется только в безопасном fallback без MCP tools, когда Hub или
AgentService недоступны.

## Этап 0. Зафиксировать границы и контракты

**Статус: завершён.**

### Задачи

- создать ADR по Agent Service, vLLM и MCP boundaries;
- сохранить MCP tools contract fixture;
- определить MVP intents и ожидаемые последовательности tools;
- зафиксировать mapping `MCP error → agent behavior`;
- определить threat model;
- определить Chat API и streaming events;
- собрать первые 40–60 eval-примеров;
- согласовать SLO для chat, LLM и MCP.

### Результат

- ADR и trust boundaries;
- MCP contract baseline;
- список MVP-сценариев;
- начальный eval dataset;
- error-policy table.

### Definition of Done

- Agent Service не имеет зависимости от OilCaseX API/Domain;
- каждый MVP intent сопоставлен с разрешёнными MCP tools;
- определены input/output и streaming contracts;
- зафиксированы prompt, model и MCP contract versions.

## Этап 1. Создать solution и ASP.NET Core host

**Статус: завершён.** Host scaffold расширен MVP runtime для chat, vLLM и MCP;
production hardening выполняется на этапах 8–10.

### Задачи

- создать проекты `Api`, `Application`, `Domain`, `Infrastructure`;
- создать unit, integration, contract и E2E test projects;
- добавить configuration classes с validation-on-start;
- подключить authentication и authorization;
- реализовать `/health/live` и `/health/ready`;
- добавить request/message size limits;
- настроить structured logging и базовый redaction;
- добавить Dockerfile и локальный compose profile;
- добавить CI на self-hosted runner с label `W10534`.

### Результат

- запускаемый ASP.NET Core service;
- solution с зафиксированными project boundaries;
- health endpoints и контейнерная сборка;
- базовый CI pipeline.

### Definition of Done

- solution собирается без предупреждений;
- host запускается локально и в контейнере;
- configuration errors обнаруживаются до начала приёма запросов;
- секреты отсутствуют в repository, logs и build artifacts.

## Этап 2. Подключить vLLM/Gemma 4

**Статус: MVP завершён.** Реализован OpenAI-compatible клиент на базе `OpenAIClient`/`IChatClient`;
streaming, readiness probe и fake vLLM остаются production hardening.

### Задачи

- определить `IAgentModelClient`;
- реализовать OpenAI-compatible vLLM client;
- поддержать text response, streaming и structured tool calls;
- закрепить model ID/revision, tokenizer и generation settings;
- реализовать timeout, cancellation и bounded retry;
- добавить model readiness check;
- создать fake vLLM server для integration tests;
- проверить tool-call template на benchmark-наборе;
- нормализовать malformed model responses.

### Результат

- типизированная граница Agent Service → vLLM;
- воспроизводимая конфигурация Gemma 4;
- fake model для детерминированных тестов.

### Definition of Done

- сервис получает streaming text и structured tool call;
- malformed response не приводит к MCP-вызову;
- недоступность модели не приводит к write operation;
- model revision и latency присутствуют в trace.

## Этап 3. Подключить завершённый MCP Server

**Статус: MVP завершён.** Реализован SDK-клиент `McpClient` через Streamable HTTP,
передача Authorization и локальная фильтрация tools. Проверка каталога на startup и
contract drift остаются отдельными hardening-задачами.

### Задачи

- определить `IAgentToolClient`;
- реализовать MCP Streamable HTTP client;
- выполнять handshake и `tools/list`;
- проверять обязательные tools и schemas при startup/readiness;
- сформировать immutable локальный tool catalog;
- реализовать локальную allow-list/deny-list policy;
- передавать JWT и trace context вне LLM arguments;
- добавить MCP timeout и response-size limit;
- создать fake MCP Server;
- добавить contract drift test.

### Результат

- типизированная граница Agent Service → MCP;
- проверяемый локальный каталог tools;
- fake MCP для agent-loop tests.

### Definition of Done

- readiness unhealthy при несовместимом MCP contract;
- модель видит только tools, прошедшие policy;
- неизвестный tool невозможно отправить в MCP;
- JWT отсутствует в prompt, conversation history и tool arguments.

## Этап 4. Реализовать read-only agent loop

**Статус: MVP завершён.** Реализован bounded loop с allow-list, ограничениями шагов и
MCP-вызовов, сохранением observations и graceful degradation при недоступности LLM/MCP.
SSE/NDJSON и detection повторяющихся вызовов запланированы после MVP.

### Задачи

- реализовать `AgentOrchestrator` и bounded `AgentLoop`;
- ограничить шаги, MCP calls и общий deadline;
- передавать модели разрешённые tool schemas;
- валидировать tool name и arguments;
- выполнять read tools через MCP;
- сохранять steps и `ToolObservation`;
- добавить detection повторяющихся tool calls;
- реализовать grounded response composition;
- реализовать SSE/NDJSON agent events;
- добавить fallback при timeout и недоступности зависимостей.

### Ограничения MVP

- не более 6 agent steps на пользовательский ход;
- не более 4 MCP calls на ход;
- не более одного одинакового tool call с теми же arguments;
- cancellation при отключении клиента;
- финальный ответ формируется после сохранения observation.

### Definition of Done

- list/get сценарии проходят end-to-end;
- агент не вызывает tools вне policy;
- ответ опирается на фактический MCP observation;
- после MCP error агент не утверждает, что операция успешна.

## Этап 5. Добавить conversation state и clarification

**Статус: MVP завершён на in-memory storage.** Состояние привязано к пользователю и
команде, контекст ограничен последними сообщениями. Distributed lock, TTL/cleanup,
summary и специализированная clarification-модель требуют production storage.

### Задачи

- реализовать conversation/message persistence;
- хранить messages и structured state отдельно;
- привязать conversation к `userId` и `teamId`;
- добавить optimistic concurrency или distributed lock;
- реализовать `PendingClarification`;
- добавить entity resolution по имени/ID;
- поддержать варианты выбора при неоднозначности;
- реализовать sliding context window и summary;
- обработать multi-intent и смену темы;
- добавить TTL и cleanup state.

### Definition of Done

- уточнение продолжается в следующем HTTP request;
- параллельные сообщения не повреждают state;
- сущность разрешается детерминированно либо пользователь получает уточнение;
- context window остаётся в заданном token budget;
- другой пользователь или команда не могут открыть conversation.

## Этап 6. Реализовать создание draft, prepare и preview

**Статус: MVP завершён.** Agent Service разрешает только read tools и
`prepare_create_borehole`, сохраняет pending confirmation и возвращает фактический preview.

### Задачи

- определить `CreateBoreholeDraft`;
- извлекать `wellpadId/orderId` из естественного языка;
- использовать read tools для разрешения площадки;
- уточнять отсутствующие и неоднозначные параметры;
- вызвать `prepare_create_borehole`;
- сохранить `PendingConfirmation`;
- показать preview, warnings и expiry;
- остановить agent loop после preview;
- аннулировать pending confirmation при изменении параметров.

### Definition of Done

- prepare никогда не вызывает execute;
- preview отображает фактический MCP response;
- pending confirmation связан с conversation/user/team;
- изменение параметров требует нового prepare;
- без явного подтверждения выполнено 0 write operations.

## Этап 7. Реализовать explicit confirmation и execute

**Статус: MVP завершён.** Добавлены confirm/reject endpoints; execute вызывается только
из отдельного confirm-запроса с сохранённым `confirmationId`, вне свободного LLM loop.

### Задачи

- добавить confirm/reject endpoints;
- проверить conversation owner, team, pending state и expiry;
- передать MCP только `confirmationId`;
- вызвать `execute_create_borehole` вне свободного LLM loop;
- сохранить MCP observation до success-ответа пользователю;
- обработать expiry, reject, replay, conflict и unknown result;
- запретить автоматический retry execute;
- добавить reconciliation UX.

### Definition of Done

- LLM не может вызвать execute в обычном ходе;
- один confirmation создаёт не более одной скважины;
- повторное подтверждение не создаёт дубль;
- success показывается только после успешного MCP result;
- неизвестный результат явно помечается и не превращается в ложный success.

## Этап 8. Security hardening

### Задачи

- реализовать role/tool policy;
- добавить rate, token и concurrency limits;
- завершить input/output/log redaction;
- добавить prompt-injection regression tests;
- протестировать cross-user и cross-team access;
- протестировать неизвестные tools, arguments и dynamic URLs;
- добавить архитектурный test запрета OilCaseX API/Domain references;
- проверить отсутствие secrets в binary и container image;
- запретить admin/delete/reset/restore operations;
- провести threat-model review.

### Definition of Done

- unsafe write без confirmation = 0;
- cross-user/cross-team access = 0;
- вызов tool вне allow-list = 0;
- JWT, secrets и system prompt отсутствуют в logs/stream;
- security tests являются обязательным CI gate.

## Этап 9. Observability и эксплуатация

### Задачи

- добавить OpenTelemetry traces Agent → vLLM/MCP;
- добавить metrics по turns, steps, tool calls, latency и errors;
- связать conversation, turn и trace IDs;
- добавить audit confirmation и policy violations;
- подготовить dashboards и alerts;
- реализовать dependency-specific health/readiness;
- описать graceful degradation;
- создать runbook диагностики.

### Definition of Done

- один trace показывает полный agent turn;
- доступны p50/p95 LLM и MCP latency;
- причина ошибки определяется без sensitive payload;
- недоступность dependency отражается в readiness и alerts;
- audit восстанавливает последовательность write-сценария.

## Этап 10. Evals, CI/CD и production readiness

### Задачи

- создать datasets для routing, arguments и clarification;
- добавить create-borehole и confirmation scenarios;
- добавить adversarial и production regression cases;
- измерять task success, tool accuracy и unsafe execution count;
- добавить E2E со staging MCP;
- добавить format, analyzers, tests и contract checks в CI;
- добавить dependency/container scan;
- настроить deploy на runner `W10534`;
- провести load test и rollback rehearsal;
- закрепить quality gates.

### Начальные quality gates

- tool selection accuracy ≥ 95%;
- корректность обязательных arguments ≥ 95%;
- end-to-end success основного набора ≥ 85%;
- unsafe write без confirmation = 0;
- hallucinated success = 0;
- security regression failures = 0;
- p95 укладывается в согласованный SLO.

### Definition of Done

- deployment воспроизводим из CI;
- staging smoke/E2E проходят перед deploy;
- safety regression блокирует merge/deploy;
- rollback проверен;
- основной сценарий продемонстрирован на OilCaseX staging.

## Критический путь MVP

```text
Stage 0: contracts
  → Stage 1: host
  → Stage 2: vLLM client
  → Stage 3: MCP client
  → Stage 4: read-only AgentLoop
  → Stage 5: conversation/clarification
  → Stage 6: prepare/preview
  → Stage 7: confirm/execute
  → Stage 8: security
  → Stage 9: telemetry
  → Stage 10: evals/deploy
```

RAG, multi-agent orchestration, long-term semantic memory и дополнительные write tools
не блокируют первый работающий сценарий создания скважины и выполняются отдельными
вертикальными блоками после стабилизации MVP.

## Итоговый Definition of Done

- пользователь решает read-only сценарии через чат;
- скважина создаётся только через `prepare → preview → confirm → execute`;
- Agent Service работает с OilCaseX только через MCP;
- tool calls ограничены MCP catalog и локальной policy;
- JWT и secrets не попадают в LLM context;
- conversation/confirmation state защищены от cross-user доступа;
- ошибки не превращаются в ложное сообщение об успехе;
- Agent, vLLM и MCP связаны trace context;
- unit, integration, contract, security и E2E tests проходят в CI;
- deployment воспроизводим на runner `W10534`.

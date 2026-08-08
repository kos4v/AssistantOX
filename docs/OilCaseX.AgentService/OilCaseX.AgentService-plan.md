# Архитектура OilCaseX.AgentService

## 1. Цель

Создать ASP.NET Core Agent Service, который принимает сообщения пользователя из чата,
общается с локальной Gemma 4 через OpenAI-совместимый API vLLM, выбирает доступные
OilCaseX MCP tools, уточняет недостающие параметры и безопасно проводит пользователя
через сценарий создания скважины `prepare → preview → explicit confirmation → execute`.

К началу разработки `OilCaseX.McpServer` считается завершённым и предоставляет
стабильный MCP Streamable HTTP endpoint, curated `tools/list`, типизированные ответы,
авторизацию, confirmation, idempotency, audit и нормализованные ошибки.

## 2. Результат MVP

Пользователь должен уметь через чат:

1. Получить список кустовых площадок.
2. Найти площадку или скважину по имени либо ID.
3. Получить фактические сведения о площадке и скважине.
4. Попросить создать скважину естественным языком.
5. Получить уточняющий вопрос, если площадка или позиция неоднозначны.
6. Получить preview операции без изменения данных.
7. Явно подтвердить preview.
8. Получить подтверждённый результат создания, основанный на ответе MCP/OilCaseX.

Агент не должен утверждать, что действие выполнено, пока MCP не вернул успешный
результат `execute_create_borehole`.

## 3. Исходные предпосылки

Перед началом разработки Agent Service должны быть доступны:

- MCP endpoint и его health/readiness endpoints;
- рабочий MCP handshake и `tools/list`;
- read tools, необходимые для разрешения названий и ID;
- `prepare_create_borehole`;
- `execute_create_borehole`;
- стабильные MCP error codes;
- делегирование пользовательского JWT;
- тестовый пользователь OilCaseX staging;
- закреплённая версия Gemma 4 и OpenAI-compatible endpoint vLLM;
- измеренные ограничения модели: context length, tool-call формат, concurrency и timeout.

Agent Service не генерирует MCP tools из Swagger и не содержит HTTP-клиент OilCaseX.
Единственная продуктовая интеграция агента — MCP.

## 4. Границы ответственности

### Agent Service отвечает за

- HTTP API чата и потоковую выдачу событий;
- conversation state и историю сообщений;
- вызов vLLM;
- агентный цикл и ограничение числа шагов;
- выбор доступных модели tools через локальную policy;
- проверку формы tool call до отправки в MCP;
- уточнение отсутствующих и неоднозначных параметров;
- управление pending confirmation на уровне диалога;
- сборку итогового ответа только из подтверждённых observations;
- ограничение контекста, summary и redaction;
- traces, metrics и agent audit;
- graceful degradation при недоступности LLM или MCP.

### Agent Service не отвечает за

- бизнес-валидацию OilCaseX;
- проверку занятости позиции скважины;
- авторизацию доступа к объектам OilCaseX;
- создание confirmation token;
- идемпотентность продуктовой записи;
- прямой доступ к OilCaseX REST API или продуктовой БД;
- выполнение SQL, shell, произвольных URL или динамических HTTP-запросов;
- доверие решению LLM о безопасности операции.

Эти проверки остаются в MCP Server и OilCaseX API.

## 5. Целевая архитектура

```text
OilCaseX Chat UI
       |
       | HTTPS/SSE + user JWT
       v
OilCaseX.Agent.Api
  - chat endpoints
  - authentication
  - streaming
  - request limits
       |
       v
OilCaseX.Agent.Application
  - AgentOrchestrator
  - AgentLoop
  - ToolPolicy
  - Clarification
  - ConfirmationCoordinator
  - ContextWindow
  - ResponseComposer
       |
       +---------------------------+
       |                           |
       v                           v
OilCaseX.Agent.Infrastructure   OilCaseX.Agent.Persistence
  - vLLM client                  - conversations
  - MCP client                   - messages
  - tool catalog cache           - structured state
  - OpenTelemetry                - pending confirmations
       |                           - prompt/model versions
       |
       +---------------------------+
       |
       +-------------> vLLM / Gemma 4
       |
       +-------------> OilCaseX.McpServer
                              |
                              v
                       OilCaseX REST API
```

Зависимости направляются внутрь: `Api` и `Infrastructure` зависят от
`Application`, а `Application` не зависит от MCP SDK, vLLM SDK, EF Core или ASP.NET.

## 6. Предлагаемая структура solution

```text
AI/AssistantOX/
  src/
    OilCaseX.Agent.Api/
      Controllers/
      Contracts/
      Streaming/
      Middleware/
      Extensions/
      Program.cs
    OilCaseX.Agent.Application/
      Orchestration/
      State/
      Tools/
      Policies/
      Clarification/
      Confirmation/
      Context/
      Responses/
      Abstractions/
    OilCaseX.Agent.Domain/
      Conversations/
      Messages/
      Confirmations/
      ValueObjects/
    OilCaseX.Agent.Infrastructure/
      Llm/
      Mcp/
      Persistence/
      Observability/
      Configuration/
  tests/
    OilCaseX.Agent.UnitTests/
    OilCaseX.Agent.IntegrationTests/
    OilCaseX.Agent.ContractTests/
    OilCaseX.Agent.E2ETests/
  evals/
    datasets/
    runners/
    reports/
  deploy/
    docker/
    compose/
```

Для MVP допустимо объединить `Domain` и `Application`, если там остаются только чистые
модели и правила. Границы LLM, MCP и persistence должны оставаться отдельными.

## 7. Основные компоненты

### 7.1. Chat API

Минимальные endpoints:

```http
POST /api/v1/conversations
GET  /api/v1/conversations/{conversationId}
POST /api/v1/conversations/{conversationId}/messages
POST /api/v1/conversations/{conversationId}/confirmations/{confirmationId}/confirm
POST /api/v1/conversations/{conversationId}/confirmations/{confirmationId}/reject
GET  /health/live
GET  /health/ready
```

Отправка сообщения возвращает SSE или NDJSON stream с событиями:

- `message.started`;
- `assistant.delta`;
- `tool.started`;
- `tool.completed`;
- `clarification.required`;
- `confirmation.required`;
- `operation.completed`;
- `message.completed`;
- `error`.

В пользовательский stream нельзя отдавать chain-of-thought, system prompt, JWT,
неотфильтрованные tool arguments или внутренние исключения.

### 7.2. LLM client

Создать абстракцию `IAgentModelClient` и реализацию OpenAI-compatible vLLM client.

Клиент должен поддерживать:

- messages и tool definitions;
- tool calls со структурированными arguments;
- streaming текста;
- cancellation;
- timeout;
- ограниченный retry только до получения ответа модели;
- model ID/revision в telemetry;
- нормализацию malformed tool call;
- отсутствие API key в логах.

Prompt, generation settings, model revision и tool-call template должны быть
версионируемыми конфигурационными артефактами.

### 7.3. MCP client

Создать абстракцию `IAgentToolClient` поверх официального .NET MCP SDK.

На старте сервис:

1. Подключается к фиксированному MCP endpoint.
2. Выполняет handshake.
3. Получает `tools/list`.
4. Проверяет обязательные tools и ожидаемые schemas.
5. Формирует локальный immutable catalog.
6. Помечает readiness unhealthy при несовместимом контракте.

На каждом `tools/call` Agent Service передаёт пользовательский JWT и trace context вне
LLM messages и tool arguments.

Agent Service не принимает от модели MCP URL, transport, имя сервера или произвольное
имя инструмента. Модель может выбрать только tool из каталога, прошедшего локальную
policy.

### 7.4. Tool policy

Перед каждым обращением к модели формируется разрешённый поднабор tools с учётом:

- типа текущего хода;
- роли пользователя;
- состояния диалога;
- наличия pending clarification;
- наличия pending confirmation;
- типа инструмента: `Read`, `PrepareWrite`, `ExecuteWrite`;
- локального deny-list для admin/delete/reset/restore;
- лимита tool calls на один ход.

`execute_create_borehole` не передаётся модели в обычном ходе. Он доступен только после
отдельного явного подтверждения пользователем и вызывается через
`ConfirmationCoordinator`, а не по свободному решению LLM.

### 7.5. Agent loop

```text
user message
  → authenticate and load conversation
  → input guardrails
  → build structured state
  → select allowed tools
  → build bounded model context
  → call Gemma 4
      ├─ final answer
      ├─ clarification
      └─ typed tool call
           → validate known tool and JSON schema
           → call MCP with delegated JWT
           → normalize observation
           → update structured state
           → continue loop
  → compose grounded response
  → persist result and trace references
```

Обязательные ограничения:

- максимум 6 agent steps на один пользовательский ход;
- максимум 4 MCP calls на ход;
- максимум 1 prepare write на ход;
- `execute` выполняется только в confirmation flow;
- общий deadline запроса;
- защита от повторяющихся одинаковых tool calls;
- cancellation при отключении клиента;
- финальный ответ строится только после сохранения observation.

### 7.6. Structured state

История сообщений и состояние агента хранятся отдельно.

```text
ConversationState
  conversationId
  userId
  teamId
  status
  currentIntent
  resolvedEntities
  missingFields
  lastToolObservations
  pendingClarification
  pendingConfirmation
  summary
  promptVersion
  modelRevision
  version
```

LLM не является источником истины для `userId`, `teamId`, confirmation status и
результатов write operation.

### 7.7. Clarification

Уточнение должно быть отдельным состоянием, а не только текстом ассистента.

Примеры причин:

- не указана площадка;
- имя соответствует нескольким площадкам;
- не указан `orderId`;
- пользователь назвал недоступную позицию;
- в одном сообщении обнаружены конфликтующие намерения;
- пользователь изменил параметры после preview.

Pending clarification хранит ожидаемое поле, допустимый тип ответа, варианты выбора и
TTL. Следующее сообщение сначала обрабатывается как ответ на уточнение и только затем
как новый intent.

### 7.8. Confirmation flow

```text
«Создай скважину на площадке 12, позиция 3»
  → resolve parameters
  → MCP prepare_create_borehole
  → persist confirmationId + preview + expiry
  → show preview and Confirm/Reject controls
  → stop agent loop

explicit Confirm
  → verify conversation/user/team/pending state
  → MCP execute_create_borehole(confirmationId)
  → persist factual result
  → compose completion message
```

После preview запрещено автоматически продолжать к execute в том же ходе. Текст
«подтверждаю» можно поддержать дополнительно, но основной безопасный путь — отдельный
confirm endpoint/UI action.

Изменение параметров аннулирует локальный pending confirmation и требует нового
`prepare_create_borehole`.

### 7.9. Context management

В контекст модели включаются:

- system policy фиксированной версии;
- краткое summary предыдущего диалога;
- ограниченное окно последних сообщений;
- структурированные resolved entities;
- только актуальные tool schemas;
- компактные MCP observations без чувствительных полей.

В контекст не включаются JWT, connection strings, raw logs, stack traces, hidden policy
state и полные большие ответы API.

## 8. Контракты приложения

Минимальные application contracts:

```csharp
public interface IAgentModelClient;
public interface IAgentToolClient;
public interface IToolCatalogProvider;
public interface IToolCallValidator;
public interface IConversationRepository;
public interface IConversationLock;
public interface IAgentEventWriter;
public interface IClock;
```

Основные DTO/state models:

- `ChatTurnRequest`;
- `AgentMessage`;
- `AgentStep`;
- `AgentToolDefinition`;
- `AgentToolCall`;
- `ToolObservation`;
- `ClarificationRequest`;
- `PendingConfirmation`;
- `CreateBoreholeDraft`;
- `CreateBoreholePreview`;
- `CreateBoreholeResult`;
- `AgentTurnResult`;
- `AgentError`.

Tool observations должны хранить стабильный MCP error code, trace ID, безопасные данные
результата и признак фактического выполнения операции.

## 9. Обработка ошибок

| Ситуация | Поведение Agent Service |
|---|---|
| vLLM timeout | завершить ход контролируемой ошибкой, не вызывать write автоматически |
| malformed tool call | один constrained repair либо уточнение; затем остановка |
| неизвестный tool | policy violation, не отправлять в MCP |
| MCP 401 | запросить повторную авторизацию |
| MCP 403 | сообщить об отсутствии доступа без раскрытия ресурса |
| MCP resource not found | уточнить имя/ID |
| MCP domain conflict | показать конфликт и предложить изменить параметры |
| MCP validation failed | показать issues и не создавать confirmation локально |
| confirmation expired | предложить повторный prepare |
| confirmation replayed | запросить фактическое состояние, не повторять write вслепую |
| MCP timeout после execute | результат считать неизвестным; не утверждать success |
| клиент отключился | отменить LLM/read calls; исход write сверять по MCP semantics |

## 10. Безопасность

1. JWT берётся только из аутентифицированного HTTP context.
2. JWT не передаётся модели и не сохраняется в conversation history.
3. System policy не может быть изменена содержимым пользователя или MCP observation.
4. Ответы MCP считаются недоверенными данными, а не инструкциями.
5. Tool names и MCP endpoint формируются только сервером.
6. Аргументы проверяются по MCP JSON Schema до вызова.
7. Write tools недоступны без server-side confirmation state.
8. Conversation state привязан к `userId` и `teamId`.
9. Для одного conversation применяется optimistic concurrency или distributed lock.
10. В логах работает redaction токенов, prompts и потенциально чувствительных полей.
11. Ограничиваются request size, message length, turns/minute и одновременные LLM calls.
12. Prompt injection и confused-deputy сценарии входят в обязательный regression suite.

## 11. План реализации

Поэтапные задачи, порядок разработки и Definition of Done вынесены в отдельный
[план разработки](OilCaseX.AgentService-development-plan.md).

## 12. RAG как следующий вертикальный блок

После стабилизации Agent MVP подключается отдельный RAG Service. Agent Service должен
воспринимать retrieval как ещё один недоверенный источник observations и требовать:

- citations с document ID и version;
- metadata filters по роли и версии OilCaseX;
- no-answer policy при низкой релевантности;
- запрет инструкциям из документов изменять system/tool policy;
- retrieval diagnostics для evals.

RAG не должен использоваться для определения факта успешного выполнения OilCaseX
операции. Такой факт приходит только из MCP.

## 13. Стратегия тестирования

### Unit tests

- transitions agent state;
- tool policy;
- clarification;
- confirmation coordinator;
- context trimming;
- error mapping;
- duplicate tool-call detection;
- response grounding.

### Integration tests

- Agent ↔ fake vLLM;
- Agent ↔ fake MCP;
- persistence и concurrency;
- streaming contracts;
- JWT/trace propagation без попадания в prompt.

### Contract tests

- обязательные MCP tools существуют;
- schemas совместимы;
- запрещённые tools отсутствуют;
- model tool-call format совместим с parser.

### E2E tests

- read-only вопрос;
- неизвестная площадка;
- неоднозначное имя;
- prepare → preview → confirm → execute;
- reject confirmation;
- expired confirmation;
- replay confirmation;
- LLM/MCP timeout;
- prompt injection;
- пользователь пытается выполнить admin/delete/reset operation.

## 14. Критический путь MVP

```text
Host/API
  → vLLM client
  → MCP client + contract check
  → read-only AgentLoop
  → conversation state
  → clarification
  → prepare/preview
  → explicit confirmation/execute
  → security tests
  → telemetry/evals
  → staging demo
```

RAG, multi-agent orchestration, long-term semantic memory и дополнительные write tools
не должны блокировать первый работающий сценарий создания скважины.

## 15. Итоговый Definition of Done Agent MVP

- пользователь решает read-only сценарии через чат;
- агент создаёт скважину только через `prepare → preview → confirm → execute`;
- Agent Service не обращается к OilCaseX API или БД напрямую;
- tool calls ограничены завершённым MCP catalog и локальной policy;
- JWT и secrets не попадают в LLM context;
- conversation и confirmation state защищены от cross-user доступа;
- ошибки не превращаются в ложное сообщение об успехе;
- Agent, vLLM и MCP связаны trace context;
- unit, integration, contract, security и E2E tests проходят в CI;
- deployment воспроизводим на self-hosted runner `W10534`;
- основной сценарий продемонстрирован на OilCaseX staging.

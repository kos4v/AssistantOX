# OilCaseX AI Agent — план разработки

## 1. Цель проекта

Создать production-oriented AI-агента для OilCaseX, который общается с пользователем через чат, понимает предметные запросы нефтегазового домена, безопасно читает и изменяет данные OilCaseX через MCP и отвечает на вопросы по документации с помощью RAG.

Проект должен демонстрировать максимум компетенций из вакансии:

- разработку AI-агентов и function/tool calling;
- интеграцию LLM в реальный .NET-продукт;
- проектирование RAG-пайплайна, embeddings и vector DB;
- управление контекстом и prompt engineering;
- путь от MVP до production;
- evals, мониторинг и observability;
- C#/.NET, PostgreSQL, Docker, GPU-инфраструктуру, Git и тестирование;
- продуктовый подход и безопасную работу с изменяющими состояние операциями.

Итоговый проект должен быть не «чатом над Swagger», а управляемой AI-системой с явными policy, подтверждением опасных действий, автоматической оценкой качества и сквозной трассировкой.

## 2. Целевое покрытие вакансии

| Требование | Как закрывается проектом | Цель |
|---|---|---:|
| RAG | База знаний OilCaseX, embeddings, vector DB, hybrid retrieval, reranking, ссылки на источники | Полностью |
| AI-агенты | Многошаговый агент, состояние, planning, clarification, recovery | Полностью |
| Function/tool calling | MCP tools поверх OilCaseX API | Полностью |
| Оркестрация моделей | Отдельные модели/режимы для агента, router, RAG generation и eval judge | Полностью |
| Контекст и prompt strategy | Диалоговая память, состояние проекта, summarization, versioned prompts | Полностью |
| MVP → production | Чат, API, Docker, CI/CD, безопасность, эксплуатационная документация | Полностью |
| Evals | Golden dataset, offline/online метрики, regression gates | Полностью |
| Monitoring/observability | OpenTelemetry, traces, metrics, dashboards, audit log | Полностью |
| .NET/C# | MCP gateway, политики, API-клиент и contract tests на .NET | Полностью |
| LLM API и AI-фреймворки | ASP.NET Core agent, OpenAI-compatible API vLLM, MCP SDK и RAG-компоненты | Полностью |
| Локальный model serving | Gemma 4 через vLLM в закрытом GPU-контуре | Полностью |
| Реляционная БД | PostgreSQL для истории, audit/eval данных и OilCaseX | Полностью |
| Docker и Git | Compose, контейнеры, CI, миграции, quality gates | Полностью |
| ML, embeddings | Embedding pipeline и измерение retrieval quality | Хорошо |
| Fine-tuning | Только обоснованный эксперимент после eval baseline | Частично/опционально |
| Коммерческий стаж по времени | Проект показывает уровень работы, но не заменяет формальный стаж | Не закрывается кодом |

## 3. Текущее состояние OilCaseX API

На staging опубликован OpenAPI 3.0.4:

- около 200 paths и 215 HTTP operations;
- около 326 schemas;
- 139 GET и 76 изменяющих состояние операций;
- среди операций есть reset, restore, delete, purchase, изменение конфигурации и admin-действия;
- отсутствуют стабильные `operationId`;
- часть операций не имеет качественного `summary`/`description`;
- OpenAPI не описывает Bearer security scheme и `servers`.

Следствие: нельзя автоматически показать LLM все операции Swagger. MCP должен предоставлять ограниченный, предметно спроектированный набор tools с устойчивыми именами, краткими схемами и отдельными правилами безопасности.

## 4. Основные пользовательские сценарии

### 4.1. Read-only сценарии MVP

1. «Покажи все скважины моей команды».
2. «Какая добыча у скважины X за последний период?»
3. «Сравни показатели скважин X и Y».
4. «Покажи траекторию и текущий статус скважины X».
5. «Можно ли сейчас добавить перфорацию в скважину X?»
6. «Какой статус у последнего расчёта проекта?»
7. «Объясни, что означает этот показатель» — ответ через RAG со ссылкой на источник.

### 4.2. Многошаговые сценарии

1. Описать новую скважину естественным языком → извлечь параметры → запросить недостающие → провалидировать → показать preview → подтвердить → создать через REST API.
2. Найти скважину по имени → получить ID → запросить показатели → объяснить результат.
3. Получить список объектов → выбрать подходящие → сравнить экономические показатели.
4. Запустить расчёт → сохранить идентификатор сессии → проверять статус → сообщить результат.
5. Получить состояние проекта → найти ограничения в документации → предложить допустимые действия.

### 4.3. Изменяющие состояние сценарии

1. Создать скважину из естественного описания пользователя.
2. Переименовать скважину.
3. Изменить параметр только после проверки ограничений.
4. Добавить точку траектории с предварительным показом аргументов.
5. Запустить расчёт после явного подтверждения.

Любое изменение выполняется только после preview и явного подтверждения пользователя. Delete, reset, restore, изменение паролей и admin API не входят в первый production scope.

## 5. Целевая архитектура

```text
OilCaseX Front / Chat UI
          |
          v
ASP.NET Core Agent Service
  - conversation state
  - tool selection
  - clarification
  - context management
  - safety policy
  - response composition
          |
          +----------------------+----------------------+
          |                      |                      |
          v                      v                      v
.NET OilCaseX MCP Server    RAG Service          vLLM Model Server
  - curated tools             - ingestion          - Gemma 4
  - JWT delegation            - embeddings         - OpenAI API
  - authorization             - vector DB          - local GPU
  - validation                - hybrid retrieval   - no internet egress
  - confirmation tokens       - reranker
  - idempotency               - citations
          |                      |
          v                      v
OilCaseX REST API         OilCaseX knowledge base
          |
          v
OilCaseX Domain Services
          |
          v
PostgreSQL / RabbitMQ / MinIO / calculation services

Cross-cutting: OpenTelemetry, metrics, audit, evals, prompt/model registry
```

### 5.1. Границы ответственности

**ASP.NET Core Agent Service:** общается с Gemma 4 через OpenAI-совместимый API vLLM, решает, какой инструмент нужен, формирует типизированные tool calls, управляет контекстом, задаёт уточнения и собирает финальный ответ.

**.NET MCP Server:** не доверяет решениям LLM, повторно валидирует авторизацию и аргументы, применяет allowlist/policy и вызывает только разрешённые операции OilCaseX REST API через типизированный HTTP-клиент.

**vLLM Model Server:** запускает закреплённую версию Gemma 4 внутри закрытого GPU-контура, предоставляет только внутренний OpenAI-compatible endpoint и не имеет доступа к продуктовой БД или MCP.

**RAG Service:** отвечает только по найденным источникам, возвращает цитаты и диагностические retrieval-метаданные.

**OilCaseX API:** остаётся единственным владельцем бизнес-логики и данных. Агент не обращается напрямую к таблицам продуктовой БД.

### 5.2. MCP строго поверх OilCaseX REST API

MCP Server является внешним anti-corruption layer над HTTP-контрактом OilCaseX, а не альтернативным входом в доменную модель. Зависимости направлены только так:

```text
MCP tool
  → tool registry
  → JSON Schema validation
  → authorization/policy
  → confirmation/idempotency
  → OilCaseX API adapter
  → typed HttpClient
  → OilCaseX REST API
  → Domain Services
  → PostgreSQL
```

Архитектурные правила:

- MCP-проекты не ссылаются на `OilCaseX.Domain`, `OilCaseX.Domain.Services` и `OilCaseXContext`;
- MCP не выполняет SQL, не подключается к продуктовой БД и не воспроизводит бизнес-логику OilCaseX;
- JSON Schema проверяет форму tool call, а бизнес-инварианты проверяет OilCaseX API;
- если для preview нужна проверка без записи, в OilCaseX API добавляется отдельный validate/preflight endpoint; MCP не вызывает domain service напрямую;
- MCP вызывает только заранее сопоставленные `operationId` на фиксированном OilCaseX base URL и не принимает произвольный URL от LLM;
- JWT пользователя, `traceparent` и idempotency key передаются в API вне LLM-контекста;
- OilCaseX API остаётся авторитетной точкой авторизации, транзакций и проверки принадлежности данных команде;
- HTTP-ответы и ошибки преобразуются в компактный стабильный MCP-контракт без утечки внутренних stack traces и моделей БД.

Таким образом, Swagger/OpenAPI используется для генерации и проверки API-клиента, а MCP catalog остаётся ручным curated-контрактом для агента.

## 6. Предлагаемая структура репозитория

```text
AI/oilcase_agent/
  README.md
  PLAN.md
  OilCaseX.Agent.sln
  Directory.Build.props
  .env.example
  src/
    OilCaseX.Agent.Api/
    OilCaseX.Agent.Application/
      Orchestration/
      Policies/
      Prompts/
      Memory/
      Tools/
    OilCaseX.Agent.Domain/
    OilCaseX.Agent.Infrastructure/
      Llm/
      Mcp/
      Persistence/
      Observability/
    OilCaseX.Agent.Rag/
      Ingestion/
      Retrieval/
      Generation/
  mcp/
    OilCaseX.McpServer/
      Transport/
      Tools/
      Policies/
      Confirmation/
      ApiClient/
      Security/
      Observability/
    OilCaseX.McpServer.Tests/
  model-serving/
    vllm/
      compose.gpu.yml
      model-config.example.env
  evals/
    datasets/
    graders/
    reports/
  tests/
    unit/
    integration/
    contract/
    e2e/
  docs/
    architecture.md
    threat-model.md
    tool-catalog.md
    eval-methodology.md
    runbook.md
  infra/
    docker-compose.yml
    otel-collector.yml
    prometheus.yml
    grafana/
  scripts/
```

## 7. План реализации

## Этап 0. Product discovery и baseline

### Задачи

- выбрать 10–15 пользовательских задач с реальной ценностью;
- зафиксировать роли пользователей и доступные каждой роли действия;
- разделить операции на read-only, write, destructive и admin;
- собрать первые 50–70 eval-примеров до написания агента;
- измерить baseline: может ли LLM выбрать endpoint и аргументы только по текущему Swagger;
- зафиксировать модель угроз и требования к данным;
- определить ограничения latency и стоимости.

### Результат

- `docs/product-scenarios.md`;
- начальная версия `docs/threat-model.md`;
- eval dataset v0;
- baseline report с ошибками выбора инструментов.

### Definition of Done

- у каждого сценария есть вход, ожидаемые tools, ожидаемый результат и критерий успеха;
- ни один write/destructive сценарий не считается успешным без подтверждения;
- известны владельцы и источники документов для RAG.

## Этап 1. Подготовка OpenAPI и каталога инструментов

### Задачи

- добавить стабильные `operationId` для используемых OilCaseX endpoints;
- дополнить `summary`, `description`, response codes и примеры;
- описать Bearer authentication и `servers`;
- проверить корректность request/response schemas;
- сохранить версионируемый snapshot OpenAPI;
- сформировать curated tool catalog из 15–25 операций;
- убрать из каталога login, password, reset, restore и admin tools;
- определить человекочитаемые MCP names, descriptions и минимальные JSON schemas;
- создать явный mapping `MCP tool → OpenAPI operationId`, не генерируя MCP catalog автоматически из всех endpoints;
- сгенерировать или написать typed API client по зафиксированному OpenAPI snapshot;
- добавить contract test, который обнаруживает несовместимое изменение OpenAPI.

### Результат

- качественный OpenAPI-контракт;
- `docs/tool-catalog.md`;
- allowlist tools;
- автоматическая проверка drift между MCP и Swagger.

### Definition of Done

- каждый MCP tool однозначно связан с OilCaseX operation;
- название инструмента понятно без чтения URL;
- в schema нет лишних полей и огромных вложенных объектов;
- изменение используемого endpoint ломает contract test, а не production.

## Этап 2. Read-only MCP Server на C#/.NET

### Задачи

- создать отдельный ASP.NET Core/.NET MCP Server;
- реализовать typed OilCaseX API client через `HttpClientFactory`;
- работать только через существующий OilCaseX REST API, не подключаясь напрямую к продуктовой БД;
- запретить project references на `OilCaseX.Domain`, `OilCaseX.Domain.Services` и API `DbContext` архитектурным тестом/CI-проверкой;
- использовать фиксированный allowlist base URL и routes без возможности передать URL из tool arguments;
- прокидывать пользовательский JWT без передачи токена в LLM context;
- передавать `traceparent`, correlation ID и idempotency key в OilCaseX API;
- реализовать tools для read-only MVP;
- добавить timeout, retry только для безопасных запросов и circuit breaker;
- нормализовать ошибки API в стабильные MCP error codes;
- ограничить размер ответа и удалить лишние/чувствительные поля;
- добавить correlation/trace ID;
- написать unit, integration и contract tests.

### Результат

- контейнер `oilcase-mcp-server`;
- read-only MCP tools;
- тесты авторизации, валидации и ошибок API.

### Definition of Done

- MCP не может вызвать endpoint вне allowlist;
- MCP не имеет compile-time/runtime доступа к Domain Services и продуктовой БД;
- пользователь видит только данные своей команды;
- JWT и secrets отсутствуют в prompts, traces и логах;
- ошибки OilCaseX не приводят к галлюцинации успешного результата.

## Этап 3. Локальный model serving: Gemma 4 через vLLM

### Задачи

- выбрать поддерживаемый vLLM checkpoint Gemma 4 и закрепить точную model revision;
- проверить лицензию модели и условия локального использования;
- подготовить GPU Docker Compose и конфигурацию NVIDIA runtime;
- поднять внутренний OpenAI-compatible endpoint vLLM;
- настроить quantization, tensor parallelism, context length, batching и GPU memory limits;
- реализовать health, readiness и model warm-up;
- запретить internet egress после доставки образа и весов модели;
- хранить веса в контролируемом локальном registry/volume с checksum;
- закрыть endpoint внутренней сетью и service-to-service authentication;
- исключить prompts, токены и ответы из небезопасных access logs;
- провести benchmark latency, throughput, VRAM и стабильности под параллельной нагрузкой;
- проверить поддержку structured output/tool calling на целевой конфигурации;
- определить timeout, retry и fallback policy для ASP.NET Core Agent Service.

### Результат

- воспроизводимый контейнер `oilcase-vllm`;
- локальная Gemma 4 с OpenAI-совместимым API;
- benchmark report и зафиксированная model configuration;
- инструкция обновления и отката модели.

### Definition of Done

- после подготовки артефактов model server запускается без доступа в интернет;
- ASP.NET Core клиент получает chat completion и типизированный tool call;
- версия модели, tokenizer, quantization и checksum воспроизводимы;
- определены измеренные пределы concurrency, latency и GPU memory;
- недоступность vLLM не приводит к изменению данных OilCaseX.

## Этап 4. Agent MVP на ASP.NET Core

### Задачи

- реализовать ASP.NET Core chat API и потоковую выдачу ответа;
- создать типизированный клиент OpenAI-compatible vLLM API;
- создать stateful agent loop с лимитом шагов;
- подключить MCP client и schema-based tool calling;
- описать `CreateBoreholeDraft`, `CreateBoreholeValidationResult`, `CreateBoreholePreview`, `CreateBoreholeCommand` и `CreateBoreholeResult`;
- реализовать извлечение параметров скважины из естественного языка в `CreateBoreholeDraft`;
- реализовать clarification при отсутствии ID, имени или периода;
- реализовать отдельные уточнения для обязательных параметров новой скважины;
- добавить обработку multi-intent запросов;
- хранить историю диалога и structured state отдельно;
- ограничить контекст sliding window и summary старых сообщений;
- версионировать system prompts;
- валидировать tool arguments до вызова MCP;
- реализовать fallback при timeout, malformed tool call и недоступности модели;
- добавить unit tests с fake vLLM client и fake MCP.

### Результат

- работающий чат для read-only сценариев;
- воспроизводимые agent traces;
- минимум одна end-to-end демонстрация с OilCaseX staging.

### Definition of Done

- агент не вызывает неизвестные tools;
- любой tool call имеет trace ID и сохраняемый результат;
- после ошибки агент не утверждает, что действие выполнено;
- read-only и draft-сценарии создания скважины проходят автоматический e2e smoke test.

## Этап 5. Безопасные write tools

### Задачи

- реализовать policy engine независимо от LLM;
- ввести двухфазную схему `prepare → confirm → execute`;
- реализовать `prepare_create_borehole` и `execute_create_borehole` поверх OilCaseX REST API;
- выполнять доменную проверку новой скважины через validate/preflight endpoint OilCaseX API до выдачи preview;
- при отсутствии такого endpoint добавить его в OilCaseX API, сохранив бизнес-правила в существующем domain layer;
- формировать preview изменения понятным пользователю текстом;
- выпускать короткоживущий confirmation token, связанный с user, tool и arguments hash;
- передавать idempotency key в OilCaseX API и обеспечить end-to-end защиту от повторной записи;
- повторно проверять права и актуальность данных перед execute;
- вести immutable audit log;
- запретить массовые и destructive операции;
- добавить защиту от prompt injection в API-данных и RAG-документах;
- протестировать отмену, повтор подтверждения, истечение token и race conditions.

### Результат

- 2–4 безопасных write tools;
- журнал действий;
- threat-model и security tests.

### Definition of Done

- без подтверждения выполнено 0 write operations;
- изменение аргументов после preview делает confirmation token недействительным;
- повторная отправка execute не дублирует действие;
- destructive/admin endpoints недоступны даже при прямой MCP-команде.

## Этап 6. RAG по знаниям OilCaseX

### Источники

- пользовательская документация;
- бизнес-правила и инструкции;
- описание предметных терминов;
- архитектурная и API-документация;
- справка по статусам, расчётам и ограничениям;
- разрешённые примеры и FAQ.

### Задачи

- создать ingestion pipeline с очисткой и версионированием документов;
- разработать domain-aware chunking;
- добавить embeddings и vector DB;
- реализовать hybrid retrieval: vector + keyword;
- добавить metadata filters по роли, версии и типу документа;
- подключить reranker;
- возвращать цитаты, document ID и версию источника;
- реализовать no-answer policy;
- защититься от prompt injection внутри документов;
- хранить retrieval diagnostics для evals.

### Результат

- RAG service и knowledge MCP/resources;
- воспроизводимый индекс;
- ответы с проверяемыми источниками.

### Definition of Done

- ответ без достаточного источника явно помечается как неизвестный;
- каждое фактическое утверждение RAG-ответа связано с источником;
- переиндексация воспроизводима из Docker/CI;
- доступ к документам учитывает роль пользователя.

## Этап 7. Evals и quality gates

### Наборы данных

- routing/tool selection;
- argument extraction;
- clarification;
- multi-step tasks;
- создание скважины из естественного описания;
- read/write safety;
- prompt injection/adversarial;
- RAG retrieval;
- grounded answer generation;
- no-answer;
- regression production cases.

### Метрики

- tool selection accuracy;
- exact/semantic argument accuracy;
- end-to-end task success;
- invalid tool call rate;
- unsafe execution count;
- confirmation compliance;
- Recall@K, MRR или nDCG для retrieval;
- groundedness/faithfulness;
- citation correctness;
- no-answer precision/recall;
- latency p50/p95;
- tokens и стоимость на успешный сценарий.

### Начальные quality gates

- read-only tool selection accuracy ≥ 95%;
- корректность обязательных аргументов ≥ 95%;
- unsafe write без подтверждения = 0;
- RAG Recall@5 ≥ 85% на размеченном наборе;
- citation correctness ≥ 95%;
- end-to-end success ≥ 85% для основного набора;
- любой regression в safety-блоке запрещает merge/deploy.

Пороги уточняются после baseline, но safety-инварианты не ослабляются.

### Автоматизация

- быстрый deterministic eval subset на каждый pull request;
- полный offline eval перед release и по расписанию;
- сравнение candidate с текущим production baseline;
- сохранение model, prompt, tool schema, dataset и commit versions;
- отчёт с примерами ошибок, а не только агрегированным score.

### Definition of Done

- изменение prompt/model/tool schema имеет измеримый отчёт;
- release нельзя выполнить при провале safety или критической регрессии;
- ошибки eval превращаются в новые regression cases.

## Этап 8. Observability и мониторинг

### Задачи

- внедрить OpenTelemetry в ASP.NET Core Agent, .NET MCP, vLLM client и OilCaseX HTTP client;
- прокидывать W3C trace context через всю цепочку;
- создать structured logs с redaction;
- собирать Prometheus metrics;
- подготовить Grafana dashboards;
- логировать model/prompt/tool versions;
- добавить alerting и runbook;
- реализовать сбор пользовательской обратной связи;
- выделить online quality signals без автоматического использования пользовательских данных для обучения.

### Обязательные метрики

- request и step latency;
- LLM latency, tokens и cost;
- vLLM queue time, throughput, GPU utilization и GPU memory;
- MCP/API success/error/timeout rate;
- tool calls по типам;
- clarification rate;
- write confirmation/cancel rate;
- RAG no-answer и retrieval-empty rate;
- пользовательская оценка;
- количество policy violations и blocked actions.

### Definition of Done

- один trace показывает путь от сообщения до OilCaseX response;
- JWT, passwords, secrets и полные чувствительные payloads не попадают в telemetry;
- для ошибок LLM, MCP, API и vector DB настроены отдельные alerts;
- есть инструкция диагностики и отката.

## Этап 9. Chat UI и продуктовая интеграция

### Задачи

- встроить чат в существующий OilCaseX frontend;
- добавить streaming ответа и отображение прогресса tools;
- показывать источники RAG;
- визуализировать preview write-операции;
- реализовать confirm/cancel UX;
- показывать понятные ошибки и возможность повторить безопасный шаг;
- собирать thumbs up/down и категорию проблемы;
- добавить feature flag и поэтапное включение пользователям.

### Definition of Done

- пользователь понимает, когда агент только отвечает, а когда меняет данные;
- подтверждение нельзя сделать случайным нажатием в другом диалоге;
- UI не показывает внутренние prompts, stack traces и secrets;
- основной пользовательский путь протестирован e2e.

## Этап 10. Production hardening и deployment

### Задачи

- собрать все сервисы в Docker;
- добавить отдельный GPU deployment profile для vLLM/Gemma 4;
- обеспечить локальную доставку образов и весов модели в закрытый контур;
- запретить исходящий интернет-доступ model server в production;
- добавить health/readiness probes;
- настроить migrations и rollback;
- установить rate limits, concurrency limits и request size limits;
- добавить model timeout, fallback и budget policy;
- реализовать graceful degradation без RAG или LLM;
- провести нагрузочное тестирование;
- провести security review и prompt-injection red teaming;
- добавить backup/retention policy для history, audit и eval данных;
- подготовить runbook, incident сценарии и release checklist.

### CI/CD quality gates

- .NET Agent/MCP/RAG: format, build, analyzers, unit/integration/contract tests;
- vLLM: configuration validation, model availability и inference smoke test;
- OpenAPI backward compatibility;
- dependency/security scan;
- Docker image scan;
- eval regression subset;
- запрет merge при safety regression.

### Definition of Done

- staging поднимается одной командой;
- release воспроизводим и имеет rollback;
- сервис выдерживает согласованную нагрузку и latency SLO;
- отказ внешней модели не повреждает данные OilCaseX.

## Этап 11. ML/fine-tuning эксперимент — только при наличии оснований

Fine-tuning не является целью сам по себе. Сначала должны быть baseline, eval dataset и классификация ошибок.

Возможные эксперименты:

- fine-tuning небольшой модели для tool routing;
- domain embedding model comparison;
- reranker fine-tuning на размеченных парах query/document;
- distillation дорогого agent-router в более дешёвую модель.

Эксперимент принимается только если он статистически улучшает quality/cost/latency относительно prompt-based baseline и не ухудшает safety.

## 8. Стратегия тестирования

### Unit tests

- policy engine;
- state transitions;
- natural-language parsing в `CreateBoreholeDraft`;
- tool argument validation;
- confirmation token;
- prompt/context builders;
- RAG filters и answer composition;
- OpenAPI-to-tool mapping.

### Integration tests

- agent ↔ fake MCP;
- agent ↔ fake/real vLLM OpenAI-compatible API;
- MCP ↔ staging/mock OilCaseX;
- RAG ↔ vector DB;
- JWT delegation и role checks;
- timeout/retry/circuit breaker;
- telemetry propagation.

### Contract tests

- OpenAPI snapshot compatibility;
- request/response schemas;
- MCP tool schemas;
- стабильность error codes;
- отсутствие запрещённых endpoints в MCP catalog.

### End-to-end tests

- read-only вопрос;
- многошаговое сравнение;
- RAG-вопрос с источником;
- write preview/confirm/execute;
- создание скважины из полного описания;
- создание скважины с последовательными уточнениями;
- cancel;
- попытка destructive action;
- prompt injection;
- сбой LLM/MCP/OilCaseX.

## 9. Безопасность

Обязательные инварианты:

1. LLM никогда не получает JWT, password, connection string или secret.
2. MCP повторно проверяет роль и принадлежность данных пользователю/команде.
3. Tool allowlist формируется в коде, а не из ответа модели.
4. Все write tools требуют server-side confirmation token.
5. Destructive/admin tools отсутствуют в production MCP catalog.
6. Данные от API и RAG считаются недоверенными и не могут менять system policy.
7. В логах и traces работает redaction.
8. Agent не выполняет произвольные URL, SQL, shell или dynamic tool names.
9. Для каждого изменения существует audit record.
10. При сомнении система прекращает действие и просит уточнение/подтверждение.
11. MCP не ссылается на Domain Services/DbContext и обращается только к фиксированному OilCaseX REST API.

## 10. Управление контекстом

- отдельно хранить сообщения и структурированное состояние сущностей;
- не просить LLM повторно извлекать известные ID из полного диалога;
- связывать выбранные сущности с user/team/session;
- использовать sliding window и summary для старой истории;
- ограничивать размер MCP payload;
- не сохранять hidden chain-of-thought;
- сохранять только tool decision, arguments, observation и публичное объяснение;
- версионировать prompts и policies;
- очищать контекст при смене команды/проекта/пользователя.

## 11. Рекомендуемая последовательность релизов

### Release A — Read-only MVP

- 8–12 read-only MCP tools;
- ASP.NET Core Agent Service;
- локальная Gemma 4 через vLLM;
- простая chat UI;
- JWT delegation;
- базовые traces;
- 50–70 eval cases.

### Release B — Knowledge assistant

- RAG ingestion/retrieval;
- citations и no-answer;
- 120–150 eval cases;
- retrieval и groundedness metrics.

### Release C — Safe actions

- создание скважины по естественному описанию;
- ещё 2–4 write tools;
- preview/confirm/execute;
- audit/idempotency;
- adversarial и security evals.

### Release D — Production pilot

- OpenTelemetry/Grafana/alerts;
- полный CI/CD;
- feature flags;
- нагрузочное и security тестирование;
- 200+ regression/eval cases;
- runbook и rollback.

## 12. Что показывать на собеседовании

### Демонстрация

1. Пользователь естественным языком описывает новую скважину.
2. ASP.NET Core Agent получает structured tool call от локальной Gemma 4 через vLLM.
3. Агент уточняет недостающие или неоднозначные параметры.
4. MCP Server валидирует draft через OilCaseX REST API.
5. Агент показывает preview и ожидает confirmation.
6. После подтверждения MCP создаёт скважину через REST API и пишет audit record.
7. Агент получает созданную скважину и подтверждает результат фактическими данными API.
8. Дополнительный ответ объединяет OilCaseX данные и объяснение из RAG с источниками.
9. В trace UI/Grafana видна цепочка ASP.NET Agent → vLLM/MCP → OilCaseX API.
10. Eval report показывает качество до и после изменения prompt/model.

### Артефакты

- архитектурная диаграмма;
- ADR с выбором ASP.NET Core Agent + .NET MCP Server + REST boundary;
- benchmark локальной Gemma 4/vLLM и описание закрытого GPU-контура;
- tool catalog и threat model;
- OpenAPI contract diff;
- eval dataset и отчёт;
- dashboard screenshot;
- CI pipeline;
- короткое demo video;
- список продуктовых метрик и известных ограничений.

### Корректное описание опыта

> Разработал чат-агента на ASP.NET Core для создания скважин в OilCaseX по естественному описанию: развернул Gemma 4 через vLLM в закрытом GPU-контуре, спроектировал .NET MCP Server над авторизованным REST API, типизированный tool calling с доменной валидацией и подтверждением операций, RAG, автоматические evals и сквозную OpenTelemetry-трассировку.

Не заявлять fine-tuning, embeddings, production usage или достигнутые метрики, пока соответствующая часть действительно не реализована и не измерена.

## 13. Главные риски

| Риск | Мера |
|---|---|
| 215 Swagger operations перегружают контекст | Curated allowlist, tool routing и компактные schemas |
| Gemma 4/vLLM недостаточно стабильно формирует tools | Structured-output evals, constrained schemas, retry/fallback и закреплённая версия модели |
| Нехватка GPU memory или высокая latency | Quantization, context limits, batching, benchmark и capacity budget |
| LLM выбирает опасное действие | Независимый policy engine и confirmation protocol |
| Swagger меняется | Snapshot и contract tests |
| Prompt injection из API/RAG | Недоверенный контент, строгие boundaries и adversarial evals |
| Утечка JWT/данных | Делегирование токена вне LLM, redaction, least privilege |
| Галлюцинация успешного действия | Финальный ответ только по подтверждённому MCP observation |
| Плохой retrieval | Размеченный dataset, hybrid search, reranking и Recall@K |
| Высокая latency/cost | Model routing, caching, payload limits, budgets и измерение |
| Невоспроизводимое качество | Versioning model/prompt/tools/data и regression gates |
| Demo остаётся pet-проектом | Pilot, реальные пользователи, feedback loop, SLO и runbook |

## 14. Финальный критерий готовности проекта

Проект считается максимально покрывающим техническую часть вакансии, когда:

- агент решает реальные read-only и write OilCaseX задачи;
- ASP.NET Core Agent Service и MCP Server написаны и протестированы на .NET/C#;
- Gemma 4 развёрнута через vLLM в закрытом GPU-контуре с воспроизводимой конфигурацией;
- создание скважины из естественного описания проходит clarification, domain validation, preview и confirmation;
- RAG использует embeddings/vector search и показывает источники;
- опасное действие технически невозможно без подтверждения;
- есть минимум 200 версионируемых eval/regression сценариев;
- PR и release блокируются при quality/safety regression;
- доступна сквозная трассировка ASP.NET Agent → vLLM/MCP → OilCaseX REST API;
- проект разворачивается через Docker и имеет CI/CD;
- опубликованы архитектура, threat model, eval methodology и runbook;
- качество, latency и стоимость подтверждены измерениями, а не только demo.

# Threat model Agent Service (этап 0)

## Границы доверия

```text
User/Chat UI → Agent Service → vLLM/Gemma 4
                     |
                     +------→ OilCaseX.McpServer → OilCaseX API/DB
```

Пользовательский текст, ответ модели и MCP observations считаются недоверенными
данными. System policy, authenticated identity и tool allow-list формируются сервером.

## Основные угрозы и меры

| Угроза | Мера |
|---|---|
| prompt injection | недоверенные observations отделяются от system policy; adversarial evals |
| hallucinated success | success только по сохранённому MCP observation |
| confused deputy | conversation/confirmation binding к user/team; JWT передаётся в MCP |
| unsafe write | execute только через отдельный confirmation flow |
| unknown/dynamic tool | immutable catalog и local allow-list |
| token leakage | JWT вне prompt; redaction logs/stream |
| replay | TTL, одноразовость confirmation и API idempotency |
| DoS | body/message/tool/step/concurrency limits |
| cross-user state access | authenticated owner/team checks и repository authorization |
| dependency failure | bounded timeout, cancellation и graceful degradation |

## Security invariants

- write без explicit confirmation: 0;
- tool вне allow-list: 0;
- cross-user confirmation execution: 0;
- JWT и product DB credentials в LLM context: 0;
- неизвестный результат write не считается успехом.

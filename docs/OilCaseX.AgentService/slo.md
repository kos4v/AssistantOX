# Начальные SLO Agent Service

Значения являются стартовыми и уточняются после измерений на staging.

| Метрика | Цель |
|---|---|
| p95 health/live | < 100 ms |
| p95 read-only turn без LLM queue | < 10 s |
| p95 MCP call overhead | < 500 ms |
| maximum agent steps | 6 |
| maximum MCP calls per turn | 4 |
| write retry after unknown result | 0 |
| unsafe write without confirmation | 0 |
| hallucinated successful write | 0 |

Каждый turn должен иметь correlation ID и trace ID. SLO не разрешает автоматический
retry для `execute_create_borehole`.

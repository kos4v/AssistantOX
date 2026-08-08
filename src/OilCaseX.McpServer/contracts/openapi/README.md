# OpenAPI contracts

Файлы в этой директории являются versioned contract inputs для typed OilCaseX API client.

- `oilcasex.v1.raw.json` — точная копия staging Swagger без изменений;
- `oilcasex.v1.mcp.json` — curated snapshot для MCP: только MVP routes и локальные MCP-метаданные;
- `oilcasex.v1.mcp.manifest.json` — source/curated hashes и mapping tools.

Обновление выполняется скриптом:

```powershell
pwsh ./scripts/update-openapi-snapshot.ps1
pwsh ./scripts/validate-openapi.ps1

# после установки NSwag.ConsoleCore 14.7.1
nswag run ./contracts/openapi/oilcasex.v1.mcp.nswag
```

Overlay добавляет стабильные operation IDs, Bearer security, `servers`, summaries,
descriptions и стандартные error responses. Это не изменяет внешний OilCaseX API.

`POST /Api/V1/Purchased/Borehole/Validate` теперь присутствует в staging Swagger и
попадает в generated client при обычном обновлении snapshot. Файл
`oilcasex.v1.mcp.stage4-overlay.json` сохранён как исторический reference для этапа 4;
он больше не является источником этого маршрута и hand-written preflight adapter не нужен.

Write route `createPurchasedBorehole` присутствует в contract для фиксации API mapping,
но имеет статус `blocked-until-preflight`: его нельзя публиковать как production MCP tool,
пока OilCaseX API не предоставит безопасный preflight и end-to-end idempotency.

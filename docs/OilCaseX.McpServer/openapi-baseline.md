# OilCaseX OpenAPI baseline

- Owner: OilCaseX API team
- Environment: staging
- URL: `https://x.stg.oilcase.ru/swagger/v1/swagger.json`
- Captured: 2026-08-07
- OpenAPI: `3.0.4`
- Document version: `v1`
- Paths: `200`
- Schemas: `326`
- SHA-256 (response bytes): `402f7d8d514a7786d88ecced20e9b27fd2a1f893bb177c90a844ed7f1017ab26`
- `components.securitySchemes`: отсутствует
- `servers`: отсутствует
- `operationId`: отсутствует в исходном staging-документе у операций, выбранных для MVP baseline

## Значение baseline

Этот документ фиксирует наблюдаемый staging-контракт до изменения API. Raw snapshot
сохраняет его без изменений, а curated snapshot содержит локальный overlay для MCP.

Артефакты этапа 1:

- `../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.raw.json` — исходный snapshot;
- `../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.mcp.json` — 6 MVP operations с локальными operation IDs,
  Bearer security, `servers`, описаниями и стандартными error responses;
- `../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.mcp.manifest.json` — hashes и mapping tools;
- `../../src/OilCaseX.McpServer/generated/OilCaseXApiClient.g.cs` — результат NSwag generation;
- `../../src/OilCaseX.McpServer/contracts/openapi/oilcasex.v1.mcp.nswag` — воспроизводимая конфигурация генератора.

Curated snapshot SHA-256: `cb17597205167a2ce8fa3bf69b98f34b7fd2c41a76f251c1f0426aafcefe6ef0`.
Snapshot детерминирован: повторное обновление без изменения upstream возвращает тот же hash.

До генерации typed client нужно исправить или дополнить контракт:

1. добавить Bearer security scheme;
2. добавить `servers` или определить trusted base URL отдельно от Swagger;
3. добавить стабильные `operationId`;
4. описать response/error schemas для MVP;
5. добавить validate/preflight endpoint для безопасного preview создания скважины.

## Наблюдаемые MVP-маршруты

| Предметное действие | HTTP route baseline | operationId |
|---|---|---|
| Список кустовых площадок | `GET /Api/V1/Purchased/Wellpad` | `listWellpads` в curated snapshot |
| Кустовая площадка | `GET /Api/V1/Purchased/Wellpad/{wellpadId}` | `getWellpad` в curated snapshot |
| Список скважин | `GET /Api/V1/Purchased/Borehole/All` | `listBoreholes` в curated snapshot |
| Информация о скважине | `GET /Api/V1/Purchased/Borehole/BoreholeInfo/{boreholeId}` | `getBorehole` в curated snapshot |
| Добыча скважины | `GET /Api/V1/Production/Info/Borehole/{boreholeId}` | `getBoreholeProduction` в curated snapshot |
| Создание купленной скважины | `POST /Api/V1/Purchased/Borehole` | `createPurchasedBorehole`; production mapping заблокирован до preflight/idempotency |

Внешний API пока не изменён: operation IDs добавлены локальным overlay и должны быть
внесены в официальный OilCaseX API-контракт на следующем цикле API. До этого typed client
генерируется только из curated snapshot, а не напрямую из staging Swagger.

## Проверка и генерация

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\update-openapi-snapshot.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-openapi.ps1

# NSwag 14.7.1
nswag run .\contracts\openapi\oilcasex.v1.mcp.nswag
```

Curated overlay удаляет только machine-generated numeric bounds за пределами Decimal range,
которые не читаются NSwag/NJsonSchema и не являются предметными ограничениями API.

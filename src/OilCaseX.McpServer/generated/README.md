# Generated API client

`OilCaseXApiClient.g.cs` генерируется из `contracts/openapi/oilcasex.v1.mcp.json` через
NSwag 14.7.1. Файл не редактируется вручную.

Генерация:

```powershell
nswag run .\contracts\openapi\oilcasex.v1.mcp.nswag
```

Сгенерированный интерфейс регистрируется через typed `HttpClientFactory` с delegated JWT,
trace context, response limits и error mapping. В актуальном staging Swagger уже описан
preflight `validatePurchasedBorehole`, поэтому отдельный hand-written HTTP-клиент для него
не используется.

# Generated API client

`OilCaseXApiClient.g.cs` генерируется из `contracts/openapi/oilcasex.v1.mcp.json` через
NSwag 14.7.1. Файл не редактируется вручную.

Генерация:

```powershell
nswag run .\contracts\openapi\oilcasex.v1.mcp.nswag
```

На этапе 2/3 сгенерированный интерфейс будет обёрнут в typed `HttpClientFactory` adapter
с delegated JWT, timeout, trace context, response limits и error mapping.

# Contract compile smoke test

Этот проект проверяет, что typed client, сгенерированный NSwag из curated OpenAPI snapshot,
компилируется на целевой версии .NET.

```powershell
dotnet build .\src\OilCaseX.McpServer.ContractCompile\OilCaseX.McpServer.ContractCompile.csproj
```

Runtime adapter, `HttpClientFactory`, delegated JWT и error mapping будут добавлены на
этапах 2–3.

# ADR-0002: MCP protocol, .NET SDK и transport

- Статус: Accepted
- Дата: 2026-08-07
- Владелец решения: OilCaseX AI Agent team

## Решение

| Решение | Зафиксированное значение |
|---|---|
| .NET target | `net10.0` |
| C# MCP server package | `ModelContextProtocol.AspNetCore` `1.4.1` |
| C# MCP core package | `ModelContextProtocol` `1.4.1` |
| MCP protocol target | `2025-06-18` через protocol negotiation |
| Network transport | Streamable HTTP |
| MCP endpoint | `/mcp` |
| Legacy SSE | Не включать в production |
| Serialization | SDK JSON serialization; tool schemas должны быть стабильными и компактными |

Версия SDK закрепляется явно в project file. Переход на 2.x или новый protocol revision
делается отдельным ADR после совместимого contract/conformance теста.

## Почему Streamable HTTP

- Agent Service и MCP Server — отдельные ASP.NET Core процессы;
- transport проходит через стандартную HTTP-инфраструктуру, proxy и telemetry;
- endpoint легко защищается Bearer authentication и rate limits;
- transport не смешивает MCP protocol с внутренним OilCaseX API;
- не нужен запуск дочернего процесса MCP через stdio.

`/mcp` — единственная точка MCP-входа. REST endpoint OilCaseX (`/Api/...`) не является
MCP endpoint и не должен передаваться LLM.

## Требования совместимости

- client использует endpoint `/mcp`, а не legacy `/sse`;
- initialize должен завершаться согласованной поддерживаемой версией протокола;
- `tools/list` и `tools/call` покрываются smoke tests;
- SDK upgrade требует повторного теста streaming, cancellation, session и error mapping.

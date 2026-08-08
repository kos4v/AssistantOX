# OilCaseX Agent UI

Blazor Web App с интерактивным серверным рендерингом. UI не обращается к MCP напрямую:
он подключается к защищённому `AgentChatHub`, который использует тот же
`AgentOrchestrator`, что и HTTP API.

## Запуск

1. Запустите `OilCaseX.Agent.Api` на `https://localhost:52224`.
2. Настройте `AgentUi:HubUrl` и `AgentUi:AccessToken` в `appsettings.json` или secrets.
3. Запустите `OilCaseX.Agent.Ui` на `https://localhost:52226`.

`AgentChatClientFallback` использует `ChatClientAgent` из `first_agent`-стека только
при недоступности Hub. У fallback нет MCP tools и он не может изменять OilCaseX.

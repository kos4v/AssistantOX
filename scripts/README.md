# Локальный совместный запуск

Из `AI/AssistantOX` запустите:

```powershell
.scriptsstart-agent-stack.ps1
```

Скрипт поднимает три проекта в правильном порядке:

| Проект | URL |
|---|---|
| `OilCaseX.McpServer` | `http://localhost:5089` |
| `OilCaseX.Agent.Api` | `http://localhost:52225` |
| `OilCaseX.Agent.Ui` | `http://localhost:52227` |

В Development-профиле Agent API использует локальную identity `local-user` только
для совместного запуска. В production этот режим отключён и используется JWT bearer.

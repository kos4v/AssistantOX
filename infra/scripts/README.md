# Локальный совместный запуск

Из `AI/AssistantOX` запустите:

```powershell
.\infra\scripts\start-agent-stack.ps1
```

Скрипт поднимает три проекта в правильном порядке:

| Проект | URL |
|---|---|
| `OilCaseX.McpServer` | `http://localhost:5089` |
| `OilCaseX.Agent.Api` | `http://localhost:52225` |
| `OilCaseX.Agent.Ui` | `http://localhost:52227` |

В Development-профиле Agent API использует локальную identity `local-user` только
для совместного запуска. В production этот режим отключён и используется JWT bearer.

## Docker Compose

Для локального контейнерного запуска:

```powershell
Copy-Item infra/compose/.env.example infra/compose/.env
docker compose --env-file infra/compose/.env -f infra/compose/docker-compose.yml -f infra/compose/docker-compose.dev.yml up --build
```

Production запускается только базовым compose-файлом и требует JWT-настройки и
секретов через environment/runner secrets.

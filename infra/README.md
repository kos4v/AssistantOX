# Infrastructure

Инфраструктура AgentService собрана в этой папке:

- `compose/` — production и development Docker Compose;
- `docker/` — Dockerfile MCP Server, Agent API и Blazor UI;
- `scripts/` — совместный локальный запуск проектов.

GitHub Actions workflow оставлен в `.github/workflows`, поскольку GitHub обнаруживает
workflow только из этого каталога. Он использует Docker Compose из `infra/compose` и
запускается на self-hosted runner `W10534`.

Корневой `.dockerignore` также оставлен в корне репозитория: Docker применяет его к
контексту сборки, который намеренно включает `src/`.

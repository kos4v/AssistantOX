# OilCaseX.AgentService MVP

Agent Service uses the same local-model setup as `sandbox/first_agent`: an
OpenAI-compatible endpoint is adapted with `OpenAIClient.GetChatClient(...).AsIChatClient()`
and MCP tools are discovered through `McpClient` over Streamable HTTP.

## Configuration

The `AgentService` section can be overridden by the environment variables used by
`first_agent`:

- `LOCAL_LLM_BASE_URL` or `VLLM_BASE_URL` (default: `http://192.168.19.120:1704/v1`)
- `LOCAL_LLM_MODEL` or `VLLM_MODEL` (default: `prism-ml/bonsai-27b`)
- `LOCAL_LLM_API_KEY` or `VLLM_API_KEY` (default development value: `lm-studio`)
- `OILCASE_MCP_URL` (default: `http://localhost:5089/mcp`)

Do not put a real API key in repository configuration or logs.

## API flow

1. `POST /api/v1/conversations/{id}/messages` starts a turn.
2. Read tools and `prepare_create_borehole` may be called by the bounded agent loop.
3. A prepare result returns `status=confirmation_required` and a preview.
4. `POST /api/v1/conversations/{id}/confirm` calls `execute_create_borehole` with only
   the stored `confirmationId`.
5. `POST /api/v1/conversations/{id}/reject` discards the pending operation.

All endpoints require the configured JWT bearer authentication. Conversation state is
currently in-memory for the MVP and must be replaced with distributed persistence before
production deployment.

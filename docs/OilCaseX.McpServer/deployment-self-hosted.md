# Self-hosted deployment

Deployment workflow: [deploy-self-hosted.yml](../../.github/workflows/deploy-self-hosted.yml).

## Runner

The workflow runs only on a self-hosted GitHub Actions runner with both labels:

```yaml
runs-on: [self-hosted, W10534]
```

The runner must have:

- Docker CLI;
- a running Docker daemon with permission for the runner service account;
- Linux container support;
- network access to the configured OilCaseX API.

## What the workflow does

1. Checks Docker CLI and daemon availability.
2. Builds `oilcasex-mcpserver:<commit-sha>` from the repository root.
3. Replaces the `oilcasex-mcpserver` container.
4. Publishes container port `8080` on host port `5089`.
5. Waits for `/health/live` and verifies `/health/ready`.

The workflow runs for pushes to `master` or `main` and can also be started manually
with `workflow_dispatch`. Concurrent deployments are serialized.

## Configuration

Configure these values in the `staging-self-hosted` GitHub Environment:

- Variable `OILCASEX_BASE_URL` — trusted OilCaseX base URL;
- Variable `OILCASEX_HEALTH_PATH` — readiness path, default `/swagger/v1/swagger.json`;
- Secret `OTEL_EXPORTER_OTLP_ENDPOINT` — optional OTLP collector endpoint.
- The trusted authentication boundary must populate role claims used by
  `McpServer:WriteRoles` before exposing write tools.

No JWT, database password or other product secret is stored in the repository. Delegated
JWT is copied per request to OilCaseX API; it is never written to logs or tool arguments.

## Rollback

Images are tagged by commit SHA. To roll back on `W10534`, stop the current container and
start a previously published `oilcasex-mcpserver:<commit-sha>` image with the same
configuration and port mapping. The workflow intentionally does not prune old images
automatically so that a previous tag remains available for rollback.

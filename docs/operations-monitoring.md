# Monitoring & Alerts

## Observability stack
- Azure Log Analytics workspace
- Application Insights for API/worker telemetry
- Azure Monitor metric alerts (see `infra/azure/alerts.bicep`)

## Suggested dashboards
- API request rate, p95 latency, failed requests
- Job throughput (queued/running/completed/needs_review)
- Queue depth
- Worker loop heartbeat

## Alert recommendations
1. API failed requests > 20 in 15m (severity 2)
2. Queue depth > threshold for 10m
3. No completed jobs in 60m during business hours
4. Worker unavailable > 5m

## Runbook sketch
- Validate dependency health (`/health`, Redis ping, Postgres connectivity)
- Check recent deployments and rollback if correlated
- Inspect dead-letter or failed jobs and retry from admin tooling
- Escalate if repeated OAuth provider failures occur

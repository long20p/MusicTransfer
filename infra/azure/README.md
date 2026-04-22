# Azure Deployment

## Included in this milestone
- `main.bicep`: baseline Azure infrastructure deployment
  - Container Apps environment
  - API + Worker Container Apps
  - PostgreSQL Flexible Server
  - Redis Cache
  - Log Analytics + Application Insights
- `alerts.bicep`: basic Azure Monitor alert resources
- `main.parameters.json`: sample parameter file
- GitHub Actions workflows:
  - `.github/workflows/ci.yml`
  - `.github/workflows/deploy-azure.yml`

## Deploy
```bash
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/azure/main.bicep \
  --parameters @infra/azure/main.parameters.json
```

## Notes
- Replace placeholder secrets with Key Vault references before production use.
- Configure `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_RESOURCE_GROUP` secrets in GitHub.

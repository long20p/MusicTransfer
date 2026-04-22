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
- Deployment mode: manual Azure CLI deployment (no GitHub workflow required)

## Deploy
```bash
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/azure/main.bicep \
  --parameters @infra/azure/main.parameters.json
```

## Notes
- Replace placeholder secrets with Key Vault references before production use.
- For manual deploy, authenticate with `az login` and ensure RBAC on the target resource group.

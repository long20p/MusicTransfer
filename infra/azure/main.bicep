@description('Base name for all resources')
param namePrefix string = 'musictransfer'

@description('Deployment location')
param location string = resourceGroup().location

@description('Container image for API')
param apiImage string

@description('Container image for worker')
param workerImage string

resource la 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${namePrefix}-law'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource ai 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-appi'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: la.id
  }
}

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: la.properties.customerId
        sharedKey: listKeys(la.id, la.apiVersion).primarySharedKey
      }
    }
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: '${namePrefix}-pg'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '15'
    storage: { storageSizeGB: 32 }
    administratorLogin: 'mtadmin'
    administratorLoginPassword: 'ChangeMe-In-KeyVault'
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource redis 'Microsoft.Cache/Redis@2023-08-01' = {
  name: '${namePrefix}-redis'
  location: location
  properties: {
    minimumTlsVersion: '1.2'
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-api'
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      registries: []
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: [
            { name: 'ConnectionStrings__Postgres', value: 'Host=${postgres.name}.postgres.database.azure.com;Database=postgres;Username=mtadmin;Password=ChangeMe-In-KeyVault;Ssl Mode=Require' }
            { name: 'ConnectionStrings__Redis', value: '${redis.name}.redis.cache.windows.net:6380,password=ChangeMe-In-KeyVault,ssl=True,abortConnect=False' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: ai.properties.ConnectionString }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-worker'
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: { registries: [] }
    template: {
      containers: [
        {
          name: 'worker'
          image: workerImage
          env: [
            { name: 'ConnectionStrings__Postgres', value: 'Host=${postgres.name}.postgres.database.azure.com;Database=postgres;Username=mtadmin;Password=ChangeMe-In-KeyVault;Ssl Mode=Require' }
            { name: 'ConnectionStrings__Redis', value: '${redis.name}.redis.cache.windows.net:6380,password=ChangeMe-In-KeyVault,ssl=True,abortConnect=False' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: ai.properties.ConnectionString }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 2 }
    }
  }
}

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output appInsightsConnectionString string = ai.properties.ConnectionString

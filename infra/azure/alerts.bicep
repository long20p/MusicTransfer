@description('Application Insights resource name')
param appInsightsName string

@description('Location')
param location string = resourceGroup().location

resource appi 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${appInsightsName}-ag'
  location: 'global'
  properties: {
    groupShortName: 'mtops'
    enabled: true
    emailReceivers: []
  }
}

resource failedReqAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${appInsightsName}-failed-requests'
  location: location
  properties: {
    description: 'High failed request count in API'
    severity: 2
    enabled: true
    scopes: [appi.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'failed-requests'
          metricName: 'requests/failed'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 20
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

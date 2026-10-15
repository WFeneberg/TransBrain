// TransBrain on Azure Container Apps. Scope: resource group.
//   az deployment group create -g <rg> -f infra/main.bicep -p deployApps=false ...   (1st pass: base infrastructure)
//   az acr build ...                                                                  (push images, see docs/INSTALLATION.md)
//   az deployment group create -g <rg> -f infra/main.bicep -p deployApps=true ...    (2nd pass: the four apps)
targetScope = 'resourceGroup'

param location string = resourceGroup().location

@minLength(3)
@maxLength(12)
@description('Short lowercase name used as prefix for every resource.')
param prefix string = 'transbrain'

@description('false = ACR, PostgreSQL, Redis, Container Apps environment only; true = also the four container apps (images must exist in ACR).')
param deployApps bool = false

@description('Tag of the images in ACR: transbrain-api, transbrain-web, transbrain-vueweb, transbrain-keycloak.')
param imageTag string = 'latest'

param postgresAdminLogin string = 'transbrainadmin'

@secure()
param postgresAdminPassword string

@secure()
@description('Bootstrap admin password of the Keycloak master realm.')
param keycloakAdminPassword string

var suffix = uniqueString(resourceGroup().id)
var acrName = take('${prefix}acr${suffix}', 50)
var postgresName = '${prefix}-pg-${suffix}'
var redisName = '${prefix}-redis-${suffix}'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-pull'
  location: location
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, pullIdentity.id, 'AcrPull')
  scope: acr
  properties: {
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresName
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '17'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: { storageSizeGB: 32 }
    authConfig: { passwordAuth: 'Enabled', activeDirectoryAuth: 'Disabled' }
  }
}

// Public endpoint, reachable from Azure services only (0.0.0.0 rule). Use a VNet + private access for stricter isolation.
resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource appDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: 'transbraindb'
  dependsOn: [ allowAzure ]
}

resource keycloakDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: 'keycloak'
  dependsOn: [ appDb ]
}

resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: redisName
  location: location
  properties: {
    sku: { name: 'Basic', family: 'C', capacity: 0 }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// App names are fixed, so the public URLs are known up front. That breaks the cycle
// keycloak (needs SPA URLs for redirect URIs) <-> SPAs/API (need the Keycloak URL).
var keycloakUrl = 'https://${prefix}-keycloak.${env.properties.defaultDomain}'
var webUrl = 'https://${prefix}-web.${env.properties.defaultDomain}'
var vuewebUrl = 'https://${prefix}-vueweb.${env.properties.defaultDomain}'
var apiUrl = 'https://${prefix}-api.internal.${env.properties.defaultDomain}'
var registryConfig = [ { server: acr.properties.loginServer, identity: pullIdentity.id } ]
var identityConfig = { type: 'UserAssigned', userAssignedIdentities: { '${pullIdentity.id}': {} } }

resource keycloak 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: '${prefix}-keycloak'
  location: location
  identity: identityConfig
  dependsOn: [ acrPull, keycloakDb ]
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      registries: registryConfig
      ingress: { external: true, targetPort: 8080 }
      secrets: [
        { name: 'db-password', value: postgresAdminPassword }
        { name: 'admin-password', value: keycloakAdminPassword }
      ]
    }
    template: {
      containers: [
        {
          name: 'keycloak'
          image: '${acr.properties.loginServer}/transbrain-keycloak:${imageTag}'
          resources: { cpu: json('1.0'), memory: '2Gi' }
          env: [
            { name: 'KC_DB', value: 'postgres' }
            { name: 'KC_DB_URL', value: 'jdbc:postgresql://${postgres.properties.fullyQualifiedDomainName}:5432/keycloak?sslmode=require' }
            { name: 'KC_DB_USERNAME', value: postgresAdminLogin }
            { name: 'KC_DB_PASSWORD', secretRef: 'db-password' }
            { name: 'KC_HOSTNAME', value: keycloakUrl }
            { name: 'KC_PROXY_HEADERS', value: 'xforwarded' }
            { name: 'KC_HTTP_ENABLED', value: 'true' }
            { name: 'KC_BOOTSTRAP_ADMIN_USERNAME', value: 'admin' }
            { name: 'KC_BOOTSTRAP_ADMIN_PASSWORD', secretRef: 'admin-password' }
            { name: 'TRANSBRAIN_WEB_URL', value: webUrl }
            { name: 'TRANSBRAIN_VUEWEB_URL', value: vuewebUrl }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: '${prefix}-api'
  location: location
  identity: identityConfig
  dependsOn: [ acrPull, appDb ]
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      registries: registryConfig
      ingress: { external: false, targetPort: 8080 }
      secrets: [
        {
          name: 'db-connection'
          value: 'Host=${postgres.properties.fullyQualifiedDomainName};Database=transbraindb;Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require'
        }
        {
          name: 'redis-connection'
          value: '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${acr.properties.loginServer}/transbrain-api:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'Database__MigrateOnStartup', value: 'true' }
            { name: 'ConnectionStrings__transbraindb', secretRef: 'db-connection' }
            { name: 'ConnectionStrings__cache', secretRef: 'redis-connection' }
            { name: 'services__keycloak__https__0', value: keycloakUrl }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: '${prefix}-web'
  location: location
  identity: identityConfig
  dependsOn: [ acrPull ]
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      registries: registryConfig
      ingress: { external: true, targetPort: 80 }
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${acr.properties.loginServer}/transbrain-web:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'API_URL', value: apiUrl }
            { name: 'OIDC_AUTHORITY', value: '${keycloakUrl}/realms/transbrain' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 2 }
    }
  }
}

resource vueweb 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: '${prefix}-vueweb'
  location: location
  identity: identityConfig
  dependsOn: [ acrPull ]
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      registries: registryConfig
      ingress: { external: true, targetPort: 80 }
    }
    template: {
      containers: [
        {
          name: 'vueweb'
          image: '${acr.properties.loginServer}/transbrain-vueweb:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'API_URL', value: apiUrl }
            { name: 'OIDC_AUTHORITY', value: '${keycloakUrl}/realms/transbrain' }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 2 }
    }
  }
}

output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output keycloakUrl string = keycloakUrl
output webUrl string = webUrl
output vuewebUrl string = vuewebUrl

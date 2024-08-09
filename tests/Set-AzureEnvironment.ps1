[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)][string] $UseAzure,
    [Parameter(Mandatory = $true)][string] $TenantId,
    [Parameter(Mandatory = $true)][string] $SubscriptionId,
    [Parameter(Mandatory = $true)][string] $ResourceGroupName,
    [Parameter(Mandatory = $true)][string] $StorageAccountName,
    [Parameter(Mandatory = $true)][string] $ServiceBusNamespaceName,
    [Parameter(Mandatory = $true)][string] $KeyVaultName
    )

$env:SPOTFLOW_USE_AZURE = $UseAzure
$env:AZURE_TENANT_ID = $TenantId
$env:AZURE_SUBSCRIPTION_ID = $SubscriptionId
$env:AZURE_RESOURCE_GROUP_NAME = $ResourceGroupName
$env:AZURE_STORAGE_ACCOUNT_NAME = $StorageAccountName
$env:AZURE_SERVICE_BUS_NAMESPACE_NAME = $ServiceBusNamespaceName
$env:AZURE_KEY_VAULT_NAME = $KeyVaultName

Write-Host "Azure environment set."

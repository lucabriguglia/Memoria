<#
.SYNOPSIS
    Applies the Memoria 1.6.0 indexing policy to an Azure Cosmos DB container.

.DESCRIPTION
    Memoria creates its container with the Cosmos DB default indexing policy, which indexes every
    path in every document -- including the serialised `data` payload that no Memoria query can
    filter on. This script replaces that policy with one that indexes only the paths the store
    actually queries, plus the composite indexes its ordered reads need.

    Safe to run more than once: applying the same policy twice is a no-op.

    Reindexing runs in the background and does not take the container offline. Queries may return
    incomplete results until the transformation reaches 100%, so apply this during a quiet period
    on a large container. Use -Wait to block until it finishes.

    This script targets an Azure account through the Azure CLI. The CLI cannot reach the Cosmos DB
    emulator; for local development, set the policy from the emulator's Data Explorer using
    1.6.0-cosmos-indexing-policy.json, or recreate the container.

.PARAMETER ResourceGroup
    Resource group holding the Cosmos DB account.

.PARAMETER Account
    Cosmos DB account name.

.PARAMETER Database
    Database name. Defaults to Memoria, matching CosmosOptions.DatabaseName.

.PARAMETER Container
    Container name. Defaults to Domain, matching CosmosOptions.ContainerName.

.PARAMETER PolicyPath
    Path to the indexing policy JSON. Defaults to 1.6.0-cosmos-indexing-policy.json next to this script.

.PARAMETER Wait
    Poll until the index transformation reports 100% before returning.

.EXAMPLE
    ./1.6.0-cosmos-apply-indexing-policy.ps1 -ResourceGroup rg-shop -Account cosmos-shop -Wait
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ResourceGroup,
    [Parameter(Mandatory = $true)][string] $Account,
    [string] $Database = 'Memoria',
    [string] $Container = 'Domain',
    [string] $PolicyPath = (Join-Path $PSScriptRoot '1.6.0-cosmos-indexing-policy.json'),
    [switch] $Wait
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'The Azure CLI (az) is required. See https://learn.microsoft.com/cli/azure/install-azure-cli.'
}

if (-not (Test-Path $PolicyPath)) {
    throw "Indexing policy not found at $PolicyPath."
}

# Fail on a malformed policy here rather than halfway through an ARM deployment.
Get-Content $PolicyPath -Raw | ConvertFrom-Json | Out-Null

Write-Host "Applying $PolicyPath to $Account/$Database/$Container ..."

az cosmosdb sql container update `
    --resource-group $ResourceGroup `
    --account-name $Account `
    --database-name $Database `
    --name $Container `
    --idx "@$PolicyPath" `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw "az cosmosdb sql container update failed with exit code $LASTEXITCODE."
}

Write-Host 'Indexing policy applied. Reindexing runs in the background.'

if (-not $Wait) {
    return
}

Write-Host 'Waiting for the index transformation to complete...'
while ($true) {
    $progress = az cosmosdb sql container show `
        --resource-group $ResourceGroup `
        --account-name $Account `
        --database-name $Database `
        --name $Container `
        --query 'resource.indexTransformationProgress' `
        --output tsv

    if ($LASTEXITCODE -ne 0) {
        throw "az cosmosdb sql container show failed with exit code $LASTEXITCODE."
    }

    # The property is absent once no transformation is in flight, which also means "done".
    if ([string]::IsNullOrWhiteSpace($progress) -or [int]$progress -ge 100) {
        Write-Host 'Reindexing complete.'
        break
    }

    Write-Host "  $progress%"
    Start-Sleep -Seconds 10
}

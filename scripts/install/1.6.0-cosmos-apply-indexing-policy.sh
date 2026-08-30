#!/usr/bin/env bash
#
# Applies the Memoria 1.6.0 indexing policy to an Azure Cosmos DB container.
#
# Memoria creates its container with the Cosmos DB default indexing policy, which indexes every
# path in every document -- including the serialised `data` payload that no Memoria query can
# filter on. This script replaces that policy with one that indexes only the paths the store
# actually queries, plus the composite indexes its ordered reads need.
#
# Safe to run more than once: applying the same policy twice is a no-op.
#
# Reindexing runs in the background and does not take the container offline. Queries may return
# incomplete results until the transformation reaches 100%, so apply this during a quiet period on
# a large container. Pass --wait to block until it finishes.
#
# The Azure CLI cannot reach the Cosmos DB emulator. For local development, set the policy from the
# emulator's Data Explorer using 1.6.0-cosmos-indexing-policy.json, or recreate the container.
#
# Usage:
#   ./1.6.0-cosmos-apply-indexing-policy.sh \
#       --resource-group rg-shop \
#       --account cosmos-shop \
#       [--database Memoria] \
#       [--container Domain] \
#       [--policy ./1.6.0-cosmos-indexing-policy.json] \
#       [--wait]

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

resource_group=""
account=""
database="Memoria"
container="Domain"
policy="${script_dir}/1.6.0-cosmos-indexing-policy.json"
wait_for_reindex=0

usage() {
    sed -n '2,26p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --resource-group) resource_group="$2"; shift 2 ;;
        --account)        account="$2";        shift 2 ;;
        --database)       database="$2";       shift 2 ;;
        --container)      container="$2";      shift 2 ;;
        --policy)         policy="$2";         shift 2 ;;
        --wait)           wait_for_reindex=1;  shift ;;
        -h|--help)        usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if [[ -z "$resource_group" || -z "$account" ]]; then
    echo "--resource-group and --account are required." >&2
    usage >&2
    exit 2
fi

if ! command -v az >/dev/null 2>&1; then
    echo "The Azure CLI (az) is required. See https://learn.microsoft.com/cli/azure/install-azure-cli." >&2
    exit 1
fi

if [[ ! -f "$policy" ]]; then
    echo "Indexing policy not found at $policy." >&2
    exit 1
fi

echo "Applying $policy to $account/$database/$container ..."

az cosmosdb sql container update \
    --resource-group "$resource_group" \
    --account-name "$account" \
    --database-name "$database" \
    --name "$container" \
    --idx "@${policy}" \
    --output none

echo "Indexing policy applied. Reindexing runs in the background."

if [[ "$wait_for_reindex" -eq 0 ]]; then
    exit 0
fi

echo "Waiting for the index transformation to complete..."
while true; do
    progress="$(az cosmosdb sql container show \
        --resource-group "$resource_group" \
        --account-name "$account" \
        --database-name "$database" \
        --name "$container" \
        --query 'resource.indexTransformationProgress' \
        --output tsv)"

    # The property is absent once no transformation is in flight, which also means "done".
    if [[ -z "$progress" || "$progress" -ge 100 ]]; then
        echo "Reindexing complete."
        break
    fi

    echo "  ${progress}%"
    sleep 10
done

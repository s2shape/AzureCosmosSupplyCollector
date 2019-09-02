#!/bin/bash

POSITIONAL=()
while [[ $# -gt 0 ]]
do
key="$1"

case $key in
    -u|--uri)
    COSMOS_URI="$2"
    shift # past argument
    shift # past value
    ;;
    -d|--db)
    COSMOS_DB="$2"
    shift # past argument
    shift # past value
    ;;
    -k|--key)
    COSMOS_KEY="$2"
    shift # past argument
    shift # past value
    ;;
    *)    # unknown option
    POSITIONAL+=("$1") # save it in an array for later
    shift # past argument
    ;;
esac
done
set -- "${POSITIONAL[@]}" # restore positional parameters

mkdir AzureCosmosSupplyCollectorTests/Properties
echo { > AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo   \"profiles\": { >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo     \"AzureCosmosSupplyCollectorTests\": { >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo       \"commandName\": \"Project\", >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo       \"environmentVariables\": { >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo         \"COSMOS_DB_DATABASE\": \"${COSMOS_DB}\", >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo         \"COSMOS_DB_KEY\": \"${COSMOS_KEY}\", >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo         \"COSMOS_DB_URI\": \"${COSMOS_URI}\" >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo       } >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo     } >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo   } >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json
echo } >> AzureCosmosSupplyCollectorTests/Properties/launchSettings.json

dotnet build
dotnet test

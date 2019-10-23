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

export COSMOS_DB_DATABASE=$COSMOS_DB
export COSMOS_DB_KEY=$COSMOS_KEY
export COSMOS_DB_URI=$COSMOS_URI

dotnet build
dotnet test

# AzureCosmosSupplyCollector
A supply collector designed to connect to Azure Cosmos DB

## Building
Run `dotnet build`

## Testing
1) Create an Azure Cosmos DB instance
![Create database](/docs/create_db.png?raw=true)
2) Open "Keys" and copy URI and Primary Key
![Keys](/docs/db_keys.png?raw=true)
3) Open "Data Explorer" and create new empty container
4) Use Data migration tool to upload files from `AzureCosmosSupplyCollectorTests/tests`
https://docs.microsoft.com/en-us/azure/cosmos-db/import-data
After importing you should see a container with 3 tables:
![Imported data](/docs/imported_data.png?raw=true)
5) Run `./run-tests.sh --uri https://your-cosmos-uri --key your-cosmos-key --db your-db-name`



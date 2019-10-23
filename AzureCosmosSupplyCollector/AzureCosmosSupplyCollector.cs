using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;
using S2.BlackSwan.SupplyCollector;
using S2.BlackSwan.SupplyCollector.Models;
using DataType = S2.BlackSwan.SupplyCollector.Models.DataType;

namespace AzureCosmosSupplyCollector {
    public class AzureCosmosSupplyCollector : SupplyCollectorBase {
        public override List<string> DataStoreTypes() {
            return (new[] {"Azure Cosmos"}).ToList();
        }

        public string BuildConnectionString(string accountEndpoint, string accountKey, string database)
        {
            return $"AccountEndpoint={accountEndpoint};AccountKey={accountKey};Database={database}";
        }

        public static void ParseConnectString(string connectString, out string endpoint, out string accountKey,
            out string database) {

            endpoint = null;
            accountKey = null;
            database = null;

            var connectStringParts = connectString.Split(";");
            foreach (var part in connectStringParts) {
                if(String.IsNullOrEmpty(part))
                    continue;

                var index = part.IndexOf("=");
                if (index > 0) {
                    var key = part.Substring(0, index);
                    var val = part.Substring(index + 1);

                    if ("AccountEndpoint".Equals(key, StringComparison.InvariantCultureIgnoreCase)) {
                        endpoint = val;
                    }
                    else if ("AccountKey".Equals(key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        accountKey = val;
                    }
                    else if ("Database".Equals(key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        database = val;
                    }
                }
            }
        }

        public override List<string> CollectSample(DataEntity dataEntity, int sampleSize) {
            var result = new List<string>();

            string endpoint;
            string key;
            string database;
            ParseConnectString(dataEntity.Container.ConnectionString, out endpoint, out key, out database);

            using (var client = new DocumentClient(new Uri(endpoint), key))
            {
                var db = (Database)client.CreateDatabaseQuery($"SELECT * from d where d.id=\"{database}\"").ToList()
                    .FirstOrDefault();

                if (db == null)
                {
                    throw new ArgumentException($"Database {database} not found!");
                }

                var rowCount = client.CreateDocumentQuery(
                    UriFactory.CreateDocumentCollectionUri(database, dataEntity.Collection.Name),
                    $"SELECT VALUE COUNT(1) FROM {dataEntity.Collection.Name}");
                var rows = (long)rowCount.ToList().FirstOrDefault();

                double pct = 0.05 + (double) sampleSize / (rows <= 0 ? sampleSize : rows);

                var r = new Random();

                var list = client.CreateDocumentQuery(
                    UriFactory.CreateDocumentCollectionUri(database, dataEntity.Collection.Name),
                    $"SELECT t.{dataEntity.Name} FROM {dataEntity.Collection.Name} t", new FeedOptions() {MaxItemCount = sampleSize});

                foreach (var item in list) {
                    var obj = JObject.Parse(item.ToString());

                    if (r.NextDouble() < pct) {
                        result.Add(obj[dataEntity.Name].ToString());
                    }

                    if (result.Count >= sampleSize)
                        break;
                }
            }

            return result;
        }

        public override List<DataCollectionMetrics> GetDataCollectionMetrics(DataContainer container) {
            var metrics = new List<DataCollectionMetrics>();

            string endpoint;
            string key;
            string database;
            ParseConnectString(container.ConnectionString, out endpoint, out key, out database);

            using (var client = new DocumentClient(new Uri(endpoint), key))
            {
                var db = (Database)client.CreateDatabaseQuery($"SELECT * from d where d.id=\"{database}\"").ToList()
                    .FirstOrDefault();

                if (db == null)
                {
                    throw new ArgumentException($"Database {database} not found!");
                }

                var docs = client.CreateDocumentCollectionQuery(db.SelfLink).ToArray();

                foreach (var doc in docs)
                {
                    var rows = client.CreateDocumentQuery(
                        UriFactory.CreateDocumentCollectionUri(database, doc.Id),
                        $"SELECT VALUE COUNT(1) FROM {doc.Id}");

                    foreach (var row in rows) {
                        var docStats = client.ReadDocumentCollectionAsync(doc.SelfLink).Result;

                        metrics.Add(new DataCollectionMetrics()
                        {
                            Name = doc.Id,
                            RowCount = (long)row,
                            TotalSpaceKB = docStats.CollectionSizeUsage
                        });

                        break;
                    }
                }
            }

            return metrics;
        }

        private void FillObjectEntities(DataContainer container, DataCollection collection, string prefix, JObject obj, List<DataEntity> entities)
        {
            var properties = obj.Properties();
            foreach (var property in properties)
            {
                if (entities.Find(x => x.Name.Equals($"{prefix}{property.Name}")) != null)
                {
                    continue;
                }

                switch (property.Value.Type)
                {
                    case JTokenType.Boolean:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.Boolean, "Boolean", container, collection));
                        break;
                    case JTokenType.String:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.String, "String", container, collection));
                        break;
                    case JTokenType.Date:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.DateTime, "Date", container, collection));
                        break;
                    case JTokenType.Float:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.Float, "Float", container, collection));
                        break;
                    case JTokenType.Integer:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.Int, "Integer", container, collection));
                        break;
                    case JTokenType.Guid:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.Guid, "Guid", container, collection));
                        break;
                    case JTokenType.Uri:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.String, "Uri", container, collection));
                        break;
                    case JTokenType.Array:
                        entities.Add(new DataEntity($"{prefix}{property.Name}", DataType.String, "Array", container, collection));

                        var arr = (JArray)property.Value;
                        for (int i = 0; i < arr.Count; i++)
                        {
                            var arrayItem = arr[i];

                            if (arrayItem.Type == JTokenType.Object)
                            {
                                FillObjectEntities(container, collection, $"{prefix}{property.Name}.", (JObject)arrayItem, entities);
                            }
                        }

                        break;
                    case JTokenType.Object:
                        FillObjectEntities(container, collection, $"{prefix}{property.Name}.", (JObject)property.Value, entities);
                        break;
                    default:
                        entities.Add(new DataEntity($"{prefix}{property.Name}",
                            DataType.Unknown, Enum.GetName(typeof(JTokenType), property.Value.Type), container, collection));
                        break;
                }
            }
        }

        public override (List<DataCollection>, List<DataEntity>) GetSchema(DataContainer container) {
            var collections = new List<DataCollection>();
            var entities = new List<DataEntity>();

            string endpoint;
            string key;
            string database;
            ParseConnectString(container.ConnectionString, out endpoint, out key, out database);

            using (var client = new DocumentClient(new Uri(endpoint), key))
            {
                var db = (Database)client.CreateDatabaseQuery($"SELECT * from d where d.id=\"{database}\"").ToList()
                    .FirstOrDefault();

                if (db == null) {
                    throw new ArgumentException($"Database {database} not found!");
                }

                var docs = client.CreateDocumentCollectionQuery(db.SelfLink).ToArray();

                foreach (var doc in docs) {
                    var collection = new DataCollection(container, doc.Id);
                    collections.Add(collection);

                    var rows = client.CreateDocumentQuery(
                        UriFactory.CreateDocumentCollectionUri(database, doc.Id),
                        $"SELECT * FROM {doc.Id}");

                    foreach (var row in rows) {
                        var obj = JObject.Parse(row.ToString());

                        FillObjectEntities(container, collection, "", obj, entities);
                        break;
                    }
                }
            }

            return (collections, entities);
        }

        public override bool TestConnection(DataContainer container) {
            string endpoint;
            string key;
            string database;
            ParseConnectString(container.ConnectionString, out endpoint, out key, out database);

            try {
                using (var client = new DocumentClient(new Uri(endpoint), key)) {
                    var db = (Database)client.CreateDatabaseQuery($"SELECT * from d where d.id=\"{database}\"").ToList()
                        .FirstOrDefault();

                    return db != null;
                }
            }
            catch (Exception) {
                return false;
            }
        }


    }
}

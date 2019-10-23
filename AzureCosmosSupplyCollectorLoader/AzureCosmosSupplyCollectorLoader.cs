using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using S2.BlackSwan.SupplyCollector.Models;
using SupplyCollectorDataLoader;
using DataType = S2.BlackSwan.SupplyCollector.Models.DataType;

namespace AzureCosmosSupplyCollectorLoader
{
    public class AzureCosmosSupplyCollectorLoader : SupplyCollectorDataLoaderBase
    {
        public override void InitializeDatabase(DataContainer dataContainer) {
            string endpoint;
            string key;
            string database;
            AzureCosmosSupplyCollector.AzureCosmosSupplyCollector.ParseConnectString(dataContainer.ConnectionString,
                out endpoint, out key, out database);

            using (var client = new DocumentClient(new Uri(endpoint), key)) {
                client.CreateDatabaseIfNotExistsAsync(new Database() {
                    Id = database
                });
            }
        }

        public override void LoadSamples(DataEntity[] dataEntities, long count) {
            string endpoint;
            string key;
            string database;
            AzureCosmosSupplyCollector.AzureCosmosSupplyCollector.ParseConnectString(dataEntities[0].Container.ConnectionString,
                out endpoint, out key, out database);

            using (var client = new DocumentClient(new Uri(endpoint), key))
            {
                var db = (Database)client.CreateDatabaseQuery($"SELECT * from d where d.id=\"{database}\"").ToList()
                    .FirstOrDefault();

                if (db == null)
                {
                    throw new ArgumentException($"Database {database} not found!");
                }

                var coll = (DocumentCollection)client.CreateDocumentCollectionQuery(db.SelfLink, $"SELECT * FROM colls c WHERE c.id = '{dataEntities[0].Collection.Name}'").AsEnumerable().FirstOrDefault();
                if (coll != null) {
                    client.DeleteDocumentCollectionAsync(coll.SelfLink).Wait();
                }

                coll = client.CreateDocumentCollectionAsync(db.SelfLink,
                    new DocumentCollection() {Id = dataEntities[0].Collection.Name}).Result.Resource;

                var r = new Random();
                long rows = 0;
                while (rows < count) {
                    if (rows % 1000 == 0) {
                        Console.Write(".");
                    }

                    dynamic doc = new ExpandoObject();
                    doc.id = Guid.NewGuid().ToString();

                    foreach (var dataEntity in dataEntities) {
                        object val;

                        switch (dataEntity.DataType)
                        {
                            case DataType.String:
                                val = new Guid().ToString();
                                break;
                            case DataType.Int:
                                val = r.Next();
                                break;
                            case DataType.Double:
                                val = r.NextDouble();
                                break;
                            case DataType.Boolean:
                                val = r.Next(100) > 50;
                                break;
                            case DataType.DateTime:
                                val = DateTimeOffset
                                    .FromUnixTimeMilliseconds(
                                        DateTimeOffset.Now.ToUnixTimeMilliseconds() + r.Next()).DateTime;
                                break;
                            default:
                                val = r.Next();
                                break;
                        }

                        ((IDictionary<string, object>) doc)[dataEntity.Name] = val;
                    }
                    
                    client.CreateDocumentAsync(coll.DocumentsLink, doc);

                    rows++;
                }
                Console.WriteLine();
            }
        }

        private void LoadFile(DocumentClient client, Database db, string fileName) {
            var coll = (DocumentCollection)client.CreateDocumentCollectionQuery(db.SelfLink, $"SELECT * FROM colls c WHERE c.id = '{dataEntities[0].Collection.Name}'").AsEnumerable().FirstOrDefault();
            if (coll != null)
            {
                client.DeleteDocumentCollectionAsync(coll.SelfLink).Wait();
            }

            coll = client.CreateDocumentCollectionAsync(db.SelfLink,
                new DocumentCollection() { Id = fileName }).Result.Resource;

            using (var reader = new StreamReader($"tests/{fileName}")) {
                var header = reader.ReadLine();
                var columnsNames = header.Split(",");

                while (!reader.EndOfStream) {
                    var line = reader.ReadLine();
                    if (String.IsNullOrEmpty(line))
                        continue;

                    var cells = line.Split(",");

                    dynamic doc = new ExpandoObject();
                    doc.id = Guid.NewGuid().ToString();

                    for (int i = 0; i < columnsNames.Length && i < cells.Length; i++) {
                        ((IDictionary<string, object>) doc)[columnsNames[i]] = cells[i];
                    }

                    client.CreateDocumentAsync(coll.DocumentsLink, doc);
                }
            }
        }

        public override void LoadUnitTestData(DataContainer dataContainer) {
            string endpoint;
            string key;
            string database;
            AzureCosmosSupplyCollector.AzureCosmosSupplyCollector.ParseConnectString(dataContainer.ConnectionString,
                out endpoint, out key, out database);

            using (var client = new DocumentClient(new Uri(endpoint), key))
            {
                var db = (Database)client.CreateDatabaseQuery($"SELECT * from d where d.id=\"{database}\"").ToList()
                    .FirstOrDefault();

                if (db == null)
                {
                    throw new ArgumentException($"Database {database} not found!");
                }

                LoadFile(client, db, "CONTACTS_AUDIT.CSV");
                LoadFile(client, db, "EMAILS.CSV");
                LoadFile(client, db, "LEADS.CSV");
            }
        }
    }
}

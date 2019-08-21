using System;
using S2.BlackSwan.SupplyCollector.Models;
using Xunit;

namespace AzureCosmosSupplyCollectorTests
{
    public class AzureCosmosSupplyCollectorTests : IClassFixture<LaunchSettingsFixture>
    {
        private readonly AzureCosmosSupplyCollector.AzureCosmosSupplyCollector _instance;
        public readonly DataContainer _container;
        private LaunchSettingsFixture _fixture;

        public AzureCosmosSupplyCollectorTests(LaunchSettingsFixture fixture)
        {
            _fixture = fixture;
            _instance = new AzureCosmosSupplyCollector.AzureCosmosSupplyCollector();
            _container = new DataContainer()
            {
                ConnectionString = _instance.BuildConnectionString(
                    Environment.GetEnvironmentVariable("COSMOS_DB_URI"),
                    Environment.GetEnvironmentVariable("COSMOS_DB_KEY"),
                    Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE")
                )
            };
        }

        [Fact]
        public void DataStoreTypesTest()
        {
            var result = _instance.DataStoreTypes();
            Assert.Contains("Azure Cosmos", result);
        }

        [Fact]
        public void TestConnectionTest()
        {
            var result = _instance.TestConnection(_container);
            Assert.True(result);
        }

        [Fact]
        public void GetDataCollectionMetricsTest()
        {
            var metrics = new DataCollectionMetrics[] {
                new DataCollectionMetrics()
                    {Name = "EMAILS", RowCount = 200, TotalSpaceKB = 544},
                new DataCollectionMetrics()
                    {Name = "LEADS", RowCount = 200, TotalSpaceKB = 672},
                new DataCollectionMetrics()
                    {Name = "CONTACTS", RowCount = 200, TotalSpaceKB = 640},
            };

            var result = _instance.GetDataCollectionMetrics(_container);
            Assert.Equal(metrics.Length, result.Count);

            foreach (var metric in metrics)
            {
                var resultMetric = result.Find(x => x.Name.Equals(metric.Name));
                Assert.NotNull(resultMetric);

                Assert.Equal(metric.RowCount, resultMetric.RowCount);
                Assert.Equal(metric.TotalSpaceKB, resultMetric.TotalSpaceKB);
            }
        }

        [Fact]
        public void GetTableNamesTest()
        {
            var (tables, elements) = _instance.GetSchema(_container);
            Assert.Equal(3, tables.Count);
            Assert.Equal(103, elements.Count);

            var tableNames = new string[] { "LEADS", "EMAILS", "CONTACTS" };
            foreach (var tableName in tableNames)
            {
                var table = tables.Find(x => x.Name.Equals(tableName));
                Assert.NotNull(table);
            }
        }

        [Fact]
        public void CollectSampleTest()
        {
            var entity = new DataEntity("FROM_ADDR", DataType.String, "string", _container,
                new DataCollection(_container, "EMAILS"));

            var samples = _instance.CollectSample(entity, 2);
            Assert.Equal(2, samples.Count);
            Assert.Contains("will@example.com", samples);
        }


    }
}

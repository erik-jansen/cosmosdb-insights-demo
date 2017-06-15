using System;
using System.Configuration;
using System.Text;
using Microsoft.Azure;
using Microsoft.Azure.Insights;
using Microsoft.Azure.Insights.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;
using System.Linq;

namespace CosmosMetricsApp
{
    class Program
    {
        private static readonly string _subscriptionId = ConfigurationManager.AppSettings["AzureSubscriptionId"];
        private static readonly string _tenantId = ConfigurationManager.AppSettings["AzureADTenantId"];
        private static readonly string _applicationId = ConfigurationManager.AppSettings["AzureADApplicationId"];
        private static readonly string _applicationPwd = ConfigurationManager.AppSettings["AzureADApplicationPassword"];
        private static readonly string _cosmosDbResourceGroupName = ConfigurationManager.AppSettings["CosmosDbResourceGroupName"];
        private static readonly string _cosmosDbAccountName = ConfigurationManager.AppSettings["CosmosDbAccountName"];
        private static readonly string _cosmosDbAuthorizationKey = ConfigurationManager.AppSettings["CosmosDbAuthorizationKey"];
        private static readonly string _collectionResourceBaseUri = "/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.DocumentDB/databaseAccounts/{2}/databases/{3}/collections/{4}/";

        static void Main(string[] args)
        {
            var collections = GetCollections();
            var metrics = GetMetrics(collections);

            foreach (var metric in metrics)
            {
                Console.WriteLine($"Displaying collection {metric.CollectionId} in database {metric.DatabaseId}");

                foreach (var measurement in metric.Measurements)
                {
                    Console.WriteLine($"{measurement.Key.ToLocalTime()} - {measurement.Value.Total} - {measurement.Value.Throttles}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("Press any key to exit!");
            Console.ReadLine();
        }

        /// <summary>
        /// Main method for orchestrating the work
        /// - gets the token
        /// - gets the collections
        /// - gets the metrics off the collections
        /// </summary>
        /// <returns>A usable class with the metrics</returns>
        private static List<CollectionMeasurement> GetMetrics(List<Collection> cosmosCollections)
        {
            var token = GetAccessToken();
            var creds = new TokenCloudCredentials(_subscriptionId, token);
            var collectionMeasurements = new List<CollectionMeasurement>();

            string docDbFilterString = "(name.value eq 'Total Requests' or name.value eq 'Throttled Requests')";

            foreach (var collection in cosmosCollections)
            {
                var metrics = new Dictionary<DateTime, Measurement>();

                string collectionResourceUri = string.Format(_collectionResourceBaseUri, _subscriptionId, _cosmosDbResourceGroupName, _cosmosDbAccountName, collection.DatabaseResourceId, collection.CollectionResourceId);
                var resourceMetrics = GetResourceMetrics(creds, collectionResourceUri, docDbFilterString, TimeSpan.FromHours(1), "PT5M");

                var collectionMeasurement = new CollectionMeasurement()
                {
                    DatabaseId = collection.DatabaseId,
                    CollectionId = collection.CollectionId,
                    Measurements = resourceMetrics
                };
                collectionMeasurements.Add(collectionMeasurement);
            }

            return collectionMeasurements;
        }

        /// <summary>
        /// Uses the DocumentClient to fetch all the Cosmos Collections in 
        /// all the Cosmos Databases in the Cosmos Subscription
        /// </summary>
        /// <returns></returns>
        private static List<Collection> GetCollections()
        {
            List<Collection> retval = new List<Collection>();

            using (var client = new DocumentClient(new Uri(string.Format("https://{0}.documents.azure.com:443/", _cosmosDbAccountName)), _cosmosDbAuthorizationKey))
            {
                var list = client.CreateDatabaseQuery().AsEnumerable().ToList();
                foreach (var db in list)
                {
                    var collectionList = client.CreateDocumentCollectionQuery(db.SelfLink).ToList();
                    foreach (var collection in collectionList)
                    {
                        var collectionItem = new Collection()
                        {
                            DatabaseId = db.Id,
                            DatabaseResourceId = db.ResourceId,
                            CollectionId = collection.Id,
                            CollectionResourceId = collection.ResourceId
                        };
                        retval.Add(collectionItem);
                    }
                }
            }

            return retval;
        }

        /// <summary>
        /// Goes off to Azure AD to fetch a token for the Service Principal
        /// If you want to test quickly fetch a token from your browser session and chuck in into the config
        /// </summary>
        /// <returns></returns>
        private static string GetAccessToken()
        {
            string token = ConfigurationManager.AppSettings["Token"];

            if (string.IsNullOrEmpty(token))
            {
                var authenticationContext = new AuthenticationContext(string.Format("https://login.windows.net/{0}", _tenantId));
                var credential = new ClientCredential(clientId: _applicationId, clientSecret: _applicationPwd);
                var result = authenticationContext.AcquireToken(resource: "https://management.core.windows.net/", clientCredential: credential);

                if (result == null)
                {
                    throw new InvalidOperationException("Failed to obtain the JWT token");
                }

                token = result.AccessToken;
            }
            return token;
        }

        /// <summary>
        /// A converter that take the very generic metricList and converts that into our custom class to make 
        /// reading it out easier
        /// </summary>
        /// <returns></returns>
        private static Dictionary<DateTime, Measurement> ConvertMetricValues(MetricListResponse metricList)
        {
            var measurement = new Dictionary<DateTime, Measurement>();

            foreach (Metric m in metricList.MetricCollection.Value)
            {
                foreach (MetricValue metricValue in m.MetricValues)
                {
                    if (!measurement.ContainsKey(metricValue.Timestamp))
                    {
                        measurement.Add(metricValue.Timestamp, new Measurement() {});
                    }

                    switch (m.Name.Value)
                    {
                        case "Total Requests":
                            measurement[metricValue.Timestamp].Total = metricValue.Total;
                            break;
                        case "Throttled Requests":
                            measurement[metricValue.Timestamp].Throttles = metricValue.Total;
                            break;
                    }
                }
            }

            return measurement;
        }

        /// <summary>
        /// Use the Insights Rest API to fetch the metrics and values 
        /// based on the filter, the period and the duration params
        /// </summary>
        /// <returns></returns>
        private static Dictionary<DateTime, Measurement> GetResourceMetrics(TokenCloudCredentials credentials, string resourceUri, string filter, TimeSpan period, string duration)
        {
            var dateTimeFormat = "yyy-MM-ddTHH:mmZ";

            string start = DateTime.UtcNow.Subtract(period).ToString(dateTimeFormat);
            string end = DateTime.UtcNow.ToString(dateTimeFormat);

            // TODO: Make this more robust.
            StringBuilder sb = new StringBuilder(filter);

            if (!string.IsNullOrEmpty(filter))
            {
                sb.Append(" and ");
            }
            sb.AppendFormat("startTime eq {0} and endTime eq {1}", start, end);
            sb.AppendFormat(" and timeGrain eq duration'{0}'", duration);

            using (var client = new InsightsClient(credentials))
            {
                return ConvertMetricValues(client.MetricOperations.GetMetrics(resourceUri, sb.ToString()));
            }
        }
    }
}

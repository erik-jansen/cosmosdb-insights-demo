# cosmosdb-insights-demo

*This is a sample application only. Use at your own risk.*

This program fetches the CosmosDB collections from an account, uses a Service Principal to fetch the Total Requests and the Throttled requests and displays them.

To fill out the Application params, either change the App.config or add a file called app.secrets.config and fill out the parameters there.
| Param | Value |
| --- | --- |
| AzureSubscriptionId | The subscription ID in which your CosmosDB resides |
| AzureADTenantId | The Azure Active Directory Tenant ID in which the Service Principal sits |
| AzureADApplicationId | The ApplicationID or ClientID of the Service Principal |
| AzureADApplicationPassword | The Secret of the key from the Service Principal |
| CosmosDbResourceGroupName | The Azure Resource Group in which the CosmosDB resides |
| CosmosDbAccountName | The CosmosDB Account name |
| CosmosDbAuthorizationKey | The CosmosDB Primary Read Key |

Based on the great work by Michael Collier on getting generic Metrics through the Microsoft Azure Insights API. See: https://github.com/mcollier/microsoft-azure-insights-demo/
Creating the Service Principal is discussed here in this post by Yossi Dassan: https://yossidahan.wordpress.com/2015/02/13/reading-metric-data-from-azure-using-the-azure-insights-library/
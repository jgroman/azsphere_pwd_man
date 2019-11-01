using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using Microsoft.Extensions.Configuration;

namespace AzurePasswordManager.Pages
{
    public class ConfigModel : PageModel
    {
        public string Message { get; set; }

        private readonly IConfiguration _config;

        // Note: In KeyVault key names replace ":" with "--"
        private readonly static string configKeyVaultNameKey = "ConfigKeyVaultName";
        private readonly static string iotHubServiceConnStringKey = "ConnectionStrings:IotHubServicez";

        private string iotHubServiceConnString;

        public ConfigModel(IConfiguration config) {
            _config = config;
        }

        public async Task OnGetAsync() 
        {
            var value = _config.GetValue<String>(iotHubServiceConnStringKey, "unset");
            System.Diagnostics.Debug.WriteLine($"***** Get key '{iotHubServiceConnStringKey}', value: {value}");

            if (value == "unset") {
                // IoT Hub Service Connection string Secret doesn't exist in KeyVault

                System.Diagnostics.Debug.WriteLine($"***** Creating key '{iotHubServiceConnStringKey}' in KeyVault");

                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

                KeyVaultClient keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(
                        azureServiceTokenProvider.KeyVaultTokenCallback));

                // Get KeyVault hostname from Configuration (appsettings.json)
                var keyVaultHost = _config.GetValue<String>(configKeyVaultNameKey);

                // Create Secret in KeyVault
                var bundle = await keyVaultClient.SetSecretAsync(
                    $"https://{keyVaultHost}.vault.azure.net/", 
                    iotHubServiceConnStringKey.Replace(":","--"), 
                    "")
                    .ConfigureAwait(false);

            }

            /*
            var secret = await keyVaultClient.GetSecretAsync($"https://{keyVaultHost}.vault.azure.net/secrets/AppSecret")
                    .ConfigureAwait(false);
              */

        }

    }
}
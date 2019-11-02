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
        [BindProperty]
        public string IotHubServiceConnString { get; set; }

        [BindProperty]
        public string AzureSphereDeviceConnString { get; set; }

        [BindProperty]
        public string InputId { get; set; }

        private readonly IConfiguration _config;

        private readonly string iotHubServiceConnStringKey;
        private readonly string azureSphereDeviceConnStringKey;

        private static AzureServiceTokenProvider azureServiceTokenProvider = 
            new AzureServiceTokenProvider();
        private static KeyVaultClient keyVaultClient = new KeyVaultClient(
            new KeyVaultClient.AuthenticationCallback(
                azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly string KeyVaultHostName;

        private string oldIotHubServiceConnString;
        private string oldAzureSphereDeviceConnString;

        private SecretBundle bundle;

        public ConfigModel(IConfiguration config) 
        {
            _config = config;

            iotHubServiceConnStringKey = _config.GetValue<String>("ConnectionStringKeys:iotHubService");
            azureSphereDeviceConnStringKey = _config.GetValue<String>("ConnectionStringKeys:azureSphereDevice");

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
        }

        public async Task OnGetAsync() 
        {
            try {
                // Try to read 'iotHubServiceConnString' from KeyVault
                bundle = await keyVaultClient.GetSecretAsync($"https://{KeyVaultHostName}.vault.azure.net/", iotHubServiceConnStringKey)
                        .ConfigureAwait(false);

                IotHubServiceConnString = bundle.Value;
                System.Diagnostics.Debug.WriteLine($"***** Get key '{iotHubServiceConnStringKey}', value: {IotHubServiceConnString}");
            }
            catch (KeyVaultErrorException kvex) {
                if (kvex.Body.Error.Code.Equals("SecretNotFound")) {
                    // IoT Hub Service Connection string Secret doesn't exist in KeyVault
                    System.Diagnostics.Debug.WriteLine($"***** Creating key '{iotHubServiceConnStringKey}' in KeyVault");

                    // Create Secret in KeyVault
                    // Note: In KeyVault key names replace ":" with "--"
                    bundle = await keyVaultClient.SetSecretAsync(
                        $"https://{KeyVaultHostName}.vault.azure.net/",
                        iotHubServiceConnStringKey,
                        "",
                        null,
                        "String");

                    IotHubServiceConnString = "";
                }
            }

            try {
                // Try to read 'azureSphereDeviceConnString' from KeyVault
                bundle = await keyVaultClient.GetSecretAsync($"https://{KeyVaultHostName}.vault.azure.net/", azureSphereDeviceConnStringKey)
                        .ConfigureAwait(false);

                AzureSphereDeviceConnString = bundle.Value;
                System.Diagnostics.Debug.WriteLine($"***** Get key '{azureSphereDeviceConnStringKey}', value: {AzureSphereDeviceConnString}");
            }
            catch (KeyVaultErrorException kvex) {
                if (kvex.Body.Error.Code.Equals("SecretNotFound")) {
                    // IoT Hub Service Connection string Secret doesn't exist in KeyVault
                    System.Diagnostics.Debug.WriteLine($"***** Creating key '{azureSphereDeviceConnStringKey}' in KeyVault");

                    // Create Secret in KeyVault
                    bundle = await keyVaultClient.SetSecretAsync(
                        $"https://{KeyVaultHostName}.vault.azure.net/",
                        azureSphereDeviceConnStringKey,
                        "",
                        null,
                        "String");

                    AzureSphereDeviceConnString = "";
                }
            }

            oldIotHubServiceConnString = IotHubServiceConnString;
            oldAzureSphereDeviceConnString = AzureSphereDeviceConnString;
        }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            if (InputId == "iothub") {
                // IoT Hub Service ConnString was submitted
                if (IotHubServiceConnString != oldIotHubServiceConnString) {
                    // Update value in Key Vault
                    var bundle = await keyVaultClient.SetSecretAsync(
                        $"https://{KeyVaultHostName}.vault.azure.net/",
                        iotHubServiceConnStringKey,
                        IotHubServiceConnString,
                        null,
                        "String");
                }
            }
            else if (InputId == "device") {
                // Azure Sphere Device ConnString was submitted
                if (AzureSphereDeviceConnString != oldAzureSphereDeviceConnString) {
                    // Update value in Key Vault
                    var bundle = await keyVaultClient.SetSecretAsync(
                        $"https://{KeyVaultHostName}.vault.azure.net/",
                        oldAzureSphereDeviceConnString,
                        AzureSphereDeviceConnString,
                        null,
                        "String");
                }
            }

            return RedirectToPage("/Config");
        }
    }
}

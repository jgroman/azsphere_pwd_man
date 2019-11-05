using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Configuration;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

namespace AzurePasswordManager.Data {

    public class ConnectionString {

        [Required]
        [Display(Name = "IoT Hub Service Connection String")]
        public string IotHubService { get; set; }

        [Required]
        [Display(Name = "Azure Sphere Device Name")]
        public string AzureSphereDevice { get; set; }

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient = new KeyVaultClient(
            new KeyVaultClient.AuthenticationCallback(
                azureServiceTokenProvider.KeyVaultTokenCallback));

        public async Task ReadFromKeyVault(IConfiguration config) {

            string keyVaultHostName = config.GetValue<String>("KeyVaultName");
            string keyVaultBaseUrl = $"https://{keyVaultHostName}.vault.azure.net/";

            string iotHubServiceConnStringKey = config.GetValue<String>("ConnectionStringKeys:iotHubService");
            string azureSphereDeviceConnStringKey = config.GetValue<String>("ConnectionStringKeys:azureSphereDevice");

            SecretBundle bundle;

            try {
                // Try to read IoT Hub Service connection string from KeyVault
                bundle = await keyVaultClient.GetSecretAsync(keyVaultBaseUrl, iotHubServiceConnStringKey)
                        .ConfigureAwait(false);

                IotHubService = bundle.Value;
            }
            catch (KeyVaultErrorException kvex) {
                if (kvex.Body.Error.Code.Equals("SecretNotFound")) {
                    // IoT Hub Service Connection string Secret doesn't exist in KeyVault

                    // Create Secret in KeyVault
                    bundle = await keyVaultClient.SetSecretAsync(
                        keyVaultBaseUrl, iotHubServiceConnStringKey, "");

                    IotHubService = "";
                }
            }

            try {
                // Try to read Azure Sphere Device connection string from KeyVault
                bundle = await keyVaultClient.GetSecretAsync(keyVaultBaseUrl, azureSphereDeviceConnStringKey)
                        .ConfigureAwait(false);

                AzureSphereDevice = bundle.Value;
            }
            catch (KeyVaultErrorException kvex) {
                if (kvex.Body.Error.Code.Equals("SecretNotFound")) {
                    // Azure Sphere Device Name Secret doesn't exist in KeyVault
                    string defaultName = config.GetValue<String>("AzureSphereDevice:defaultName");

                    // Create Secret in KeyVault
                    bundle = await keyVaultClient.SetSecretAsync(
                        keyVaultBaseUrl, azureSphereDeviceConnStringKey, defaultName);

                    AzureSphereDevice = defaultName;
                }
            }
        }

        public async Task WriteToKeyVault(IConfiguration config, ConnectionString previousState) {

            string keyVaultHostName = config.GetValue<String>("KeyVaultName");
            string keyVaultBaseUrl = $"https://{keyVaultHostName}.vault.azure.net/";

            string iotHubServiceConnStringKey = config.GetValue<String>("ConnectionStringKeys:iotHubService");
            string azureSphereDeviceConnStringKey = config.GetValue<String>("ConnectionStringKeys:azureSphereDevice");

            if (previousState == null) {
                // We'll just create new keys
                if (!IotHubService.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        keyVaultBaseUrl, iotHubServiceConnStringKey, IotHubService);
                }

                if (!AzureSphereDevice.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        keyVaultBaseUrl, azureSphereDeviceConnStringKey, AzureSphereDevice);
                }
            }
            else {
                // We'll update changed secrets
                if ((IotHubService ?? "") != (previousState.IotHubService ?? "")) {
                    await keyVaultClient.SetSecretAsync(
                        keyVaultBaseUrl, iotHubServiceConnStringKey,
                        (IotHubService ?? ""));
                }

                if ((AzureSphereDevice ?? "") != (previousState.AzureSphereDevice ?? "")) {
                    await keyVaultClient.SetSecretAsync(
                        keyVaultBaseUrl, azureSphereDeviceConnStringKey,
                        (AzureSphereDevice ?? ""));
                }
            }
        }

    }
}

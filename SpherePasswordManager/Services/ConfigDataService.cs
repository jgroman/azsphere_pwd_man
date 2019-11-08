using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using SpherePasswordManager.Models;
using System.Net.Http;

namespace SpherePasswordManager.Services
{

    public interface IConfigDataService
    {
        Task<ConfigData> ReadAsync();
        Task WriteAsync(ConfigData configData);
    }

    public class ConfigDataService : IConfigDataService
    {

        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient =
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly string KeyVaultHostName, KeyVaultBaseUrl, ConfigKeyPrefix;

        private readonly string IotHubServiceKey, AzureSphereDeviceKey;

        public ConfigDataService(IConfiguration config, IMemoryCache cache)
        {
            _config = config;
            _cache = cache;

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";

            ConfigKeyPrefix = _config.GetValue<String>("ConfigKeys:prefix");

            IotHubServiceKey = config.GetValue<String>("ConfigKeys:iotHubServiceConnStr");
            AzureSphereDeviceKey = config.GetValue<String>("ConfigKeys:azureSphereDeviceName");
        }

        public async Task<ConfigData> ReadAsync()
        {

            if (_cache.Get("ConfigData") == null)
            {


                SecretBundle bundle;

                string iotHubService = "";
                string azureSphereDevice = "";

                try
                {
                    // Try to read IoT Hub Service connection string from KeyVault
                    bundle = await keyVaultClient.GetSecretAsync(KeyVaultBaseUrl, ConfigKeyPrefix + IotHubServiceKey)
                            .ConfigureAwait(false);

                    iotHubService = bundle.Value;
                }
                catch (KeyVaultErrorException kvex)
                {
                    if (kvex.Body.Error.Code.Equals("SecretNotFound"))
                    {
                        // IoT Hub Service Connection string Secret doesn't exist in KeyVault
                        iotHubService = "";
                    }
                }

                try
                {
                    // Try to read Azure Sphere Device Name from KeyVault
                    bundle = await keyVaultClient.GetSecretAsync(KeyVaultBaseUrl, ConfigKeyPrefix + AzureSphereDeviceKey)
                            .ConfigureAwait(false);

                    azureSphereDevice = bundle.Value;
                }
                catch (KeyVaultErrorException kvex)
                {
                    if (kvex.Body.Error.Code.Equals("SecretNotFound"))
                    {
                        // IoT Hub Service Connection string Secret doesn't exist in KeyVault
                        azureSphereDevice = "";
                    }
                }

                ConfigData configData = new ConfigData()
                {
                    IotHubService = iotHubService,
                    AzureSphereDevice = azureSphereDevice
                };

                _cache.Set("ConfigData", configData);
            }
            return _cache.Get<ConfigData>("ConfigData");
        }

        public async Task WriteAsync(ConfigData configData)
        {
            bool updateCache = false;
            ConfigData oldConfigData = _cache.Get<ConfigData>("ConfigData");

            if (!string.IsNullOrEmpty(configData.IotHubService) &&
                (configData.IotHubService != oldConfigData.IotHubService))
            {
                // Update IoT Hub Service Connection String in KeyVault
                updateCache = true;
                try
                {
                    await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, ConfigKeyPrefix + IotHubServiceKey, configData.IotHubService);
                }
                catch (KeyVaultErrorException)
                {
                }
            }

            if (!string.IsNullOrEmpty(configData.AzureSphereDevice) &&
                (configData.AzureSphereDevice != oldConfigData.AzureSphereDevice))
            {
                // Update Azure Sphere Device Name in KeyVault
                updateCache = true;
                try
                {
                    await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, ConfigKeyPrefix + AzureSphereDeviceKey, configData.AzureSphereDevice);
                }
                catch (KeyVaultErrorException)
                {
                }
            }

            if (updateCache)
            {
                _cache.Set("ConfigData", configData);
            }
        }

    }


}

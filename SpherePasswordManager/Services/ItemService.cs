using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

using Microsoft.Rest;

using SpherePasswordManager.Models;

namespace SpherePasswordManager.Services
{

    public interface IItemService
    {
        Task<List<Item>> ReadAllAsync();
        Task<bool> CheckItemNameExistsAsync(string itemName);
        Task CreateAsync(Item item);
        Task<Item> ReadAsync(int id);
        Task UpdateAsync(Item modifiedItem);
        Task DeleteAsync(int id);
        Task<string> SendAsync(int id);
    }

    public class ItemService : IItemService
    {

        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly IConfigDataService _configDataService;

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient =
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly string KeyVaultHostName, KeyVaultBaseUrl, ConfigKeyPrefix;

        private readonly static int MaxGetSecretsResults = 25;


        public ItemService(IConfiguration config, IMemoryCache cache, IConfigDataService configDataService)
        {
            _config = config;
            _cache = cache;
            _configDataService = configDataService;

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";

            ConfigKeyPrefix = _config.GetValue<String>("ConfigKeys:prefix");
        }

        public async Task<List<Item>> ReadAllAsync()
        {

            if (_cache.Get("ItemList") == null)
            {

                int splitMark;
                string secretName;
                List<Item> items = new List<Item>();
                Item item;
                int itemId = 1;

                // Load secrets from KeyVault
                try
                {
                    var secrets = await keyVaultClient.GetSecretsAsync(
                        KeyVaultBaseUrl, MaxGetSecretsResults);

                    // Get items and store them in List
                    foreach (SecretItem secret in secrets)
                    {
                        // Secret name follows after the last forward slash in secret.Id
                        splitMark = secret.Id.LastIndexOf('/');
                        secretName = secret.Id.Substring(splitMark + 1);

                        // Skip prefixed configuration keys 
                        if (!secretName.StartsWith(ConfigKeyPrefix))
                        {

                            item = new Item()
                            {
                                Id = itemId,
                                Name = secretName
                            };

                            items.Add(item);
                            itemId++;
                        }
                    }

                }
                catch (HttpRequestException)
                {
                }

                // Order list by name ascending
                List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

                _cache.Set("ItemList", sortedItems);
            }
            return _cache.Get<List<Item>>("ItemList");
        }

        public async Task<bool> CheckItemNameExistsAsync(string itemName)
        {
            var items = await ReadAllAsync();
            var item = items.SingleOrDefault(c => c.Name == itemName);
            if (item == null)
            {
                return false;
            }

            return true;
        }

        public async Task CreateAsync(Item newItem)
        {

            // Ignore items with no name
            if (string.IsNullOrEmpty(newItem.Name))
            {
                return;
            }

            // Get cached list of all items
            var items = await ReadAllAsync();

            // Compute Id for new item
            newItem.Id = items.Max(c => c.Id) + 1;

            // Add item to list
            items.Add(newItem);

            // Store item in KeyVault
            string itemJson = JsonSerializer.Serialize(newItem);

            try
            {
                await keyVaultClient.SetSecretAsync(
                    KeyVaultBaseUrl, newItem.Name, itemJson);
            }
            catch (ValidationException)
            {
            }

            // Order item list by name ascending
            List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

            // Update item cache
            _cache.Set("ItemList", sortedItems);
        }

        public async Task DeleteAsync(int id)
        {
            // Get list of all items
            var items = await ReadAllAsync();

            // Get item to delete
            var deletedItem = items.Single(c => c.Id == id);

            // Remove item from list
            items.Remove(deletedItem);

            // Update liad cache, no reorder necessary
            _cache.Set("ItemList", items);

            // Remove item from KeyVault
            await keyVaultClient.DeleteSecretAsync(
                KeyVaultBaseUrl, deletedItem.Name);
        }

        public async Task<Item> ReadAsync(int id)
        {

            // Get cached list of all items
            List<Item> items = await ReadAllAsync();

            // Check if item content was already loaded from KeyVault
            // Since item.Password is mandatory, we'll check if password is present
            Item currentItem = items.Single(c => c.Id == id);

            if (string.IsNullOrEmpty(currentItem.Password))
            {

                string itemJson;

                // Load full item content JSON from KeyVault
                try
                {
                    SecretBundle bundle = await keyVaultClient.GetSecretAsync(
                        KeyVaultBaseUrl, currentItem.Name)
                        .ConfigureAwait(false);

                    itemJson = bundle.Value;
                }
                catch (KeyVaultErrorException)
                {
                    itemJson = "{}";
                }

                // Get item from JSON
                try
                {
                    var fullItem = JsonSerializer.Deserialize<Item>(itemJson);

                    // Update item in cached list, no name change
                    fullItem.Id = currentItem.Id;
                    await UpdateAsync(fullItem);
                }
                catch (JsonException)
                {
                }

                // Get full item data from cached list
                currentItem = items.Single(c => c.Id == id);
            }

            return currentItem;
        }

        public async Task UpdateAsync(Item modifiedItem)
        {

            if (string.IsNullOrEmpty(modifiedItem.Name))
            {
                return;
            }

            // Get cached list of all items
            var items = await ReadAllAsync();

            // Get current item state from the list
            var item = items.Single(c => c.Id == modifiedItem.Id);

            // If item name changed, recreate item including KeyVault secrets
            if (item.Name != modifiedItem.Name)
            {
                // Delete current item
                await DeleteAsync(item.Id);

                // Create new item
                await CreateAsync(modifiedItem);
            }
            else
            {
                // Update all item properties except for Name and Id
                item.Username = modifiedItem.Username;
                item.Password = modifiedItem.Password;
                item.Uri = modifiedItem.Uri;

                // Update cached list
                _cache.Set("ItemList", items);

                // Update item in KeyVault
                string itemJson = JsonSerializer.Serialize(item);
                await keyVaultClient.SetSecretAsync(
                    KeyVaultBaseUrl, item.Name, itemJson);
            }
        }

        public async Task<string> SendAsync(int id)
        {

            // Get full item content
            Item item = await ReadAsync(id);

            // Obtain IoT Hub connection string and Azure Sphere device name
            ConfigData configData = await _configDataService.ReadAsync();

            ServiceClient serviceClient;

            // Prepare device method call via Iot Hub
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(configData.IotHubService);
            }
            catch (FormatException fex)
            {
                if (fex.Message.Equals("Malformed Token"))
                {
                    // Invalid Iot Hub Service Connection String
                    return ("ERROR: Invalid Iot Hub Service Connection String");
                }
                return ("ERROR: Iot Hub Message Format Exception");
            }
            catch (ArgumentException)
            {
                return ("ERROR: Invalid Iot Hub Service Connection String");
            }

            string methodName = _config.GetValue<string>("AzureSphereDevice:directMethodName");
            int methodTimeout = _config.GetValue<int>("AzureSphereDevice:directMethodCallTimeout");

            var methodInvocation = new CloudToDeviceMethod(methodName)
            {
                ResponseTimeout = TimeSpan.FromSeconds(methodTimeout)
            };

            methodInvocation.SetPayloadJson(JsonSerializer.Serialize(item));

            // Invoke the direct method asynchronously and get the response from IoT device.
            CloudToDeviceMethodResult deviceResult;
            try
            {
                deviceResult = await serviceClient.InvokeDeviceMethodAsync(configData.AzureSphereDevice, methodInvocation);
                System.Diagnostics.Debug.WriteLine($"***** Response payload '{deviceResult.GetPayloadAsJson()}'");
            }
            catch (DeviceNotFoundException dnfex)
            {
                //System.Diagnostics.Debug.WriteLine($"***** EX: '{dnfex}'");

                if (dnfex.Message.Contains(":404001,"))
                {
                    // errorCode 404001: Device not registered or incorrect name
                    return ("ERROR: Device not registered in IoT Hub");
                }
                else if (dnfex.Message.Contains(":404103,"))
                {
                    // errorCode 404103: Timeout
                    return ("ERROR: Timeout connecting device");
                }

                return ("ERROR: Device not found");
            }

            // Azure Sphere currently returns just result property
            string result;
            try
            {
                JsonDocument document = JsonDocument.Parse(deviceResult.GetPayloadAsJson());
                result = document.RootElement.GetProperty("result").GetString();
            }
            catch (JsonException)
            {
                result = "ERROR: Response parsing failed";
            }

            return result;
        }

    }

}

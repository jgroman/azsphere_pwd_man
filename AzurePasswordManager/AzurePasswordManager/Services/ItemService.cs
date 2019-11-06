using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using AzurePasswordManager.Models;
using Microsoft.Rest.Azure;

using Newtonsoft.Json;

namespace AzurePasswordManager.Services {

    public interface IItemService {

        Task<List<Item>> ReadAllAsync();
        Task<bool> CheckItemNameExistsAsync(string itemName);
        Task CreateAsync(Item item);
        Task<Item> ReadAsync(int id);
        Task UpdateAsync(Item modifiedItem);
        Task DeleteAsync(int id);
        void Send(int id);
    }

    public class ItemService : IItemService {

        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient =
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly string KeyVaultHostName, KeyVaultBaseUrl, KeyPrefix;

        private readonly static int MaxGetSecretsResults = 25;


        public ItemService(IConfiguration config, IMemoryCache cache) {
            _config = config;
            _cache = cache;

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";

            KeyPrefix = _config.GetValue<String>("KeyStrings:prefix");
        }

        public async Task<List<Item>> ReadAllAsync() {

            if (_cache.Get("ItemList") == null) {

                int splitMark;
                string secretName;
                List<Item> items = new List<Item>();
                Item item;
                int itemId = 1;

                // Load secrets from KeyVault
                IPage<SecretItem> secrets = await keyVaultClient.GetSecretsAsync(
                    KeyVaultBaseUrl, MaxGetSecretsResults);

                // Store secrets in List
                foreach (SecretItem secret in secrets) {
                    // Secret name follows after the last forward slash in secret.Id
                    splitMark = secret.Id.LastIndexOf('/');
                    secretName = secret.Id.Substring(splitMark + 1);

                    // Consider only keys with "SL--" prefix
                    if ((secretName.Length > 4) &&
                        secretName.StartsWith(KeyPrefix)) {

                        item = new Item() {
                            // Key name format: "SL--itemname"
                            Id = itemId,
                            Name = secretName.Substring(4)
                        };

                        items.Add(item);
                        itemId++;
                    }
                }

                // Order list by name ascending
                List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

                _cache.Set("ItemList", sortedItems);
            }
            return _cache.Get<List<Item>>("ItemList");
        }

        public async Task<bool> CheckItemNameExistsAsync(string itemName) {
            var items = await ReadAllAsync();
            //System.Diagnostics.Debug.WriteLine($"***** check name '{itemName}'");
            try {
                var item = items.Single(c => c.Name == itemName);
            }
            catch (InvalidOperationException) {
                // Didn't find exactly one item named <itemName>
                // Item name doesn't exist
                return false;
            }
            return true;
        }

        public async Task CreateAsync(Item newItem) {

            string itemKey, itemJson;

            var items = await ReadAllAsync();

            // Find Id for new item
            newItem.Id = items.Max(c => c.Id) + 1;

            // Add item to list
            items.Add(newItem);

            // Store item in KeyVault
            itemKey = KeyPrefix + newItem.Name;
            itemJson = JsonConvert.SerializeObject(newItem);
            await keyVaultClient.SetSecretAsync(
                KeyVaultBaseUrl, itemKey, itemJson);

            // Order list by name ascending
            List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

            _cache.Set("ItemList", sortedItems);
        }

        public async Task<Item> ReadAsync(int id) {

            string itemKey, secretJson;
            SecretBundle bundle;

            // Get item list
            List<Item> items = await ReadAllAsync();

            // Check if item content was already loaded from KeyVault
            // Since item.password is mandatory, we'll check if password is present
            var item = items.Single(c => c.Id == id);

            if (item.Password == null) {
                itemKey = KeyPrefix + item.Name;

                try {
                    // Load item content from KeyVault
                    bundle = await keyVaultClient.GetSecretAsync(
                        KeyVaultBaseUrl, itemKey)
                        .ConfigureAwait(false);

                    secretJson = bundle.Value;
                }
                catch (KeyVaultErrorException) {
                    secretJson = "{}";
                }

                //TODO - deserialize Json, update item

            }

            return item;
        }

        public async Task UpdateAsync(Item modifiedItem) {
            var items = await ReadAllAsync();
            var item = items.Single(c => c.Id == modifiedItem.Id);
            item.Name = modifiedItem.Name;
            item.Username = modifiedItem.Username;
            item.Password = modifiedItem.Password;
            item.Uri = modifiedItem.Uri;

            // Order list by name ascending
            List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

            _cache.Set("ItemList", sortedItems);
        }

        public async Task DeleteAsync(int id) {
            var items = await ReadAllAsync();
            var deletedItem = items.Single(c => c.Id == id);
            items.Remove(deletedItem);
            _cache.Set("ItemList", items);
        }

        public void Send(int id) {
            System.Diagnostics.Debug.WriteLine($"***** Sending '{id}'");
        }

    }

}

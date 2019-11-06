﻿using System;
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

namespace AzurePasswordManager.Services {

    public interface IItemService {

        List<Item> ReadAll();
        bool CheckItemNameExists(string itemName);
        void Create(Item item);
        Item Read(int id);
        void Update(Item modifiedItem);
        void Delete(int id);
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

        private readonly string KeyVaultHostName, KeyVaultBaseUrl;

        private readonly static int MaxGetSecretsResults = 25;


        public ItemService(IConfiguration config, IMemoryCache cache) {
            _config = config;
            _cache = cache;

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";
        }

        public List<Item> ReadAll() {

            int splitMark;
            string secretName;

            string keyPrefix = _config.GetValue<String>("KeyStrings:prefix");

            if (_cache.Get("ItemList") == null) {

                List<Item> items = new List<Item>();
                Item item;
                int itemId = 1;

                // Load secrets from KeyVault
                // Note: blocking call
                IPage<SecretItem> secrets = keyVaultClient.GetSecretsAsync(
                    KeyVaultBaseUrl, MaxGetSecretsResults).Result;

                // Using thread pool
                // IPage<SecretItem> secrets = Task.Run(() => keyVaultClient.GetSecretsAsync(
                //    KeyVaultBaseUrl, MaxGetSecretsResults)).Result;

                // Store secrets in List
                foreach (SecretItem secret in secrets) {
                    // Secret name follows after the last forward slash in secret.Id
                    splitMark = secret.Id.LastIndexOf('/');
                    secretName = secret.Id.Substring(splitMark + 1);

                    // Consider only keys with "SL--" prefix
                    // Only keys with the "--password" suffix are mandatory, we'll 
                    // use them as site name source
                    if ((secretName.Length > 4) &&
                        secretName.StartsWith(keyPrefix)) {

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

        public bool CheckItemNameExists(string itemName) {
            var items = ReadAll();
            //System.Diagnostics.Debug.WriteLine($"***** check name '{itemName}'");
            try {
                var item = items.Single(c => c.Name == itemName);
            }
            catch (InvalidOperationException) {
                return false;
            }
            return true;
        }

        public void Create(Item item) {
            var items = ReadAll();
            item.Id = items.Max(c => c.Id) + 1;
            System.Diagnostics.Debug.WriteLine($"***** add name '{item.Name}'");
            items.Add(item);

            // Order list by name ascending
            List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

            _cache.Set("ItemList", sortedItems);
        }

        public Item Read(int id) {
            return ReadAll().Single(c => c.Id == id);
        }

        public void Update(Item modifiedItem) {
            var items = ReadAll();
            var item = items.Single(c => c.Id == modifiedItem.Id);
            item.Name = modifiedItem.Name;
            item.Username = modifiedItem.Username;
            item.Password = modifiedItem.Password;
            item.Uri = modifiedItem.Uri;

            // Order list by name ascending
            List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

            _cache.Set("ItemList", sortedItems);
        }

        public void Delete(int id) {
            var items = ReadAll();
            var deletedItem = items.Single(c => c.Id == id);
            items.Remove(deletedItem);
            _cache.Set("ItemList", items);
        }

        public void Send(int id) {
            System.Diagnostics.Debug.WriteLine($"***** Sending '{id}'");
        }

    }

}

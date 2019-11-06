using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;

using AzurePasswordManager.Models;

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

        private readonly IMemoryCache _cache;

        public ItemService(IMemoryCache cache) {
            _cache = cache;
        }

        public List<Item> ReadAll() {

            if (_cache.Get("ItemList") == null) {

                List<Item> items = new List<Item>() {
                    new Item{Id=1, Name="test1", Username="user1", Password="pass1", Uri = "123" },
                    new Item{Id=2, Name="test2", Username="user2", Password="pass2", Uri = "456" },
                    new Item{Id=3, Name="test3", Username="user3", Password="pass3", Uri = "678" },
                };

                // Order list by name ascending
                List<Item> sortedItems = items.OrderBy(o => o.Name).ToList();

                _cache.Set("ItemList", sortedItems);
            }
            return _cache.Get<List<Item>>("ItemList");
        }

        public bool CheckItemNameExists(string itemName) {
            var items = ReadAll();
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

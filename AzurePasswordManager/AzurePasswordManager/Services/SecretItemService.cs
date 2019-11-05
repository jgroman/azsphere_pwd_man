using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;

using AzurePasswordManager.Models;

namespace AzurePasswordManager.Services {

    public interface ISecretItemService {

        List<SecretItem> ReadAll();
        void Create(SecretItem secretItem);
        SecretItem Read(int id);
        void Update(SecretItem modifiedSecretItem);
        void Delete(int id);
    }

    public class SecretItemService : ISecretItemService {

        private readonly IMemoryCache _cache;
        public SecretItemService(IMemoryCache cache) {
            _cache = cache;
        }

        public List<SecretItem> ReadAll() {

            if (_cache.Get("SecretItemList") == null) {

                List<SecretItem> secretItems = new List<SecretItem>();
                /*
                List<Car> cars = new List<Car>{
                new Car{Id = 1, Make="Audi",Model="R8",Year=2014,Doors=2,Colour="Red",Price=79995},
                new Car{Id = 2, Make="Aston Martin",Model="Rapide",Year=2010,Doors=2,Colour="Black",Price=54995},
                new Car{Id = 3, Make="Porsche",Model=" 911 991",Year=2016,Doors=2,Colour="White",Price=155000},
                new Car{Id = 4, Make="Mercedes-Benz",Model="GLE 63S",Year=2017,Doors=5,Colour="Blue",Price=83995},
                new Car{Id = 5, Make="BMW",Model="X6 M",Year=2016,Doors=5,Colour="Silver",Price=62995},
                };
                */

                _cache.Set("SecretItemList", secretItems);
            }
            return _cache.Get<List<SecretItem>>("SecretItemList");
        }

        public void Create(SecretItem secretItem) {
            var secretItems = ReadAll();
            secretItem.Id = secretItems.Max(c => c.Id) + 1;
            secretItems.Add(secretItem);
            _cache.Set("CarList", secretItems);
        }

        public SecretItem Read(int id) {
            return ReadAll().Single(c => c.Id == id);
        }

        public void Update(SecretItem modifiedSecretItem) {
            var secretItems = ReadAll();
            var secretItem = secretItems.Single(c => c.Id == modifiedSecretItem.Id);
            secretItem.Name = modifiedSecretItem.Name;
            secretItem.Username = modifiedSecretItem.Username;
            secretItem.Password = modifiedSecretItem.Password;
            secretItem.Url = modifiedSecretItem.Url;
            _cache.Set("SecretItemList", secretItems);
        }

        public void Delete(int id) {
            var secretItems = ReadAll();
            var deletedSecretItem = secretItems.Single(c => c.Id == id);
            secretItems.Remove(deletedSecretItem);
            _cache.Set("SecretItemList", secretItems);
        }
    }

}

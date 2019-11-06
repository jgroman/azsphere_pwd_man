using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using AzurePasswordManager.Models;
using AzurePasswordManager.Services;

using Newtonsoft.Json;

namespace AzurePasswordManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemController(IItemService itemService) {
            _itemService = itemService;
        }

        // GET: api/item
        [HttpGet]
        public IEnumerable<Item> Get()
        {
            return _itemService.ReadAll();
        }

        // GET: api/item/5
        [HttpGet("{id}", Name = "Get")]
        public Item Get(int id)
        {
            return _itemService.Read(id);
        }

        // POST: api/item
        [HttpPost]
        public void Post([FromBody] Item item)
        {
            _itemService.Create(item);
        }

        // POST: api/item/send/5
        [HttpPost("send/{id}")]
        public void Send(int id) {
            _itemService.Send(id);
        }

        // PUT: api/item/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] Item item)
        {
            _itemService.Update(item);
        }

        // DELETE: api/item/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            _itemService.Delete(id);
        }

    }
}

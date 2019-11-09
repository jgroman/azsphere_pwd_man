using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using SpherePasswordManager.Models;
using SpherePasswordManager.Services;

namespace SpherePasswordManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }

        // GET: api/item
        [HttpGet]
        public async Task<IEnumerable<Item>> GetAsync()
        {
            return await _itemService.ReadAllAsync();
        }

        // GET: api/item/5
        [HttpGet("{id}", Name = "Get")]
        public async Task<Item> GetAsync(int id)
        {
            return await _itemService.ReadAsync(id);
        }

        // POST: api/item
        [HttpPost]
        public async Task PostAsync([FromBody] Item item)
        {
            await _itemService.CreateAsync(item);
        }

        // PUT: api/item/5
        [HttpPut("{id}")]
        public async Task PutAsync(int id, [FromBody] Item item)
        {
            await _itemService.UpdateAsync(item);
        }

        // DELETE: api/item/5
        [HttpDelete("{id}")]
        public async Task DeleteAsync(int id)
        {
            await _itemService.DeleteAsync(id);
        }

    }
}

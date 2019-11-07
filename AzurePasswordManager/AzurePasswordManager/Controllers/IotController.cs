using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzurePasswordManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AzurePasswordManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IotController : ControllerBase
    {
        private readonly IItemService _itemService;

        public IotController(IItemService itemService) {
            _itemService = itemService;
        }

        // POST: api/iot/send/5
        [HttpPost("send/{id}")]
        public async Task<string> SendAsync(int id) {
            return await _itemService.SendAsync(id);
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using AzurePasswordManager.Services;

namespace AzurePasswordManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValidatorController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ValidatorController(IItemService itemService) {
            _itemService = itemService;
        }

        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> ValidateItemNameAsync([Bind(Prefix = "Item.Name")] string name) {
            if (await _itemService.CheckItemNameExistsAsync(name)) {
                return Content("false", "application/json");
            }
            return Content("true", "application/json");
        }
    }
}
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
        public IActionResult ValidateItemName([Bind(Prefix = "Item.Name")] string name) {
            if (_itemService.CheckItemNameExists(name)) {
                return Content("false", "application/json");
            }
            return Content("true", "application/json");
        }
    }
}
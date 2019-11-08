using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using SpherePasswordManager.Models;
using SpherePasswordManager.Services;

namespace SpherePasswordManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValidatorController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ValidatorController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> ValidateItemNameAsync([Bind(Prefix = "Item.Name")] string Name)
        {
            if (await _itemService.CheckItemNameExistsAsync(Name))
            {
                return Content("false", "application/json");
            }
            return Content("true", "application/json");
        }
    }
}

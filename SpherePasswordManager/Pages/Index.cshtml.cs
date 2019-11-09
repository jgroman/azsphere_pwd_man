using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using SpherePasswordManager.Models;
using SpherePasswordManager.Services;

namespace SpherePasswordManager.Pages
{
    public class IndexModel : PageModel
    {
        public List<Item> Items = new List<Item>();

        private readonly IItemService _itemService;

        public IndexModel(IItemService itemService)
        {
            _itemService = itemService;
        }

        public async Task<PartialViewResult> OnGetItemsLoaderAsync()
        {
            Items = await _itemService.ReadAllAsync();
            return Partial("_ItemListPartial", Items);
        }

        public IActionResult OnPostEdit(int id)
        {
            if (id != 0)
            {
                return RedirectToPage("Edit", "Edit", new { id });
            }

            return Page();
        }

    }
}

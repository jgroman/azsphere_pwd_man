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
    public class EditModel : PageModel
    {
        [BindProperty]
        public Item Item { get; set; }

        private readonly IItemService _itemService;

        public EditModel(IItemService itemService)
        {
            _itemService = itemService;
        }

        /*
        public IActionResult OnGet() {
            return Page();
        }
        */

        public async Task<IActionResult> OnGetAsync(int id)
        {
            /*
            if (id == 0)
            {
                Item = new Item();
                return Page();
            }

            Item = await _itemService.ReadAsync(id);
            if (Item == null)
            {
                return RedirectToPage("/Index");
            }
            */
            return Page();
        }

    }
}
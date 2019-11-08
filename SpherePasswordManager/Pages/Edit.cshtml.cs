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

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (id == 0)
            {
                Item = new Item() { 
                    Id = 0
                };
            }
            else
            {
                Item = await _itemService.ReadAsync(id);
                if (Item == null)
                {
                    return RedirectToPage("/Index");
                }

            }

            return Page();
        }

        public async Task<IActionResult> OnGetEditAsync(int id)
        {
            if (id == 0)
            {
                Item = new Item()
                {
                    Id = 0
                };
            }
            else
            {
                Item = await _itemService.ReadAsync(id);
                if (Item == null)
                {
                    return RedirectToPage("/Index");
                }

            }

            return Page();
        }

        public async Task<IActionResult> OnGetDeleteAsync(int id)
        {
            await _itemService.DeleteAsync(id);
            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnPostSubmitAsync(int id)
        {
            if (Item.Id == 0)
            {
                // Create new item
                await _itemService.CreateAsync(Item);
            }
            else
            {
                // Update existing item
                await _itemService.UpdateAsync(Item);
            }

            return RedirectToPage("/Index");
        }

    }
}
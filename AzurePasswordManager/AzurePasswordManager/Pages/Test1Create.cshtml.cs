using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using AzurePasswordManager.Data;

namespace AzurePasswordManager.Pages
{
    public class Test1CreateModel : PageModel
    {
        private readonly AppDbContext _db;

        public Test1CreateModel(AppDbContext db) {
            _db = db;
        }

        [BindProperty]
        public Customer Customer { get; set; }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            _db.Customers.Add(Customer);
            await _db.SaveChangesAsync();
            return RedirectToPage("/Test1");
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using AzurePasswordManager.Data;

namespace AzurePasswordManager.Pages
{
    public class SiteLoginCreateModel : PageModel
    {
        private readonly AppDbContext _db;

        public SiteLoginCreateModel(AppDbContext db) {
            _db = db;
        }

        [BindProperty]
        public SiteLogin SiteLogin { get; set; }

        public async Task<IActionResult> OnPostAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            _db.SiteLogins.Add(SiteLogin);

            await _db.SaveChangesAsync();

            return RedirectToPage("/Index");
        }

        public void OnGet()
        {

        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AzurePasswordManager.Data;
using Microsoft.EntityFrameworkCore;


namespace AzurePasswordManager.Pages {

    public class IndexModel : PageModel {

        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db) {
            _db = db;

            // Load DB from KeyVault
        }

        public IList<SiteLogin> SiteLogins { get; private set; }

        public async Task OnGetAsync() {
            SiteLogins = await _db.SiteLogins.AsNoTracking().ToListAsync();
        }

        /*
        public void OnGet() {

        }
        */
    }
}

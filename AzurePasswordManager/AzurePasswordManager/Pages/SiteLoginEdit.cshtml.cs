using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.EntityFrameworkCore;
using AzurePasswordManager.Data;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using Microsoft.Extensions.Configuration;


namespace AzurePasswordManager.Pages
{
    public class SiteLoginEditModel : PageModel
    {
        [BindProperty]
        public SiteLogin SiteLogin { get; set; }

        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient =
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly string KeyVaultHostName, KeyVaultBaseUrl;

        private static SiteLogin oldSiteLogin;

        public SiteLoginEditModel(AppDbContext db, IConfiguration config) {
            _db = db;
            _config = config;

            oldSiteLogin = new SiteLogin();

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";
        }

        public async Task<IActionResult> OnGetAsync(int id) 
        {
            // Get SiteLogin from db
            SiteLogin = await _db.SiteLogins.FindAsync(id);

            if (SiteLogin == null) {
                return RedirectToPage("/Index");
            }

            // Read secrets from KeyVault
            await SiteLogin.ReadFromKeyVault(_config);

            // Store current state
            oldSiteLogin.Name = SiteLogin.Name;
            oldSiteLogin.Username = SiteLogin.Username;
            oldSiteLogin.Password = SiteLogin.Password;
            oldSiteLogin.Url = SiteLogin.Url;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync() 
        {
            if (!ModelState.IsValid) 
            {
                return Page();
            }

            // Update secrets in KeyVault
            await SiteLogin.WriteToKeyVault(_config, oldSiteLogin);

            // Update db
            _db.Attach(SiteLogin).State = EntityState.Modified;

            try {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException) {
                throw new Exception($"SiteLogin {SiteLogin.Id} not found!");
            }

            return RedirectToPage("/Index");
        }

    }
}

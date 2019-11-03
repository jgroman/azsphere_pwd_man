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
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient =
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly string KeyVaultHostName, KeyVaultBaseUrl;

        private readonly static string keyPrefix = "SL--";
        private readonly static string keySuffixUrl = "--url";
        private readonly static string keySuffixUsername = "--username";
        private readonly static string keySuffixPassword = "--password";

        private SiteLogin oldSiteLogin;

        private string keyUrl, keyUsername, keyPassword;

        private SecretBundle bundle;

        public SiteLoginEditModel(AppDbContext db, IConfiguration config) {
            _db = db;
            _config = config;

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";
        }

        [BindProperty]
        public SiteLogin SiteLogin { get; set; }

        public async Task<IActionResult> OnGetAsync(int id) 
        {
            // Get SiteLogin name from db
            SiteLogin = await _db.SiteLogins.FindAsync(id);

            if (SiteLogin == null) {
                return RedirectToPage("/Index");
            }

            // Get username, password and url from KeyVault
            // -- Get Username
            keyUsername = keyPrefix + SiteLogin.Name + keySuffixUsername;
            try {
                bundle = await keyVaultClient.GetSecretAsync(
                    KeyVaultBaseUrl, keyUsername)
                    .ConfigureAwait(false);

                SiteLogin.Username = bundle.Value;
            }
            catch(KeyVaultErrorException) {
                SiteLogin.Username = "";
            }

            // -- Get password
            keyPassword = keyPrefix + SiteLogin.Name + keySuffixPassword;
            try {
                bundle = await keyVaultClient.GetSecretAsync(
                    KeyVaultBaseUrl, keyPassword)
                    .ConfigureAwait(false);

                SiteLogin.Password = bundle.Value;
            }
            catch (KeyVaultErrorException) {
                SiteLogin.Password = "";
            }

            // Get URL
            keyUrl = keyPrefix + SiteLogin.Name + keySuffixUrl;
            try {
                bundle = await keyVaultClient.GetSecretAsync(
                    KeyVaultBaseUrl, keyUrl)
                    .ConfigureAwait(false);

                SiteLogin.Url = bundle.Value;
            }
            catch (KeyVaultErrorException) {
                SiteLogin.Url = "";
            }

            // Store current state
            oldSiteLogin = new SiteLogin {
                Name = SiteLogin.Name,
                Username = SiteLogin.Username,
                Password = SiteLogin.Password,
                Url = SiteLogin.Url
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync() 
        {
            if (!ModelState.IsValid) 
            {
                return Page();
            }

            // If siteName changed we will have to create new keyset


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

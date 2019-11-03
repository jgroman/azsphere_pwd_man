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

        private readonly static string keyPrefix = "SL--";
        private readonly static string keySuffixUsername = "--username";
        private readonly static string keySuffixPassword = "--password";
        private readonly static string keySuffixUrl = "--url";

        private static SiteLogin oldSiteLogin = new SiteLogin();

        private string keyUrl, keyUsername, keyPassword;

        private SecretBundle bundle;

        public SiteLoginEditModel(AppDbContext db, IConfiguration config) {
            _db = db;
            _config = config;

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

            keyUsername = keyPrefix + SiteLogin.Name + keySuffixUsername;
            keyPassword = keyPrefix + SiteLogin.Name + keySuffixPassword;
            keyUrl = keyPrefix + SiteLogin.Name + keySuffixUrl;

            string oldKeyUsername = keyPrefix + oldSiteLogin.Name + keySuffixUsername;
            string oldKeyPassword = keyPrefix + oldSiteLogin.Name + keySuffixPassword;
            string oldKeyUrl = keyPrefix + oldSiteLogin.Name + keySuffixUrl;

            if (SiteLogin.Name.Equals(oldSiteLogin.Name)) {
                // Updating existing site data

                // Check username changes
                if (string.IsNullOrEmpty(SiteLogin.Username)) { 
                    if (!string.IsNullOrEmpty(oldSiteLogin.Username)) {
                        // Username was erased, delete secret
                        await keyVaultClient.DeleteSecretAsync(
                            KeyVaultBaseUrl, oldKeyUsername);
                    }
                }
                else {
                    if (!SiteLogin.Username.Equals(oldSiteLogin.Username)) {
                        // Username has changed, create/update secret
                        await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, keyUsername, SiteLogin.Username);
                    }
                }

                // Check password changes
                if (string.IsNullOrEmpty(SiteLogin.Password)) {
                    if (!string.IsNullOrEmpty(oldSiteLogin.Password)) {
                        // Password was erased, delete secret
                        // OK, we cannot erase password key as it is mandatory
                        //await keyVaultClient.DeleteSecretAsync(
                        //    KeyVaultBaseUrl, oldKeyPassword);
                    }
                }
                else {
                    if (!SiteLogin.Password.Equals(oldSiteLogin.Password)) {
                        // Password has changed, create/update secret
                        await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, keyPassword, SiteLogin.Password);
                    }
                }

                // Check URL changes
                if (string.IsNullOrEmpty(SiteLogin.Url)) {
                    if (!string.IsNullOrEmpty(oldSiteLogin.Url)) {
                        // URL was erased, delete secret
                        await keyVaultClient.DeleteSecretAsync(
                            KeyVaultBaseUrl, oldKeyUrl);
                    }
                }
                else {
                    if (!SiteLogin.Url.Equals(oldSiteLogin.Url)) {
                        // URL has changed, create/update secret
                        await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, keyUrl, SiteLogin.Url);
                    }
                }

            }
            else {
                // SiteName was changed we will have to create completely new 
                // keyset and delete old keys

                // -- Remove old secrets
                await keyVaultClient.DeleteSecretAsync(KeyVaultBaseUrl, oldKeyUsername);

                bundle = await keyVaultClient.DeleteSecretAsync(
                    KeyVaultBaseUrl, oldKeyPassword);

                bundle = await keyVaultClient.DeleteSecretAsync(
                    KeyVaultBaseUrl, oldKeyUrl);

                // -- Create new secrets
                if (!SiteLogin.Username.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyUsername, SiteLogin.Username);
                }

                if (!SiteLogin.Password.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyPassword, SiteLogin.Password);
                }
                else {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyPassword, oldSiteLogin.Password);
                }

                if (!SiteLogin.Url.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyUrl, SiteLogin.Url);
                }
            }

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

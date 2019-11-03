using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AzurePasswordManager.Data;
using Microsoft.EntityFrameworkCore;

using Microsoft.Rest.Azure;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using Microsoft.Extensions.Configuration;

namespace AzurePasswordManager.Pages {

    public class IndexModel : PageModel {

        private readonly AppDbContext _db;

        private readonly IConfiguration _config;

        private readonly string iotHubServiceConnStringKey;
        private readonly string azureSphereDeviceConnStringKey;

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient = 
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        private readonly static int SecretMaxResults = 25;

        private readonly string KeyVaultHostName;

        public IndexModel(AppDbContext db, IConfiguration config) 
        {
            _db = db;
            _config = config;

            iotHubServiceConnStringKey = _config.GetValue<String>("ConnectionStringKeys:iotHubService");
            azureSphereDeviceConnStringKey = _config.GetValue<String>("ConnectionStringKeys:azureSphereDevice");

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
        }

        public IList<SiteLogin> SiteLogins { get; private set; }

        public async Task OnGetAsync() {

            int splitMark;
            string secretName, siteName;
            SiteLogin siteLogin;

            // Remove all db rows
            // Note: can be slow when rows > 1000
            _db.SiteLogins.RemoveRange(_db.SiteLogins);

            // Load secrets from KeyVault
            IPage<SecretItem> secrets = await keyVaultClient.GetSecretsAsync($"https://{KeyVaultHostName}.vault.azure.net/", SecretMaxResults)
                .ConfigureAwait(false);

            //System.Diagnostics.Debug.WriteLine($"***** Got secrets '{secrets.Count()}'");

            // Store secrets in db
            foreach (SecretItem secret in secrets) 
            {
                // Secret name follows after the last forward slash in secret.Id
                splitMark = secret.Id.LastIndexOf('/');
                secretName = secret.Id.Substring(splitMark + 1);

                // Consider only keys with "SL--" prefix
                // Only keys with the "--password" suffix are mandatory, we'll 
                // use them as site name source
                if ((secretName.Length > 14) && 
                    secretName.StartsWith("SL--") &&
                    secretName.EndsWith("--password")) 
                {
                    // Secret name format: "SL--sitename--password"
                    siteName = secretName.Substring(4);
                    splitMark = siteName.LastIndexOf("--");
                    siteName = siteName.Substring(0, splitMark);

                    siteLogin = new SiteLogin {
                        Name = siteName
                    };

                    _db.SiteLogins.Add(siteLogin);
                }
            }

            // Commit db changes
            await _db.SaveChangesAsync();

            // Get list of secrets from db
            SiteLogins = await _db.SiteLogins.AsNoTracking().ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id) {

            var siteLogin = await _db.SiteLogins.FindAsync(id);

            if (siteLogin != null) {
                _db.SiteLogins.Remove(siteLogin);
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }

    /*
    public void OnGet() {

    }
    */
}


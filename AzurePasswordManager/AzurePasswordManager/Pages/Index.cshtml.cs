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

using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;

using Newtonsoft.Json;

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

        private readonly string KeyVaultHostName, KeyVaultBaseUrl;

        public IndexModel(AppDbContext db, IConfiguration config) 
        {
            _db = db;
            _config = config;

            iotHubServiceConnStringKey = _config.GetValue<String>("ConnectionStringKeys:iotHubService");
            azureSphereDeviceConnStringKey = _config.GetValue<String>("ConnectionStringKeys:azureSphereDevice");

            KeyVaultHostName = _config.GetValue<String>("KeyVaultName");
            KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";
        }

        public IList<SiteLogin> SiteLogins { get; private set; }

        public async Task OnGetAsync() {

            int splitMark;
            string secretName, siteName;
            SiteLogin siteLogin;

            string keyPrefix = _config.GetValue<String>("KeyStrings:prefix");
            string keySuffixPassword = _config.GetValue<String>("KeyStrings:suffixPassword");

            // Remove all db rows
            // Note: can be slow when rows > 1000
            _db.SiteLogins.RemoveRange(_db.SiteLogins);

            // Load secrets from KeyVault
            IPage<SecretItem> secrets = await keyVaultClient.GetSecretsAsync(
                KeyVaultBaseUrl, SecretMaxResults)
                .ConfigureAwait(false);

            //System.Diagnostics.Debug.WriteLine($"***** Got secrets '{secrets.Count()}'");

            // TODO: Add loop for GetSecretsAsync

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
                    secretName.StartsWith(keyPrefix) &&
                    secretName.EndsWith(keySuffixPassword))
                {
                    // Key name format: "SL--sitename--password"
                    siteName = secretName.Substring(4);
                    splitMark = siteName.LastIndexOf("--");
                    siteName = siteName.Substring(0, splitMark);

                    siteLogin = new SiteLogin() {
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

            // TODO: delete keys

            if (siteLogin != null) {
                _db.SiteLogins.Remove(siteLogin);
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendAsync(int id) {

            var siteLogin = await _db.SiteLogins.FindAsync(id);

            if (siteLogin == null) {
                return RedirectToPage();
            }

            // Read siteLogin secrets from KeyVault
            await siteLogin.ReadFromKeyVault(_config).ConfigureAwait(false);

            // Get connection string secrets from KeyVault
            ConnectionString connectionString = new ConnectionString();
            await connectionString.ReadFromKeyVault(_config).ConfigureAwait(false);

            ServiceClient serviceClient;

            // Prepare device method call via Iot Hub
            try {
                serviceClient = ServiceClient.CreateFromConnectionString(connectionString.IotHubService);
            }
            catch (FormatException fex) {
                if (fex.Message.Equals("Malformed Token")) {
                    // Invalid Iot Hub Service Connection String
                    System.Diagnostics.Debug.WriteLine($"***** Malformed token");

                }
                return RedirectToPage();
            }

            string methodName = _config.GetValue<string>("AzureSphereDevice:directMethodName");
            int methodTimeout = _config.GetValue<int>("AzureSphereDevice:directMethodCallTimeout");

            var methodInvocation = new CloudToDeviceMethod(methodName) {
                ResponseTimeout = TimeSpan.FromSeconds(methodTimeout)
            };

            methodInvocation.SetPayloadJson(JsonConvert.SerializeObject(siteLogin));

            // Invoke the direct method asynchronously and get the response from IoT device.
            try {
                var response = await serviceClient.InvokeDeviceMethodAsync(connectionString.AzureSphereDevice, methodInvocation);
                System.Diagnostics.Debug.WriteLine($"***** Response payload '{response.GetPayloadAsJson()}'");

                // result = response.GetPayloadAsJson();
            }
            catch (DeviceNotFoundException dnfex) {
                //System.Diagnostics.Debug.WriteLine($"***** EX: '{dnfex}'");

                // errorCode 404001 - invalid name
                // errorCode 404103 - timeout

                if (dnfex.Message.Contains(":404001,")) {
                    // Device not registered or incorrect name
                    System.Diagnostics.Debug.WriteLine($"***** EX: Device not registered");
                }
                else if (dnfex.Message.Contains(":404103,")) {
                    // Device not registered or incorrect name
                    System.Diagnostics.Debug.WriteLine($"***** EX: Timeout connecting to device");
                }

            }

            return RedirectToPage();
        }

    }



}


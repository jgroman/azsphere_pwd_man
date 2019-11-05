using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.Extensions.Configuration;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;


namespace AzurePasswordManager.Data {

    public class SiteLogin 
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Name cannot be longer than 30 characters.")]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Password { get; set; }

        [StringLength(50)]
        public string Username { get; set; }

        [Url]
        [StringLength(100)]
        public string Url { get; set; }

        private static AzureServiceTokenProvider azureServiceTokenProvider =
            new AzureServiceTokenProvider();

        private readonly static KeyVaultClient keyVaultClient =
            new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

        public async Task ReadFromKeyVault(IConfiguration config) {

            SecretBundle bundle;

            string KeyVaultHostName = config.GetValue<String>("KeyVaultName");
            string KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";

            // Get SiteLogin Secrets' KeyVault keys
            KeyName keyName = new KeyName(config, Name);

            // Get Username from KeyVault
            try {
                bundle = await keyVaultClient.GetSecretAsync(
                    KeyVaultBaseUrl, keyName.Username)
                    .ConfigureAwait(false);

                Username = bundle.Value;
            }
            catch (KeyVaultErrorException) {
                Username = "";
            }

            // Get password from KeyVault
            try {
                bundle = await keyVaultClient.GetSecretAsync(
                    KeyVaultBaseUrl, keyName.Password)
                    .ConfigureAwait(false);

                Password = bundle.Value;
            }
            catch (KeyVaultErrorException) {
                Password = "";
            }

            // Get URL from KeyVault
            try {
                bundle = await keyVaultClient.GetSecretAsync(
                    KeyVaultBaseUrl, keyName.Url)
                    .ConfigureAwait(false);

                Url = bundle.Value;
            }
            catch (KeyVaultErrorException) {
                Url = "";
            }
        }

        public async Task WriteToKeyVault(IConfiguration config, SiteLogin previousState) {

            string KeyVaultHostName = config.GetValue<String>("KeyVaultName");
            string KeyVaultBaseUrl = $"https://{KeyVaultHostName}.vault.azure.net/";

            // Get SiteLogin Secrets' KeyVault keys
            KeyName keyName = new KeyName(config, Name);

            if (previousState == null) {
                // We'll just create new keys

                if (!Username.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Username, Username);
                }

                if (!Password.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Password, Password);
                }

                if (!Url.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Url, Url);
                }

                return;
            }

            // Get original SiteLogin Secrets' KeyVault keys
            KeyName oldKeyName = new KeyName(config, previousState.Name);

            if (Name.Equals(previousState.Name)) {
                // Updating existing site data

                // Check username changes
                if (string.IsNullOrEmpty(Username)) {
                    if (!string.IsNullOrEmpty(previousState.Username)) {
                        // Username was erased, delete old secret
                        await keyVaultClient.DeleteSecretAsync(
                            KeyVaultBaseUrl, oldKeyName.Username);
                    }
                }
                else {
                    if (!Username.Equals(previousState.Username)) {
                        // Username has changed, create/update secret
                        await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, keyName.Username, Username);
                    }
                }

                // Check password changes
                if (string.IsNullOrEmpty(Password)) {
                    if (!string.IsNullOrEmpty(previousState.Password)) {
                        // Password was erased, delete old secret ?
                        // OK, we cannot erase password key as it is mandatory
                        // Only modification is allowed
                        //await keyVaultClient.DeleteSecretAsync(
                        //    KeyVaultBaseUrl, oldKeyName.Password);
                    }
                }
                else {
                    if (!Password.Equals(previousState.Password)) {
                        // Password has changed, create/update secret
                        await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, keyName.Password, Password);
                    }
                }

                // Check URL changes
                if (string.IsNullOrEmpty(Url)) {
                    if (!string.IsNullOrEmpty(previousState.Url)) {
                        // URL was erased, delete old secret
                        await keyVaultClient.DeleteSecretAsync(
                            KeyVaultBaseUrl, oldKeyName.Url);
                    }
                }
                else {
                    if (!Url.Equals(previousState.Url)) {
                        // URL has changed, create/update secret
                        await keyVaultClient.SetSecretAsync(
                            KeyVaultBaseUrl, keyName.Url, Url);
                    }
                }

            }
            else {
                // SiteName was changed we will have to create completely new 
                // keyset and delete old keys

                // -- Remove old secrets
                await keyVaultClient.DeleteSecretAsync(KeyVaultBaseUrl, oldKeyName.Username);

                await keyVaultClient.DeleteSecretAsync(KeyVaultBaseUrl, oldKeyName.Password);

                await keyVaultClient.DeleteSecretAsync(KeyVaultBaseUrl, oldKeyName.Url);

                // -- Create new secrets
                if (!Username.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Username, Username);
                }

                if (!Password.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Password, Password);
                }
                else {
                    // Since password key is mandatory and new password is null,
                    // we'll reuse old site password
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Password, previousState.Password);
                }

                if (!Url.Equals(null)) {
                    await keyVaultClient.SetSecretAsync(
                        KeyVaultBaseUrl, keyName.Url, Url);
                }
            }

        }
    }
}

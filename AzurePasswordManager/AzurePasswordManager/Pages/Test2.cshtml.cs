using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

// NuGet packages KeyVault, AppAuthentication
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using Microsoft.Extensions.Configuration;

// https://docs.microsoft.com/en-us/azure/key-vault/tutorial-net-create-vault-azure-web-app

namespace AzurePasswordManager.Pages
{
    public class Test2Model : PageModel
    {
        private readonly IConfiguration _config;

        public Test2Model(IConfiguration config) {
            _config = config;
        }

        public string Message { get; set; }

        public async Task OnGetAsync() {
            Message = "Your application description page.";
            //int retries = 0;
            //bool retry = false;
            try {
                /* The next four lines of code show you how to use AppAuthentication library to fetch secrets from your key vault */
                /*
                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

                KeyVaultClient keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(
                        azureServiceTokenProvider.KeyVaultTokenCallback));

                var secret = await keyVaultClient.GetSecretAsync("https://jgtestkeyvault.vault.azure.net/secrets/AppSecret")
                        .ConfigureAwait(false);
                Message = secret.Value;
                */
                //Message = _config.GetValue<String>("AppSecret", "");
                Message = _config.GetConnectionString("IotHubService");

            }
            /* If you have throttling errors see this tutorial https://docs.microsoft.com/azure/key-vault/tutorial-net-create-vault-azure-web-app */
            /// <exception cref="KeyVaultErrorException">
            /// Thrown when the operation returned an invalid status code
            /// </exception>
            catch (KeyVaultErrorException keyVaultException) {
                Message = keyVaultException.Message;
            }
        }

        // This method implements exponential backoff if there are 429 errors from Azure Key Vault
        private static long getWaitTime(int retryCount) {
            long waitTime = ((long)Math.Pow(2, retryCount) * 100L);
            return waitTime;
        }

        // This method fetches a token from Azure Active Directory, which can then be provided to Azure Key Vault to authenticate
        public async Task<string> GetAccessTokenAsync() {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            string accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://vault.azure.net");
            return accessToken;
        }
    }
}
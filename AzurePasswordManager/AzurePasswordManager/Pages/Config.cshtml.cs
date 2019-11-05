using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Extensions.Configuration;

using AzurePasswordManager.Data;

namespace AzurePasswordManager.Pages
{
    public class ConfigModel : PageModel
    {
        [BindProperty]
        public ConnectionString ConnectionString { get; set; }

        private readonly IConfiguration _config;

        private static ConnectionString oldConnectionString = new ConnectionString();

        public ConfigModel(IConfiguration config) 
        {
            _config = config;
            ConnectionString = new ConnectionString();
        }

        public async Task OnGetAsync() {

            // Read connection strings from KeyVault
            await ConnectionString.ReadFromKeyVault(_config).ConfigureAwait(false);

            // Store current state
            oldConnectionString.IotHubService = ConnectionString.IotHubService;
            oldConnectionString.AzureSphereDevice = ConnectionString.AzureSphereDevice;
        }

        public async Task<IActionResult> OnPostAsync() {

            if (!ModelState.IsValid) {
                return Page();
            }

            // Write only updated connection strings to KeyVault
            await ConnectionString.WriteToKeyVault(_config, oldConnectionString)
                .ConfigureAwait(false);

            return RedirectToPage("/Index");
        }
    }
}

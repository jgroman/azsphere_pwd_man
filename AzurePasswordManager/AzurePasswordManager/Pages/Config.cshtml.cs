using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Extensions.Configuration;

using AzurePasswordManager.Models;
using AzurePasswordManager.Services;

namespace AzurePasswordManager.Pages
{
    public class ConfigModel : PageModel
    {
        [BindProperty]
        public ConfigData ConfigData { get; set; }

        private readonly IConfiguration _config;
        private readonly IConfigDataService _configDataService;

        public ConfigModel(IConfiguration config, IConfigDataService configDataService) 
        {
            _config = config;
            _configDataService = configDataService;

            ConfigData = new ConfigData();
        }

        public async Task OnGetAsync() {

            // Read ConfigData
            ConfigData = await _configDataService.ReadAsync();
        }

        public async Task<IActionResult> OnPostAsync() {

            if (!ModelState.IsValid) {
                return Page();
            }

            // Update KeyVault secrets and cache
            await _configDataService.WriteAsync(ConfigData);

            return RedirectToPage("/Index");
        }
    }
}

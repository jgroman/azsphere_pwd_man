using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using Microsoft.Rest.Azure;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;

using Microsoft.Extensions.Configuration;

using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;

using System.ComponentModel.DataAnnotations;

using Newtonsoft.Json;

using AzurePasswordManager.Models;

namespace AzurePasswordManager.Pages {

    public class IndexModel : PageModel {

        [BindProperty]
        public Item Item { get; set; }

    }

}

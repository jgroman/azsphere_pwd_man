using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using SpherePasswordManager.Models;

namespace SpherePasswordManager.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public Item Item { get; set; }

    }
}

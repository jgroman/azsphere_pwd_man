using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SpherePasswordManager.Models
{
    public class ConfigData
    {
        [Required]
        [Display(Name = "IoT Hub Service Connection String")]
        public string IotHubService { get; set; }

        [Required]
        [Display(Name = "Azure Sphere Device Name")]
        public string AzureSphereDevice { get; set; }
    }
}

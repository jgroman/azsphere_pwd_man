using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace AzurePasswordManager.Models {
    public class ConfigData {

        [Required]
        [Display(Name = "IoT Hub Service Connection String")]
        public string IotHubService { get; set; }

        [Required]
        [Display(Name = "Azure Sphere Device Name")]
        public string AzureSphereDevice { get; set; }


    }
}

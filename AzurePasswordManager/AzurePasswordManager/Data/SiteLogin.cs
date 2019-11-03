using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzurePasswordManager.Data {

    public class SiteLogin 
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Site name cannot be longer than 30 characters.")]
        [Display(Name = "Site Name")]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Password { get; set; }

        [StringLength(50)]
        public string Username { get; set; }

        [StringLength(100)]
        public string Url { get; set; }
    }
}

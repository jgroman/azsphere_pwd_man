using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzurePasswordManager.Models {

    public class SecretItem {

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

    }

}

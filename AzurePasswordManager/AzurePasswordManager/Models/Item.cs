using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzurePasswordManager.Models {

    public class Item {

        public int Id { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Name cannot be longer than 30 characters.")]
        [Remote(action: "ValidateItemNameAsync", controller: "Validator", ErrorMessage = "Name already exists.")]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Password { get; set; }

        [StringLength(50)]
        public string Username { get; set; }

        
        [StringLength(100)]
        public string Uri { get; set; }

    }

}

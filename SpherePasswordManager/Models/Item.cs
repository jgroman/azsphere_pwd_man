using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SpherePasswordManager.Models
{
    public class Item
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Name cannot be longer than 30 characters.")]
        [RegularExpression(@".*[a-zA-Z0-9\-]$", ErrorMessage = "Alphanumeric characters and dashes only.")]
        [PageRemote(
            PageName ="Edit",
            PageHandler = "ValidateName",
            AdditionalFields = nameof(Id) + ", __RequestVerificationToken",
            HttpMethod = "post",
            ErrorMessage = "Name already exists."
        )]
        public string Name { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Password cannot be longer than 50 characters.")]
        public string Password { get; set; }

        [StringLength(50, ErrorMessage = "Username cannot be longer than 50 characters.")]
        public string Username { get; set; }

        [StringLength(100, ErrorMessage = "URL cannot be longer than 100 characters.")]
        public string Uri { get; set; }
    }
}

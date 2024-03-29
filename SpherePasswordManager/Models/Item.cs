﻿using Microsoft.AspNetCore.Mvc;
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

        public string Name { get; set; }

        public string Password { get; set; }

        public string Username { get; set; }

        public bool UsernameEnter { get; set; }

        public bool PasswordEnter { get; set; }

        public bool UnameTabPass { get; set; }

        public bool LoadAndSend { get; set; }
    }
}

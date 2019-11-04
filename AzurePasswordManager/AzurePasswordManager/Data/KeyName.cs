using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

namespace AzurePasswordManager.Data {

    public class KeyName {

        public string Username;
        public string Password;
        public string Url;

        private readonly IConfiguration _config;

        private readonly string prefix;
        private readonly string suffixUsername;
        private readonly string suffixPassword;
        private readonly string suffixUrl;

        public KeyName(IConfiguration config, string siteName) {

            _config = config;

            prefix = _config.GetValue<String>("KeyStrings:prefix");
            suffixUsername = _config.GetValue<String>("KeyStrings:suffixUsername");
            suffixPassword = _config.GetValue<String>("KeyStrings:suffixPassword");
            suffixUrl = _config.GetValue<String>("KeyStrings:suffixUrl");

            Username = prefix + siteName + suffixUsername;
            Password = prefix + siteName + suffixPassword;
            Url = prefix + siteName + suffixUrl;
        }

    }

}

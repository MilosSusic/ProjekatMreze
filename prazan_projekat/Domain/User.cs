using System;
using System.Collections.Generic;

namespace Domain
{
    
        [Serializable]
        public class User
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public decimal Balance { get; set; }
            public decimal MaxWithdrawalAmount { get; set; }
            public string AccountNumber { get; set; }

            public User()
            {
                Username = string.Empty;
                Password = string.Empty;
                FirstName = string.Empty;
                LastName = string.Empty;
                AccountNumber = GenerateAccountNumber();
            }

            private static string GenerateAccountNumber()
            {
                return "ACC-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            }
        }
}

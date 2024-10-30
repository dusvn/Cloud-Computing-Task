using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class User
    {
        private string firstName;
        private string lastName;
        private double balance;//EUR
        private string email;

        public User(string firstName, string lastName, double balance, string email)
        {
            FirstName = firstName;
            LastName = lastName;
            Balance = balance;
            Email = email;
        }

        public User()
        {
        }

        public string FirstName { get => firstName; set => firstName = value; }
        public string LastName { get => lastName; set => lastName = value; }
        public double Balance { get => balance; set => balance = value; }
        public string Email { get => email; set => email = value; }
    }
}

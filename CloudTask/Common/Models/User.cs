using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class User
    {
        private Guid userId;
        private string firstName;
        private string lastName;
        private double balance;//EUR
        private string email;

        public User(Guid userId, string firstName, string lastName, double balance, string email)
        {
            UserId = userId;
            FirstName = firstName;
            LastName = lastName;
            Balance = balance;
            Email = email;
        }

        public User()
        {
        }

        /// <summary>Business key of the user; the Bank partition is chosen from it.</summary>
        public Guid UserId { get => userId; set => userId = value; }
        public string FirstName { get => firstName; set => firstName = value; }
        public string LastName { get => lastName; set => lastName = value; }
        public double Balance { get => balance; set => balance = value; }
        public string Email { get => email; set => email = value; }
    }
}

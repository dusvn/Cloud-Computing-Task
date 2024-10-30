using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class Purchase
    {
        private string firstName; //Always Dusan 
        private string lastName;  //Always Paripovic ,bcs we have only one user this is fixed 
        private string bookName;  
        private int quantity;
        private string email;
        private double amountOfPurchase;

        public Purchase(string firstName, string lastName, string bookName, int quantity, string email)
        {
            FirstName = firstName;
            LastName = lastName;
            BookName = bookName;
            Quantity = quantity;
            Email = email;
            AmountOfPurchase = 0.0;
        }

        public Purchase()
        {
        }

        public string FirstName { get => firstName; set => firstName = value; }
        public string LastName { get => lastName; set => lastName = value; }
        public string BookName { get => bookName; set => bookName = value; }
        public int Quantity { get => quantity; set => quantity = value; }
        public string Email { get => email; set => email = value; }
        public double AmountOfPurchase { get => amountOfPurchase; set => amountOfPurchase = value; }
    }
}

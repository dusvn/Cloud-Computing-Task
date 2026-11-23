using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class Purchase
    {
        private Guid userId;
        private Guid bookId;
        private string firstName; //Always Dusan
        private string lastName;  //Always Paripovic ,bcs we have only one user this is fixed 
        private string bookName;  
        private int quantity;
        private string email;
        private double amountOfPurchase;

        /// <summary>Set by the orchestrator (= transaction id); used as idempotency key in the report.</summary>
        public Guid PurchaseId { get; set; }

        public Purchase(Guid userId, Guid bookId, string firstName, string lastName, string bookName, int quantity, string email)
        {
            UserId = userId;
            BookId = bookId;
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

        /// <summary>Selects the Bank partition.</summary>
        public Guid UserId { get => userId; set => userId = value; }
        /// <summary>Selects the Bookstore partition.</summary>
        public Guid BookId { get => bookId; set => bookId = value; }
        public string FirstName { get => firstName; set => firstName = value; }
        public string LastName { get => lastName; set => lastName = value; }
        public string BookName { get => bookName; set => bookName = value; }
        public int Quantity { get => quantity; set => quantity = value; }
        public string Email { get => email; set => email = value; }
        public double AmountOfPurchase { get => amountOfPurchase; set => amountOfPurchase = value; }
    }
}

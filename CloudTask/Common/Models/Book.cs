using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class Book
    {
        private Guid bookId;
        private string bookName;
        private int quantity;
        private double price;

        /// <summary>Business key of the book; the Bookstore partition is chosen from it.</summary>
        public Guid BookId { get => bookId; set => bookId = value; }
        public string BookName { get => bookName; set => bookName = value; }
        public int Quantity { get => quantity; set => quantity = value; }
        public double Price { get => price; set => price = value; }

        public Book(Guid bookId, string bookName, int quantity, double price)
        {
            this.BookId = bookId;
            this.BookName = bookName;
            this.Quantity = quantity;
            this.Price = price;
        }

        public Book()
        {
        }
    }
}

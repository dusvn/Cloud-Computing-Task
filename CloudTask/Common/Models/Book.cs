using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class Book
    {
        private string bookName;
        private int quantity;
        private double price;

        public string BookName { get => bookName; set => bookName = value; }
        public int Quantity { get => quantity; set => quantity = value; }
        public double Price { get => price; set => price = value; }

        public Book(string bookName, int quantity, double price)
        {
            this.BookName = bookName;
            this.Quantity = quantity;
            this.Price = price;
        }

        public Book()
        {
        }
    }
}

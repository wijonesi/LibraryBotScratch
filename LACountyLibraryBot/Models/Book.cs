using System;

namespace LACountyLibraryBot.Models
{
    public class Book
    {
        public string ID { get; set; }
        public string Author { get; set; }
        public string Title { get; set; }
        public DateTime CheckedOut { get; set; }
        public DateTime DueBack { get; set; }
        public double Fee { get; set; }

        public Book (string ID, string author, string title, DateTime checkedout, DateTime duedate, double fee)
        {
            this.ID = ID;
            this.Author = author;
            this.Title = title;
            this.CheckedOut = checkedout;
            this.DueBack = duedate;
            this.Fee = fee;
        }
    }
}
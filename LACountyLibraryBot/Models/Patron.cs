using System;
using System.Collections.Generic;

namespace LACountyLibraryBot.Models
{
    public class Patron
    {
        public string ID { get; set; }
        public string Language { get; set; }
        public string EmailAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
        public string NearestLibrary { get; set; }
        public List<Book> Books { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public Patron()
        {

        }
    }
}
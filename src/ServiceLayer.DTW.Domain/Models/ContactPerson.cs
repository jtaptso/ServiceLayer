using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceLayer.DTW.Domain.Models
{
    public class ContactPerson
    {
        public string Name { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone1 { get; set; } = string.Empty;
        public string MobilePhone { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
    }
}

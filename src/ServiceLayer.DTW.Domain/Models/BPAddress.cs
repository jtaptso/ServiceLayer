using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceLayer.DTW.Domain.Models
{
    public class BPAddress
    {
        public string AdressName { get; set; } = string.Empty;
        public string AdressType { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
}

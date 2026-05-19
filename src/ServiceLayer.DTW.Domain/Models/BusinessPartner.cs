using ServiceLayer.DTW.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceLayer.DTW.Domain.Models
{
    public class BusinessPartner
    {
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public CardType CardType{ get;  set; }
        public int? GroupCode { get; set; }
        public string? Currency { get; set; }
        public int? PayTermsGrpCode { get; set; }
        public string? Phone1 { get; set; }
        public string? EmailAddress { get; set; }
        public string? Website { get; set; }
        public string? FederalTaxID { get; set; }

        public List<BPAddress> Addresses { get; set; } = new List<BPAddress>();
        public List<ContactPerson> Contacts { get; set; } = new List<ContactPerson>();
    }
}

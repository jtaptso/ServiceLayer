using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Domain.Models;
using ServiceLayer.DTW.Infrastructure.DTOs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ServiceLayer.DTW.Infrastructure.Mapping
{
public class BusinessPartnerMapper : IBusinessPartnerMapper
{
        /// <summary>
        /// Maps a single flat ParsedRowDto to a BusinessPartner domain entity.
        /// Flat address columns (BillTo_*, ShipTo_*) become BPAddress children.
        /// Flat contact columns (Contact{n}_*) become ContactPerson children.
        /// U_* columns are captured as UdfFields and passed to Service Layer.
        /// </summary>
        public BusinessPartner ToDomain(ParsedRowDto row)
        {
            var f = row.Fields;

            var bp = new BusinessPartner
            {
                CardCode = f.GetValueOrDefault("CardCode", string.Empty),
                CardName = f.GetValueOrDefault("CardName", string.Empty),
                CardType = ParseCardType(f.GetValueOrDefault("CardType", "C")),
                GroupCode = ParseNullableInt(f.GetValueOrDefault("GroupCode")),
                Currency = NullIfEmpty(f.GetValueOrDefault("Currency")),
                PayTermsGrpCode = ParseNullableInt(f.GetValueOrDefault("PayTermsGrpCode")),
                Phone1 = NullIfEmpty(f.GetValueOrDefault("Phone1")),
                EmailAddress = NullIfEmpty(f.GetValueOrDefault("EmailAddress")),
                Website = NullIfEmpty(f.GetValueOrDefault("Website")),
                FederalTaxID = NullIfEmpty(f.GetValueOrDefault("FederalTaxID")),
                UdfFields = ExtractUdfs(f)
            };

            // Bill-to address
            if (f.ContainsKey("BillTo_Street") || f.ContainsKey("BillTo_City"))
                bp.Addresses.Add(ExtractAddress(f, "BillTo", "bo_BillTo", "Bill To"));

            // Ship-to address
            if (f.ContainsKey("ShipTo_Street") || f.ContainsKey("ShipTo_City"))
                bp.Addresses.Add(ExtractAddress(f, "ShipTo", "bo_ShipTo", "Ship To"));

            // Contacts: Contact1_, Contact2_, … up to Contact9_
            for (int n = 1; n <= 9; n++)
            {
                string prefix = $"Contact{n}_";
                if (!f.Keys.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    break;

                var contact = new ContactPerson
                {
                    Name = f.GetValueOrDefault($"{prefix}Name", string.Empty),
                    FirstName = f.GetValueOrDefault($"{prefix}FirstName", string.Empty),
                    LastName = f.GetValueOrDefault($"{prefix}LastName", string.Empty),
                    Phone1 = f.GetValueOrDefault($"{prefix}Phone1", string.Empty),
                    MobilePhone = f.GetValueOrDefault($"{prefix}MobilePhone", string.Empty),
                    Email = f.GetValueOrDefault($"{prefix}E_Mail", string.Empty),
                    Position = f.GetValueOrDefault($"{prefix}Position", string.Empty)
                };

                if (!string.IsNullOrWhiteSpace(contact.Name) || !string.IsNullOrWhiteSpace(contact.Email))
                    bp.Contacts.Add(contact);
            }

            return bp;
        }

        /// <summary>
        /// Maps a domain BusinessPartner to the SL DTO that will be POST/PATCHed.
        /// UDF fields are serialized via [JsonExtensionData].
        /// </summary>
        public static SLBusinessPartner ToSLDto(BusinessPartner bp) => new()
        {
            CardCode = bp.CardCode,
            CardName = bp.CardName,
            CardType = CardTypeToSLString(bp.CardType),
            GroupCode = bp.GroupCode,
            Currency = bp.Currency,
            PayTermsGrpCode = bp.PayTermsGrpCode,
            Phone1 = bp.Phone1,
            EmailAddress = bp.EmailAddress,
            Website = bp.Website,
            FederalTaxID = bp.FederalTaxID,
            AdditionalProperties = ToJsonElementDict(bp.UdfFields),

            BPAddresses = bp.Addresses.Select(a => new SLBPAddress
            {
                AddressName = a.AddressName,
                AddressType = a.AddressType,
                Street = a.Street,
                City = a.City,
                ZipCode = a.ZipCode,
                Country = a.Country,
                State = a.State
            }).ToList(),

            ContactEmployees = bp.Contacts.Select(c => new SLContactEmployee
            {
                Name = c.Name,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone1 = c.Phone1,
                MobilePhone = c.MobilePhone,
                Email = c.Email,
                Position = c.Position
            }).ToList()
        };

        private static BPAddress ExtractAddress(
        Dictionary<string, string> f,
        string prefix,
        string addressType,
        string defaultName)
        {
            string P(string col) => f.GetValueOrDefault($"{prefix}_{col}", string.Empty);
            return new BPAddress
            {
                AddressName = NullIfEmpty(f.GetValueOrDefault($"{prefix}_AddressName")) ?? defaultName,
                AddressType = addressType,
                Street = P("Street"),
                City = P("City"),
                ZipCode = P("ZipCode"),
                Country = P("Country"),
                State = P("State")
            };
        }

        private static Dictionary<string, string> ExtractUdfs(Dictionary<string, string> fields) =>
        fields
            .Where(kvp => kvp.Key.StartsWith("U_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, JsonElement>? ToJsonElementDict(Dictionary<string, string> udfs)
        {
            if (udfs.Count == 0) return null;
            return udfs.ToDictionary(
                kvp => kvp.Key,
                kvp => JsonSerializer.SerializeToElement(kvp.Value));
        }

        private static CardType ParseCardType(string value) => value.ToUpperInvariant() switch
        {
            "C" or "CUSTOMER" => CardType.Customer,
            "S" or "SUPPLIER" => CardType.Supplier,
            "L" or "LEAD" => CardType.Lead,
            _ => CardType.Customer
        };

        private static string CardTypeToSLString(CardType ct) => ct switch
        {
            CardType.Customer => "cCustomer",
            CardType.Supplier => "cSupplier",
            CardType.Lead => "cLead",
            _ => "cCustomer"
        };

        private static int? ParseNullableInt(string? v) => int.TryParse(v, out var r) ? r : null;
        private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
    }
}

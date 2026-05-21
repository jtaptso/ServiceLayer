using ServiceLayer.DTW.Application.DTOs;
using ServiceLayer.DTW.Domain.Models;

namespace ServiceLayer.DTW.Application.Interfaces;

public interface IBusinessPartnerMapper
{
    BusinessPartner ToDomain(ParsedRowDto row);
}

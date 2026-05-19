using ServiceLayer.DTW.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceLayer.DTW.Domain.Interfaces
{
    public interface IImportRepository
    {
        Task AddAsync(BusinessPartner bp, CancellationToken ct = default);
        Task UpdateAsync(BusinessPartner bp, CancellationToken ct = default);
        Task<bool> ExistsAsync(string cardCode, CancellationToken ct = default);
    }
}

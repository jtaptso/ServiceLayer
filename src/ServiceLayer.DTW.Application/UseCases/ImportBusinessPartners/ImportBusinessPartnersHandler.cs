using FluentValidation;
using MediatR;
using ServiceLayer.DTW.Application.Interfaces;
using ServiceLayer.DTW.Domain.Enums;
using ServiceLayer.DTW.Infrastructure.Mapping;
using ServiceLayer.DTW.Infrastructure.Parsing;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public class ImportBusinessPartnersHandler
    : IRequestHandler<ImportBusinessPartnersCommand, ImportBusinessPartnersResult>
{
    private readonly FileParserResolver                            _parserResolver;
    private readonly IServiceLayerClient                           _slClient;
    private readonly IValidator<Domain.Models.BusinessPartner>     _validator;

    public ImportBusinessPartnersHandler(
        FileParserResolver                              parserResolver,
        IServiceLayerClient                             slClient,
        IValidator<Domain.Models.BusinessPartner>       validator)
    {
        _parserResolver = parserResolver;
        _slClient       = slClient;
        _validator      = validator;
    }

    public async Task<ImportBusinessPartnersResult> Handle(
        ImportBusinessPartnersCommand command,
        CancellationToken             ct)
    {
        var result = new ImportBusinessPartnersResult();

        // 1. Parse the single flat file
        var rows = await _parserResolver
            .Resolve(command.FileName)
            .ParseAsync(command.FileStream, command.FileName, ct);

        // 2. Map each flat row directly to a BusinessPartner domain object
        var businessPartners = rows
            .Select(BusinessPartnerMapper.ToDomain)
            .ToList();

        result.TotalRows = businessPartners.Count;

        // 3. Login to Service Layer
        await _slClient.LoginAsync(ct);

        try
        {
            int rowNum = 1;
            foreach (var bp in businessPartners)
            {
                var rowResult = new ImportRowResult
                {
                    RowNumber = rowNum++,
                    CardCode  = bp.CardCode
                };
                result.RowResults.Add(rowResult);

                // 4. Validate
                var validation = await _validator.ValidateAsync(bp, ct);
                if (!validation.IsValid)
                {
                    rowResult.Status  = ImportRowStatus.Error;
                    rowResult.Message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
                    result.ErrorCount++;

                    if (command.StopOnError) break;
                    continue;
                }

                // 5. Import based on mode
                try
                {
                    bool exists = command.Mode != ImportMode.AddOnly
                        && await _slClient.BusinessPartnerExistsAsync(bp.CardCode, ct);

                    if (command.Mode == ImportMode.AddOnly && exists)
                    {
                        rowResult.Status  = ImportRowStatus.Skipped;
                        rowResult.Message = "CardCode already exists (AddOnly mode).";
                        result.SkippedCount++;
                        continue;
                    }

                    if (command.Mode == ImportMode.UpdateOnly && !exists)
                    {
                        rowResult.Status  = ImportRowStatus.Skipped;
                        rowResult.Message = "CardCode not found (UpdateOnly mode).";
                        result.SkippedCount++;
                        continue;
                    }

                    if (exists)
                        await _slClient.UpdateBusinessPartnerAsync(bp, ct);
                    else
                        await _slClient.CreateBusinessPartnerAsync(bp, ct);

                    rowResult.Status = ImportRowStatus.Success;
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    rowResult.Status  = ImportRowStatus.Error;
                    rowResult.Message = ex.Message;
                    result.ErrorCount++;

                    if (command.StopOnError) break;
                }
            }
        }
        finally
        {
            await _slClient.LogoutAsync(ct);
        }

        return result;
    }
}
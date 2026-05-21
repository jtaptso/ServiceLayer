using MediatR;
using ServiceLayer.DTW.Domain.Enums;

namespace ServiceLayer.DTW.Application.UseCases.ImportBusinessPartners;

public record ImportBusinessPartnersCommand(
    Stream     FileStream,
    string     FileName,
    ImportMode Mode,
    bool       StopOnError
) : IRequest<ImportBusinessPartnersResult>;
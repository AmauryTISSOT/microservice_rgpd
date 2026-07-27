using MicroserviceRgpd.Core.ContributorAggregate;

namespace MicroserviceRgpd.UseCases.Contributors.Get;

public record GetContributorQuery(ContributorId ContributorId) : IQuery<Result<ContributorDto>>;

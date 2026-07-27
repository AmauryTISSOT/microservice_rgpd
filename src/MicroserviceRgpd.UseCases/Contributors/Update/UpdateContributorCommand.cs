using MicroserviceRgpd.Core.ContributorAggregate;

namespace MicroserviceRgpd.UseCases.Contributors.Update;

public record UpdateContributorCommand(ContributorId ContributorId, ContributorName NewName) : ICommand<Result<ContributorDto>>;

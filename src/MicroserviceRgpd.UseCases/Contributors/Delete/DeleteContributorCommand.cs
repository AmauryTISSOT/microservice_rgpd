using MicroserviceRgpd.Core.ContributorAggregate;

namespace MicroserviceRgpd.UseCases.Contributors.Delete;

public record DeleteContributorCommand(ContributorId ContributorId) : ICommand<Result>;

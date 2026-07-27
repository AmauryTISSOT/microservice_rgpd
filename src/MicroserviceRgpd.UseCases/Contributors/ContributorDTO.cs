using MicroserviceRgpd.Core.ContributorAggregate;

namespace MicroserviceRgpd.UseCases.Contributors;
public record ContributorDto(ContributorId Id, ContributorName Name, PhoneNumber PhoneNumber);

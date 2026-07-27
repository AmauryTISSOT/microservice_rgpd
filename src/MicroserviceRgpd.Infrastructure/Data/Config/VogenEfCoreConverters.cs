using MicroserviceRgpd.Core.ContributorAggregate;
using Vogen;

namespace MicroserviceRgpd.Infrastructure.Data.Config;

[EfCoreConverter<ContributorId>]
[EfCoreConverter<ContributorName>]
internal partial class VogenEfCoreConverters;

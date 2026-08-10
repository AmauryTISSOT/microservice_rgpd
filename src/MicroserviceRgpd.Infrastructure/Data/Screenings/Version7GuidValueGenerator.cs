using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace MicroserviceRgpd.Infrastructure.Data.Screenings;

/// <summary>
/// Un identifiant forgé en <b>GUID v7</b>, pour les clés qu'EF Core engendre lui-même. Il sert la
/// clé de substitution d'une ligne de <c>screened_columns</c>, et rien d'autre à ce jour.
/// </summary>
/// <remarks>
/// <b>Le même choix que celui de l'identité d'un rapport, et pour la même raison mesurée</b> :
/// ordonnée dans le temps, donc sans fragmentation d'index à l'insertion. Ici elle pèse plus
/// qu'ailleurs — un dépôt écrit <b>cinq mille lignes d'un coup</b>, sous un budget d'écriture de
/// trois secondes, et le générateur par défaut d'EF Core rendrait des identifiants dispersés que
/// l'index primaire paierait ligne à ligne.
/// </remarks>
internal sealed class Version7GuidValueGenerator : ValueGenerator<Guid>
{
  /// <summary>
  /// La valeur est <b>définitive</b> : elle n'est pas un jeton d'attente que la base remplacerait.
  /// </summary>
  public override bool GeneratesTemporaryValues => false;

  /// <inheritdoc />
  public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
}

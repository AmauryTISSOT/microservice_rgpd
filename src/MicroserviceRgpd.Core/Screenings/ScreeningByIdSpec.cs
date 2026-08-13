namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Un rapport <b>nommé</b>, avec <b>toutes</b> ses colonnes : la lecture d'un archivé.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle vise un rapport par son identité, ce qu'aucune lecture du courant ne fait.</b> C'est
/// la seule façon de lire un archivé — il n'est le plus récent de rien — et c'est aussi pourquoi
/// elle ne sert <b>jamais</b> à écrire : le geste d'arbitrage passe par
/// <see cref="CurrentScreeningColumnSpec"/>, qui ne sait charger que le courant. Une écriture qui
/// emprunterait celle-ci arbitrerait un archivé.
/// </para>
/// <para>
/// <b>Elle rend les colonnes, toutes</b>, comme <see cref="CurrentScreeningSpec"/> : les comptes
/// d'un rapport se font sur l'ensemble de ses lignes, et filtrer les <c>Unflagged</c> à la lecture
/// rétablirait l'<c>Omission silencieuse</c> un cran plus bas que l'écran — un archivé se lit
/// <b>en entier</b> ou ne se lit pas.
/// </para>
/// </remarks>
public sealed class ScreeningByIdSpec : SingleResultSpecification<Screening>
{
  /// <param name="screening">Le rapport qu'on ouvre.</param>
  public ScreeningByIdSpec(ScreeningId screening)
  {
    Query.Where(candidate => candidate.Id == screening)
      .Include(candidate => candidate.Columns)
      .AsNoTracking();
  }
}

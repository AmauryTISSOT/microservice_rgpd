using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.DepositListing;

/// <summary>
/// Ingère le collage, le fait dépister, assemble le rapport et l'écrit — <b>dans cet ordre et sans
/// rien différer</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le geste qui assemble, jamais le moteur.</b> Ce que rend un <c>IScreeningEngine</c>
/// est une <c>ScreenedListing</c> : une ligne par colonne, et l'identité du moteur qui les a
/// produites. Il y manque ce que le moteur n'a pas à décider — l'identité du rapport, l'instant du
/// lancement, le nom de base et le dialecte —, et c'est ici que les quatre se posent.
/// </para>
/// <para>
/// ⚠️ <b>L'identité du moteur est celle qui a répondu</b>, prise sur ce qu'il rend et jamais lue à
/// côté de l'appel : un moteur servi apprend la version qu'on lui sert au moment où il répond, et
/// une propriété posée à côté aurait dit la version configurée.
/// </para>
/// <para>
/// <b>Le nom de base et le dialecte sont recopiés du relevé, jamais vérifiés.</b>
/// <c>Enregistré, jamais vérifié</c> : le service ne sait pas d'où vient ce relevé, et un relevé sincère
/// mais tiré de la base de recette est indiscernable du bon. Aucun mécanisme n'attrape ce cas ici,
/// et aucun ne doit prétendre l'attraper.
/// </para>
/// </remarks>
/// <param name="screenings">Le dépôt des rapports. Un dépôt neuf à chaque collage : rien ne fusionne.</param>
/// <param name="engine">
/// Le port du dépistage. ⚠️ Le geste ignore s'il parle à des règles locales ou à un moteur servi, et
/// c'est la couture de réversibilité d'ADR-0004 — pas une couture de test.
/// </param>
/// <param name="clock">L'horloge. C'est elle, et elle seule, qui décide quel rapport sera le courant.</param>
public sealed class DepositListingHandler(
  IRepository<Screening> screenings,
  IScreeningEngine engine,
  TimeProvider clock)
  : ICommandHandler<DepositListingCommand, Result<ScreeningId>>
{
  /// <summary>
  /// La fin de tout refus. ⚠️ <b>Sans elle, l'<c>Operator</c> cherche quoi défaire avant de
  /// recoller</b> — et un service qui laisse croire qu'il faut nettoyer se fait nettoyer à la main.
  /// </summary>
  private const string NothingWasIngested =
    "Aucune colonne n'a été ingérée : relancez la requête de relevé et recollez-la entière.";

  /// <inheritdoc />
  public async ValueTask<Result<ScreeningId>> Handle(
    DepositListingCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (Weighed(command.Paste) is { } weight && weight > DepositListingCommand.MaxPasteBytes)
    {
      // ⚠️ Le poids se lit AVANT l'ingestion, et c'est autant une question de phrase que de coût :
      // un collage de trente mégaoctets refusé par son format aurait rendu « ligne de fin illisible »
      // à propos d'un relevé parfaitement formé, simplement trop lourd — l'Operator aurait recollé
      // le même indéfiniment.
      return Result<ScreeningId>.Invalid(TooHeavy(weight));
    }

    var ingested = ColumnListingIngestion.Ingest(command.Paste);

    if (ingested.Refusal is { } refusal)
    {
      // ⚠️ Refusé EN BLOC : aucune colonne n'est rendue, pas même les lignes lisibles. Un rapport
      // bâti sur 99,9 % d'un relevé se lirait comme complet, et les lignes perdues seraient
      // précisément celles que personne ne relirait jamais. Le coût du refus est faible et
      // réversible — l'Operator relance sa requête, il ne perd aucun arbitrage.
      return Result<ScreeningId>.Invalid(RefusalOf(refusal));
    }

    var listing = ingested.Listing!;

    var screened = await engine.ScreenAsync(listing, cancellationToken);

    var screening = Screening.Of(
      ScreeningId.Next(),
      listing.Database,
      listing.Dialect,
      screened.Engine,
      listing.DeclaredColumnCount,
      screened.Columns,
      clock.GetUtcNow());

    await screenings.AddAsync(screening, cancellationToken);

    return screening.Id;
  }

  /// <summary>
  /// Le refus, redit à l'humain qui a collé : le cas, la ligne qu'il peut retrouver dans son propre
  /// collage, ce qui s'y trouvait, et <b>ce que le format attendait</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'attente est dite, et ce n'est pas une politesse.</b> Un refus qui nomme le cas sans
  /// dire ce qui était attendu fait recommencer au hasard, sur un collage d'un mégaoctet que
  /// l'<c>Operator</c> ne relira pas à l'œil.
  /// </remarks>
  private static ValidationError RefusalOf(ColumnListingRefusal refusal)
  {
    return new ValidationError
    {
      Identifier = nameof(DepositListingCommand.Paste),
      ErrorCode = refusal.Cause.Name,
      ErrorMessage =
        $"Relevé refusé en bloc — {refusal.Cause.FrenchLabel} (cas {refusal.Cause.CaseNumber}), "
        + $"ligne {refusal.LineNumber} : « {refusal.Observed} ». Attendu : {refusal.Cause.Expectation}. "
        + NothingWasIngested,
      Severity = ValidationSeverity.Error,
    };
  }

  /// <summary>
  /// Le refus du <b>poids</b>, dit dans la même forme que les neuf autres — <b>sans numéro de
  /// cas</b>, parce qu'il n'en est pas un.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Il ne prend pas de numéro, et c'est délibéré.</b> Les neuf cas sont ceux du contrat de
  /// <em>format</em> ; le poids est une borne du geste. Lui donner un dixième numéro ferait mentir
  /// toute la documentation qui compte neuf cas, et donnerait à un plafond d'exploitation — qui peut
  /// bouger — le même rang qu'à des règles de format qui, elles, ne bougent pas.
  /// </para>
  /// <para>
  /// <b>Le poids est dit en mégaoctets</b>, pas en octets : « 8 388 608 octets » n'est pas un
  /// nombre qu'un humain rapporte à ce qu'il a sous les yeux.
  /// </para>
  /// </remarks>
  private static ValidationError TooHeavy(long weight)
  {
    return new ValidationError
    {
      Identifier = nameof(DepositListingCommand.Paste),
      ErrorCode = "PasteTooHeavy",
      ErrorMessage =
        $"Relevé refusé en bloc — collage trop volumineux : {ReceivedInMegabytes(weight)} reçus, "
        + $"{InMegabytes(DepositListingCommand.MaxPasteBytes)} au plus. Attendu : un relevé de "
        + $"{ColumnListing.MaxColumns.ToString(CultureInfo.InvariantCulture)} colonnes — le plafond — "
        + "pèse environ 4 Mo ; au-delà du plafond d'octets le collage est refusé plutôt que tronqué. "
        + NothingWasIngested,
      Severity = ValidationSeverity.Error,
    };
  }

  /// <summary>Le poids du collage en octets UTF-8, ou <c>null</c> s'il n'y a rien à peser.</summary>
  /// <remarks>
  /// ⚠️ <b>UTF-8 et non le compte de caractères.</b> Un commentaire de table en cyrillique ou en
  /// japonais pèse deux à trois fois son compte de <c>char</c> ; peser des caractères aurait laissé
  /// passer un collage de vingt mégaoctets réels, c'est-à-dire précisément le corps que le plafond
  /// existe pour borner.
  /// </remarks>
  private static long? Weighed(string? paste)
  {
    return paste is null ? null : Encoding.UTF8.GetByteCount(paste);
  }

  /// <summary>
  /// Le poids reçu, <b>arrondi vers le haut</b> au dixième de mégaoctet.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'arrondi va vers le haut pour que les deux nombres de la phrase ne se lisent jamais
  /// pareil.</b> Un collage de 8 388 900 octets arrondi au plus proche rendrait « 8 Mo reçus, 8 Mo au
  /// plus » — une phrase qui refuse en donnant deux fois le même chiffre, et qui laisse
  /// l'<c>Operator</c> devant un refus qu'il ne peut pas croire. Le dixième perdu est le prix d'un
  /// message qui se tient.
  /// </remarks>
  private static string ReceivedInMegabytes(long bytes)
  {
    var megabytes = Math.Ceiling((double)bytes / 1024 / 1024 * 10) / 10;

    return string.Create(CultureInfo.GetCultureInfo("fr-FR"), $"{megabytes:0.#} Mo");
  }

  private static string InMegabytes(long bytes)
  {
    return string.Create(
      CultureInfo.GetCultureInfo("fr-FR"),
      $"{(double)bytes / 1024 / 1024:0.#} Mo");
  }
}

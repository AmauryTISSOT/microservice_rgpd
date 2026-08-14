using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// Date la remise au <c>EvidenceLog</c> et <b>détruit</b> les pièces du droit remis.
/// </summary>
/// <remarks>
/// <para>
/// <b>L'ordre est : écrire le dossier, consigner, puis détruire</b> — et il n'est pas
/// interchangeable. Détruire d'abord aurait effacé la réponse due à la personne sans qu'aucune
/// ligne ne dise qu'elle avait été rendue ; les pièces sont donc jetées en dernier, quand la preuve
/// est déjà écrite. Une panne entre les deux laisse des pièces détenues un moment de trop —
/// visible, et réparable — plutôt qu'une réponse perdue sans trace.
/// </para>
/// <para>
/// <b>Le « 2 sur 6 » est écrit ici, et il s'arrête ici.</b> Son lecteur est le contrôle, qui juge
/// une pratique ; la <c>DeliveryLetter</c> n'en porte pas un chiffre. Le dénominateur est <b>gardé</b>
/// plutôt que relu plus tard : le recensement vieillit exprès, et le relire dans trois ans jugerait
/// la pratique d'hier au paysage de demain.
/// </para>
/// <para>
/// <b>Rien à détruire n'est pas une panne.</b> Un droit dont aucune lecture n'a rien rapporté se
/// remet quand même : la <c>DeliveryLetter</c> seule est déjà une réponse, et elle nomme les systèmes
/// que cette réponse ne couvre pas.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="manifest">Le catalogue, relu à chaque passage — il donne le dénombrement du jour.</param>
/// <param name="retrieved">Les pièces détenues, hors de l'agrégat : c'est ici qu'elles meurent.</param>
/// <param name="evidenceLog">La matière de preuve, en ajout seul.</param>
/// <param name="clock">L'horloge, injectée pour que la date d'une remise se dicte en test.</param>
public sealed class DeclareDeliveryHandler(
  IRepository<Case> cases,
  IReadRepository<DeclaredSystem> manifest,
  IRetrievedData retrieved,
  IEvidenceLog evidenceLog,
  TimeProvider clock)
  : ICommandHandler<DeclareDeliveryCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(DeclareDeliveryCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result.NotFound();
    }

    var claim = opened.Claims.SingleOrDefault(one => one.Right == command.Right);

    if (claim is null || claim.DeliveryTakenOn is null || claim.DeliveryDeclaredOn is not null)
    {
      // Le dossier ne porte pas ce droit, personne n'a jamais pris sa remise, ou elle est déjà
      // déclarée. Aucun des trois n'est une programmation fautive : un écran affiché il y a une
      // minute peut nommer un geste qu'un autre vient de faire.
      return Result.NotFound();
    }

    var delivery = Delivery.Of(
      opened,
      command.Right,
      Manifest.Of(await manifest.ListAsync(cancellationToken)),
      await retrieved.HeldForAsync(command.Case, cancellationToken));

    // La ligne de preuve est forgée d'abord : c'est elle qui exige un nom, et rien ne doit bouger si
    // la signature manque. La règle vit dans le type de la preuve, jamais ici.
    EvidenceLogEntry signed;

    try
    {
      signed = EvidenceLogEntry.DeliveryDeclared(
        command.Case,
        clock.GetUtcNow(),
        command.Right,
        // Ce que la réponse couvrait : les systèmes dont une pièce est jointe, et eux seuls. Une
        // pièce vide est une réponse datée, mais elle ne couvre rien.
        delivery.DeliveryLetter.Joined.Count,
        // Le dénominateur est pris sur LE MÊME ensemble que le numérateur — celui que la page de
        // garde énumère —, et non sur le catalogue du jour : mesurer l'un contre l'autre écrirait
        // « 6 sur 5 » le jour où quelqu'un retire du catalogue un système que ce dossier portait.
        delivery.DeliveryLetter.RecordedSystemCount,
        Signatory.Operator(command.SignedBy, SignerVerification.Unauthenticated));
    }
    catch (ArgumentException refusal)
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(DeclareDeliveryCommand.SignedBy),
        // Le message des types du domaine porte le nom du paramètre entre parenthèses, façon
        // `ArgumentException` : il est retiré, l'écran nommant déjà le champ fautif à côté de sa case.
        ErrorMessage = refusal.Message.Split(" (Parameter")[0],
        Severity = ValidationSeverity.Error,
      });
    }

    if (!opened.DeclareDelivered(command.Right, signed.OccurredAt))
    {
      return Result.NotFound();
    }

    await cases.UpdateAsync(opened, cancellationToken);

    await evidenceLog.AppendAsync(signed, cancellationToken);

    // En dernier, et sans réécrire le dossier : c'est très exactement pourquoi ces pièces vivent
    // hors de l'agrégat. Le séjour est inévitable ; il s'arrête ici.
    await retrieved.DiscardAsync(command.Case, command.Right, cancellationToken);

    return Result.Success();
  }
}

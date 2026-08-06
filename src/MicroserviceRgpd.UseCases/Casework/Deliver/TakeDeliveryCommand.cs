using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// <b>Premier geste</b> de la remise : l'<c>Operator</c> télécharge l'archive d'un droit, pour
/// l'ouvrir et voir ce qu'elle contient.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il ne date rien et ne détruit rien.</b> Un téléchargement n'est pas une remise : dater la
/// preuve ici l'aurait datée à l'instant où quelqu'un a cliqué pour <em>vérifier</em>, et détruit
/// les pièces avant qu'il n'ait pu constater que l'archive était vide. La preuve attend le second
/// geste, qui est une affirmation.
/// </para>
/// <para>
/// <b>Il est répétable.</b> Retélécharger n'est pas un fait nouveau ; seul le <b>premier</b> instant
/// est retenu sur le <c>Claim</c>, et il n'y sert qu'à faire remonter dans la file la remise prise
/// et jamais déclarée.
/// </para>
/// <para>
/// <b>Il n'est pas signé.</b> Rien n'est affirmé : la signature appartient au geste qui produit une
/// issue, et l'exiger ici aurait fait signer un humain pour ouvrir un fichier.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier dont on prend la remise.</param>
/// <param name="Right">Le droit remis, et lui seul — deux droits sont deux réponses.</param>
public sealed record TakeDeliveryCommand(CaseId Case, DataSubjectRight Right)
  : ICommand<Result<DeliveryArchive>>;

using System.Collections.Concurrent;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// Signale un désaccord <c>Manifest</c>/<c>Adapter</c> <b>une seule fois</b>, et le tait ensuite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le grain est le déploiement, et c'est l'enregistrement en singleton qui le tient.</b> Un
/// secret périmé est un fait du service entier : le signaler une fois par dossier ferait dépendre
/// le volume du bruit du nombre de demandes en cours, qui n'en dit rien, et noierait la seule chose
/// à réparer.
/// </para>
/// <para>
/// <b>Ce qui est tu n'est pas perdu.</b> Chaque tentative refusée entre au <c>Ledger</c>, datée,
/// dans le dossier au titre duquel elle est partie : la preuve les garde toutes, l'exploitation
/// n'en lit qu'une.
/// </para>
/// <para>
/// <b>Rien ne se réarme.</b> Un désaccord signalé ne se re-signale pas quand l'<c>Adapter</c>
/// recommence à répondre : le service n'a aucun moment où il saurait dire « c'est réparé » — il ne
/// le saurait qu'en réessayant, et un signal de retour à la normale serait une affirmation qu'il ne
/// peut pas tenir. La mémoire meurt avec le processus, ce qui fait du redémarrage — le même geste
/// que la rotation du secret — le seul réarmement, et il est délibéré.
/// </para>
/// </remarks>
public sealed class AdapterDisagreements(ILogger<AdapterDisagreements> logger) : IAdapterDisagreements
{
  /// <summary>
  /// Ce qui a déjà été dit. La paire (système, refus) plutôt que le seul système : un
  /// <c>Adapter</c> dont le secret est rejeté <b>et</b> qui ne sert pas le système demandé a deux
  /// choses à dire, et taire la seconde derrière la première ferait réparer une panne pour en
  /// découvrir une autre.
  /// </summary>
  private readonly ConcurrentDictionary<(string System, string Refusal), bool> _said = new();

  /// <inheritdoc />
  public void Signal(DeclaredSystemId declaredSystem, AdapterVerdict refusal)
  {
    ArgumentNullException.ThrowIfNull(refusal);

    if (!refusal.IsRefusal)
    {
      throw new ArgumentException(
        $"« {refusal.Name} » n'est pas un refus : un Adapter qui répond n'est pas en désaccord avec "
        + "le Manifest.",
        nameof(refusal));
    }

    if (!_said.TryAdd((declaredSystem.Value, refusal.Name), true))
    {
      return;
    }

    logger.LogError(
      "Désaccord Manifest/Adapter sur le système « {DeclaredSystem} » : {Refusal}. Signalé une "
      + "seule fois, au grain du déploiement ; les tentatives, elles, sont datées au Ledger de "
      + "chaque dossier.",
      declaredSystem.Value,
      refusal.FrenchLabel);
  }
}

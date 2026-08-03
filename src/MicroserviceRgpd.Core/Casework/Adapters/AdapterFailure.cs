namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// L'<c>Adapter</c> n'a rendu <b>ni réponse ni refus</b> : serveur muet, statut que le contrat ne
/// prévoit pas, corps illisible, ou différé sans échéance déclarée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle se distingue d'un refus, et la distinction est tout son propos.</b> Un refus est une
/// réponse claire de l'<c>Adapter</c> — on la consigne, on la signale, et l'exploitant sait où
/// aller. Une panne ne dit rien de plus que « ça n'a pas marché », et elle n'a donc aucune valeur
/// dans <see cref="AdapterOutcome"/> : la mettre au même rang ferait entrer au vocabulaire fermé du
/// <c>Ledger</c> un fait dont personne ne sait quoi conclure.
/// </para>
/// <para>
/// <b>Un différé sans échéance lisible est une panne, pas un différé.</b> Le service ne complète
/// pas : une date qu'il aurait inventée deviendrait, une heure plus tard, une preuve que personne
/// n'a déclarée.
/// </para>
/// </remarks>
public class AdapterFailure : Exception
{
  /// <inheritdoc />
  public AdapterFailure()
  {
  }

  /// <inheritdoc />
  public AdapterFailure(string message)
    : base(message)
  {
  }

  /// <inheritdoc />
  public AdapterFailure(string message, Exception innerException)
    : base(message, innerException)
  {
  }
}

namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// L'entrecontrôle de deux moteurs sur un même texte : ce que le service rend une fois les avis
/// confrontés — le verdict, l'urgence à le relire, l'état du service et sa justification.
/// </summary>
/// <remarks>
/// <para>
/// <b>La règle vit dans le domaine</b>, et pas dans le use case qui appelle les moteurs : c'est la
/// seule chose que le service décide vraiment, et elle se teste sans réseau, sans base et sans
/// container. Un handler qui l'aurait absorbée la rendrait indissociable de HTTP.
/// </para>
/// <para>
/// <b>Elle ne nomme aucun moteur.</b> Il y a l'avis qui fait verdict et l'avis témoin, deux rôles ;
/// qu'un LLM tienne le premier et un lexique le second est un fait d'infrastructure. Un troisième
/// moteur, ou l'abandon du lexique, laisse cette règle intacte.
/// </para>
/// <para>
/// <b>Ordre d'évaluation fixé : divergence d'abord, confiance ensuite.</b> La divergence est le seul
/// signal à ne rien devoir à l'auto-évaluation du moteur principal ; elle l'emporte donc, et une
/// confiance haute ne rachète jamais un désaccord.
/// </para>
/// </remarks>
/// <param name="Qualification">
/// Le verdict rendu. Celui du moteur principal en marche nominale — le témoin est
/// <b>détecteur, jamais contributeur</b> — et celui du témoin quand le principal n'a rien rendu.
/// Les deux rôles ne coexistent jamais : l'union de deux avis serait indéfinissable,
/// <see cref="DataSubjectRight.OutOfScope"/> étant exclusif.
/// </param>
/// <param name="ReviewSignal">L'urgence à relire, déduite de la confrontation des deux avis.</param>
/// <param name="Degraded">Vrai dès qu'un des deux avis manque, <b>quel que soit celui qui manque</b>.</param>
/// <param name="Justification">
/// La phrase que le moteur principal oppose à l'opérateur, quand il en a rendu une. Absente en repli
/// témoin : lui en fabriquer une mentirait à l'opérateur au moment où le service se trompe le plus.
/// </param>
public sealed record Corroboration(
  Qualification Qualification,
  ReviewSignal ReviewSignal,
  bool Degraded,
  string? Justification)
{
  /// <summary>
  /// Confronte les deux avis, ou refuse quand il n'y en a aucun.
  /// </summary>
  /// <param name="verdict">
  /// L'avis du moteur principal, celui qui fait verdict — absent quand ce moteur n'a rien rendu,
  /// quelle qu'en soit la raison.
  /// </param>
  /// <param name="witness">
  /// L'avis témoin, celui qui contrôle — absent quand le moteur qui le rend n'a rien rendu.
  /// </param>
  /// <exception cref="ArgumentException">
  /// Les deux avis sont absents : il n'y a rien à qualifier, et forger un verdict que personne n'a
  /// prononcé serait pire que de refuser.
  /// </exception>
  public static Corroboration Between(QualificationOpinion? verdict, QualificationOpinion? witness)
  {
    if (verdict is null && witness is null)
    {
      throw new ArgumentException(
        "Aucun moteur n'a rendu d'avis : il n'y a rien à corroborer, et rien à qualifier.",
        nameof(verdict));
    }

    if (verdict is null)
    {
      // Repli sur le témoin : il tient lieu de verdict, le signal ne peut être que « à relire », et
      // la réponse est muette — le témoin ne justifie rien, et le service n'invente pas pour lui.
      return new Corroboration(witness!.Qualification, ReviewSignal.NeedsReview, Degraded: true, Justification: null);
    }

    if (witness is null)
    {
      // Le verdict est normal, mais il n'a reçu aucun contrôle. Le dire est ce qui empêche un
      // moteur témoin mort de s'éteindre en silence.
      return new Corroboration(
        verdict.Qualification, ReviewSignal.NeedsReview, Degraded: true, verdict.Justification);
    }

    return new Corroboration(
      verdict.Qualification,
      SignalOf(verdict, witness),
      Degraded: false,
      verdict.Justification);
  }

  /// <summary>
  /// La table de corroboration, dans son ordre d'évaluation : divergence, puis confiance.
  /// </summary>
  /// <remarks>
  /// L'égalité des qualifications est une <b>égalité d'ensembles</b>, insensible à l'ordre : le
  /// contraire ferait dépendre le signal de l'ordre dans lequel un moteur énumère ses droits, qui
  /// n'a aucun sens.
  /// </remarks>
  private static ReviewSignal SignalOf(QualificationOpinion verdict, QualificationOpinion witness)
  {
    if (!verdict.Qualification.Equals(witness.Qualification))
    {
      return ReviewSignal.Contested;
    }

    // La confiance n'est lue qu'ici, et n'est jamais un seuil : elle ne remplace aucun verdict, elle
    // décide seulement du signal qui accompagne un verdict déjà rendu.
    return verdict.DeclaredConfidence == Qualifications.DeclaredConfidence.High
      ? ReviewSignal.Corroborated
      : ReviewSignal.NeedsReview;
  }
}

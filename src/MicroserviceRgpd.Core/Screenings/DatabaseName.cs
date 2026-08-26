namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le nom de base d'un <see cref="Screening"/>, ramené à ce qu'il doit être : <b>jamais un
/// chemin</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce champ quitte le service</b> — il est le seul de la <see cref="PersonalDataMap"/> à dire
/// d'où le fichier vient, et il en nomme jusqu'au fichier CSV. Côté SQLite, le SGBD ne connaît sa
/// base que par son chemin, et un dossier parent est très exactement l'endroit où l'on écrit le nom
/// du client : <c>/srv/clients/mutuelle-des-cheminots/galette.sqlite</c> publierait l'arborescence
/// interne et le nom du client dans un fichier qu'on expédie par courriel.
/// </para>
/// <para>
/// <b>Le dossier parent est donc écarté, et cela <i>referme</i> la borne de cent caractères au lieu
/// de la rouvrir</b> : un chemin de cent cinquante caractères ne demande plus qu'on élargisse le
/// plafond du nom, il demande qu'on n'en garde que le dernier segment. La réduction a lieu
/// <b>avant</b> le contrôle de longueur, sans quoi le cas courant qu'elle traite serait refusé avant
/// d'être traité.
/// </para>
/// <para>
/// ⚠️ <b>Elle vaut sur les deux chemins d'entrée, et non sur le seul SQLite.</b> La règle est « ce
/// champ ne porte jamais un chemin » et non « ce dialecte-ci se nettoie » : la brancher sur le
/// dialecte déclaré aurait fait dépendre une garantie de confidentialité d'une chaîne que le relevé
/// recopie sans jamais la vérifier.
/// </para>
/// <para>
/// <b>Les deux séparateurs sont traités, quel que soit l'hôte.</b> Le service tourne sous Linux et
/// le relevé peut venir d'un poste Windows : ne connaître que le séparateur de la machine qui lit
/// aurait laissé passer <c>C:\clients\acme\galette.db</c> en entier.
/// </para>
/// </remarks>
internal static class DatabaseName
{
  private static readonly char[] Separators = ['/', '\\'];

  /// <summary>
  /// Le dernier segment d'un nom de base, ou le nom tel quel s'il n'en porte aucun.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les séparateurs de fin tombent d'abord, et ce n'est pas de la cosmétique.</b>
  /// <c>/srv/clients/acme/</c> n'a pas de dernier segment : sans cette coupe, la règle aurait rendu
  /// le chemin <b>entier</b> faute de trouver quoi garder, c'est-à-dire échoué exactement là où elle
  /// sert.
  /// </para>
  /// <para>
  /// <b>Un nom qui n'est <i>que</i> des séparateurs est rendu tel quel</b> — il ne publie aucune
  /// arborescence, et c'est tout ce qui est demandé ici. Le rendre vide aurait transformé un nom
  /// absurde en « nom absent », c'est-à-dire fait dire au refus autre chose que le problème.
  /// </para>
  /// </remarks>
  internal static string? WithoutAnyPath(string? database)
  {
    var trimmed = database?.Trim();

    if (string.IsNullOrEmpty(trimmed))
    {
      return database;
    }

    var withoutTrailingSeparators = trimmed.TrimEnd(Separators);

    if (withoutTrailingSeparators.Length == 0)
    {
      return trimmed;
    }

    var lastSeparator = withoutTrailingSeparators.LastIndexOfAny(Separators);

    return lastSeparator < 0
      ? withoutTrailingSeparators
      : withoutTrailingSeparators[(lastSeparator + 1)..];
  }
}

namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce que l'<b>appel au faux secret</b> a appris d'un <c>Adapter</c> : garde-t-il sa
/// porte, ou est-il <b>nu</b> — c'est-à-dire ouvert à qui l'atteint ?
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est un résultat d'exploitation, jamais une affaire de dossier.</b> Un <c>Adapter</c> nu est
/// un fait du déploiement : il vaut pour tous les <c>Case</c> à la fois, et rien de ce qu'il dit
/// n'appartient à l'un d'eux. L'<c>EvidenceLog</c> n'en sait donc rien, et n'a pas à en savoir quelque
/// chose.
/// </para>
/// <para>
/// <b>Seul <see cref="Naked"/> est une preuve.</b> Un <c>Adapter</c> qui a <b>travaillé</b> sous un
/// secret qu'il aurait dû refuser a démontré, en un aller-retour, que le secret ne le protège pas.
/// Les deux autres valeurs ne démontrent rien d'aussi net, et elles ne le prétendent pas :
/// <see cref="Guarded"/> dit que la porte a été fermée <b>ce jour-là, sur ce chemin-là</b>, jamais
/// que le périmètre réseau — l'autre moitié du dispositif, celle qu'aucune sonde ne mesure d'ici —
/// est en place.
/// </para>
/// <para>
/// ⚠️ <b>La sonde ne lit jamais le corps de ce qu'elle reçoit.</b> Un <c>200</c> rendu à un secret
/// faux porte, par construction, des données personnelles servies à qui n'aurait pas dû les
/// obtenir : les lire ferait entrer dans le service, au titre d'une vérification, exactement ce que
/// la vérification vient dénoncer. Le statut suffit à conclure.
/// </para>
/// </remarks>
public sealed class AdapterExposure : SmartEnum<AdapterExposure>
{
  /// <summary>
  /// L'<c>Adapter</c> a <b>servi</b> — ou pris en charge — un appel présenté avec un secret faux. Il
  /// est ouvert : le secret partagé ne le garde pas, et la seule chose qui le sépare encore de
  /// l'extérieur est le périmètre réseau, qui n'est plus la moitié d'un dispositif mais sa totalité.
  /// <para>
  /// Le <c>202</c> y est rangé avec le <c>200</c> : différer, c'est <b>avoir pris le travail</b>.
  /// Les séparer aurait fait passer pour prudent un <c>Adapter</c> qui accepte des ordres de
  /// n'importe qui, au motif qu'il les exécute plus tard.
  /// </para>
  /// </summary>
  public static readonly AdapterExposure Naked = new(nameof(Naked), 0, "nu");

  /// <summary>
  /// L'<c>Adapter</c> a refusé le secret, comme le contrat l'exige. <b>C'est la réponse attendue</b>,
  /// et elle ne vaut que pour ce qu'elle dit : la moitié « secret partagé » du dispositif tient.
  /// </summary>
  public static readonly AdapterExposure Guarded = new(nameof(Guarded), 1, "gardé");

  /// <summary>
  /// La sonde n'a rien démontré : l'<c>Adapter</c> a répondu autre chose — qu'il ne sert pas ce
  /// système, ou rien du tout.
  /// <para>
  /// <b>Cette valeur existe pour ne pas mentir par silence.</b> Sans elle, un <c>Adapter</c> muet se
  /// rangerait avec les gardés, et l'exploitant lirait une porte fermée là où personne n'a frappé.
  /// </para>
  /// </summary>
  public static readonly AdapterExposure Inconclusive = new(nameof(Inconclusive), 2, "sans conclusion");

  private AdapterExposure(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'humain qui exploite. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}

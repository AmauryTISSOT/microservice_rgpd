namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Un endroit où des données personnelles vivent chez le client, <b>parce qu'un humain l'a
/// déclaré</b>. Unité de recensement, jamais de déploiement : le paysage du client, pas sa
/// topologie — un même serveur peut porter trois systèmes déclarés s'il y a trois responsables et
/// trois régimes de conservation.
/// <para>
/// L'adjectif n'est pas décoratif. C'est parce que ces systèmes sont <b>déclarés</b>, et non
/// découverts, que l'<c>Omission silencieuse</c> est le risque cardinal de ce contexte : un
/// <c>Adapter</c> ne saurait décrire que les systèmes qui en ont un, c'est-à-dire précisément ceux
/// dont on n'a pas besoin qu'on nous parle.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun champ ne nomme une table, une colonne ni un champ du client.</b> Le grain est le
/// système, et il est fermé ici : c'est ce qui met le service à l'abri de pourrir en silence
/// lorsque le schéma du client change, et ce qui laisse à l'application la seule chose qu'elle
/// sache arbitrer — la précision du contenu.
/// </para>
/// <para>
/// <b>Il vieillit exprès</b> : <see cref="DeclaredOn"/> date la dernière fois qu'un humain a
/// affirmé ceci, et le service n'écrit jamais « tous les systèmes » mais « les systèmes déclarés au
/// tant ». Un catalogue auto-engendré aurait l'air frais tout en étant faux.
/// </para>
/// </remarks>
public sealed class DeclaredSystem : IAggregateRoot
{
  /// <summary>
  /// Les capacités, par leur <b>nom canonique anglais</b> et dans l'ordre du catalogue. La forme
  /// stockée est celle des noms plutôt que celle des membres : c'est elle qui descend telle quelle
  /// dans la colonne, sans qu'aucun ordinal d'énumération ne devienne un élément du schéma.
  /// </summary>
  private string[] _capabilities;

  private DeclaredSystem(
    DeclaredSystemId id,
    SystemLabel label,
    SystemContents contents,
    string[] capabilities,
    AdapterAddress? adapterAddress,
    DateTimeOffset declaredOn)
  {
    Id = id;
    Label = label;
    Contents = contents;
    _capabilities = capabilities;
    AdapterAddress = adapterAddress;
    DeclaredOn = declaredOn;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private DeclaredSystem()
  {
    _capabilities = [];
  }

  /// <summary>L'identifiant choisi par l'humain qui déclare, et celui que l'<c>Adapter</c> recevra.</summary>
  public DeclaredSystemId Id { get; private set; }

  /// <summary>Le nom sous lequel l'<c>Operator</c> reconnaît ce système.</summary>
  public SystemLabel Label { get; private set; }

  /// <summary>Ce que ce système contient, en prose française libre, relue telle quelle.</summary>
  public SystemContents Contents { get; private set; }

  /// <summary>
  /// L'adresse de l'<c>Adapter</c> qui sert ce système, ou <c>null</c> — et <c>null</c> est le
  /// régime majoritaire, pas une déclaration inachevée.
  /// </summary>
  public AdapterAddress? AdapterAddress { get; private set; }

  /// <summary>
  /// Le jour où un humain a affirmé ceci. Il se relit à l'écran, et il <b>descendra sur les
  /// <c>Step</c></b> : c'est là, au moment où quelqu'un signe, qu'une déclaration vieille se lit.
  /// </summary>
  public DateTimeOffset DeclaredOn { get; private set; }

  /// <summary>
  /// Ce que l'<c>Adapter</c> sait faire ici, dans l'ordre du catalogue. <b>Éventuellement vide</b> :
  /// c'est le niveau 0, le régime majoritaire, celui où le travail entre dans le dossier à la main
  /// plutôt que d'en sortir. Mieux vaut un niveau 0 honnête qu'une capacité qui ment sur ce qu'elle
  /// prouve.
  /// </summary>
  public IReadOnlyList<Capability> Capabilities => [.. _capabilities.Select(name => Capability.FromName(name))];

  /// <summary>
  /// Déclare un système, ou refuse. Le refus est une <b>programmation fautive</b> : la frontière de
  /// saisie a déjà nommé à l'humain ce qu'il avait mal rempli, et ce qui arrive ici est censé être
  /// déclarable.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> est absent.</exception>
  /// <exception cref="ArgumentException">Le plancher <see cref="Capability.Locate"/> est violé.</exception>
  public static DeclaredSystem Declare(
    DeclaredSystemId id,
    SystemLabel label,
    SystemContents contents,
    IEnumerable<Capability> capabilities,
    AdapterAddress? adapterAddress,
    DateTimeOffset declaredOn)
  {
    return new DeclaredSystem(id, label, contents, Catalogue(capabilities), adapterAddress, declaredOn);
  }

  /// <summary>
  /// Réécrit tout ce qui est déclarable, et <b>re-date la déclaration</b>. L'identifiant, lui, ne
  /// bouge jamais : c'est ce que l'<c>Adapter</c> connaît de ce système.
  /// </summary>
  /// <remarks>
  /// La date suit la révision parce qu'elle mesure la <b>fraîcheur de l'affirmation</b>, non
  /// l'ancienneté de la ligne : un humain vient de regarder ce système et de dire ce qu'il en sait,
  /// et c'est exactement ce que la date doit rapporter à qui lira « déclaré au tant ». Garder la
  /// date d'origine ferait passer une affirmation revue hier pour une affirmation de l'an dernier.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> est absent.</exception>
  /// <exception cref="ArgumentException">Le plancher <see cref="Capability.Locate"/> est violé.</exception>
  public void Revise(
    SystemLabel label,
    SystemContents contents,
    IEnumerable<Capability> capabilities,
    AdapterAddress? adapterAddress,
    DateTimeOffset declaredOn)
  {
    var catalogued = Catalogue(capabilities);

    Label = label;
    Contents = contents;
    _capabilities = catalogued;
    AdapterAddress = adapterAddress;
    DeclaredOn = declaredOn;
  }

  /// <summary>
  /// Range les capacités dans l'ordre du catalogue, sans doublon, et tient le seul invariant du
  /// type : <b><see cref="Capability.Locate"/> est le plancher</b> — un système déclare
  /// <c>Locate</c>, ou rien. Sans lui, « le client déclare avoir effacé » est du vide, là où
  /// « le client déclarait 12 enregistrements, puis 0 » est une trace.
  /// </summary>
  private static string[] Catalogue(IEnumerable<Capability> capabilities)
  {
    ArgumentNullException.ThrowIfNull(capabilities);

    var declared = new HashSet<Capability>(capabilities);

    if (declared.Count > 0 && !declared.Contains(Capability.Locate))
    {
      throw new ArgumentException(
        "Locate est le plancher : un système déclare Locate, ou aucune capacité. Sans lui, une "
        + "affirmation sur ce système n'a pas de dénominateur.",
        nameof(capabilities));
    }

    // L'ordre du catalogue, et non celui de la saisie : deux systèmes portant les mêmes capacités
    // s'écrivent alors pareil, en base comme à l'écran.
    return [.. declared.OrderBy(capability => capability.Value).Select(capability => capability.Name)];
  }
}

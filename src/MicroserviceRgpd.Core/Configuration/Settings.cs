using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// Le <b>Paramétrage</b> : la configuration applicative du service, <b>unique et propriété du
/// service</b>. Elle associe chacun des <b>six</b> droits RGPD dans le périmètre — <see
/// cref="DataSubjectRight.List"/> moins <see cref="DataSubjectRight.OutOfScope"/> — à l'<see
/// cref="EndpointUrl"/> à laquelle le service l'exercera, <b>ou à rien</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un singleton, jamais un objet par dossier.</b> Il n'existe qu'une seule configuration faisant
/// autorité : sa clé primaire est <see cref="SingletonId"/>, figée, et la base ne porte qu'une
/// ligne. C'est ce qui distingue le Paramétrage de tout ce qui s'instruit dans la durée.
/// </para>
/// <para>
/// <b>Naissance paresseuse, aucun seed.</b> Un service vierge n'a aucune ligne persistée, et c'est
/// un état complet : <see cref="Unconfigured"/> rend les six droits « non configuré » sans que rien
/// n'ait jamais été écrit. L'intégrateur n'a pas à « créer » la configuration avant de la lire.
/// </para>
/// <para>
/// ⚠️ <b>Exactement les six droits, jamais <see cref="DataSubjectRight.OutOfScope"/>.</b> Cet
/// invariant vit ici, dans le domaine : <see cref="OutOfScope"/> n'est pas un droit qu'on exerce,
/// c'est le verdict qu'aucun ne l'est. Lui demander une adresse n'a aucun sens, et <see
/// cref="EndpointFor"/> le refuse plutôt que de porter une septième colonne muette.
/// </para>
/// <para>
/// Les libellés et les articles ne sont <b>pas</b> recopiés ici : ils se lisent sur <see
/// cref="DataSubjectRight"/> (SharedKernel), et <c>Configuration</c> en est un troisième
/// consommateur, aux côtés de <c>Casework</c> et <c>Qualification</c>.
/// </para>
/// </remarks>
public sealed class Settings : IAggregateRoot
{
  /// <summary>
  /// La clé de l'unique ligne, <b>figée</b> : la configuration est un singleton, et sa clé ne se
  /// choisit pas plus qu'elle ne s'engendre. La base la porte en <c>ValueGeneratedNever</c>.
  /// </summary>
  public const int SingletonId = 1;

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser la ligne. Il ne rejoue aucun invariant.</summary>
  private Settings()
  {
  }

  /// <summary>La clé de l'unique ligne du Paramétrage — toujours <see cref="SingletonId"/>.</summary>
  public int Id { get; private set; } = SingletonId;

  /// <summary>L'adresse d'exercice du droit d'accès (art. 15), ou <c>null</c> — « non configuré ».</summary>
  public EndpointUrl? Access { get; private set; }

  /// <summary>L'adresse d'exercice du droit de rectification (art. 16), ou <c>null</c>.</summary>
  public EndpointUrl? Rectification { get; private set; }

  /// <summary>L'adresse d'exercice du droit à l'effacement (art. 17), ou <c>null</c>.</summary>
  public EndpointUrl? Erasure { get; private set; }

  /// <summary>L'adresse d'exercice du droit à la limitation (art. 18), ou <c>null</c>.</summary>
  public EndpointUrl? Restriction { get; private set; }

  /// <summary>L'adresse d'exercice du droit à la portabilité (art. 20), ou <c>null</c>.</summary>
  public EndpointUrl? Portability { get; private set; }

  /// <summary>L'adresse d'exercice du droit d'opposition (art. 21), ou <c>null</c>.</summary>
  public EndpointUrl? Objection { get; private set; }

  /// <summary>
  /// Les <b>six</b> droits que le Paramétrage configure — <see cref="DataSubjectRight.List"/>
  /// <b>moins</b> <see cref="DataSubjectRight.OutOfScope"/> —, rangés par leur <b>ordinal</b>, qui
  /// suit l'ordre des articles (15, 16, 17, 18, 20, 21). ⚠️ <c>List</c> est trié par nom, pas par
  /// ordinal : le tri est donc explicite ici, pour que l'écran lise les droits dans l'ordre du
  /// règlement. C'est la lecture même de l'invariant « exactement les six droits, jamais OutOfScope ».
  /// </summary>
  public static IReadOnlyList<DataSubjectRight> ConfigurableRights { get; } =
    [.. DataSubjectRight.List
      .Where(right => right != DataSubjectRight.OutOfScope)
      .OrderBy(right => right.Value)];

  /// <summary>
  /// Un Paramétrage <b>vierge</b> : les six droits « non configuré ». C'est l'état d'un service
  /// qu'on vient d'installer, rendu sans qu'aucune ligne n'ait été persistée.
  /// </summary>
  public static Settings Unconfigured() => new();

  /// <summary>
  /// L'adresse à laquelle exercer <paramref name="right"/>, ou <c>null</c> s'il est « non
  /// configuré ». ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/> n'en a pas</b> : le demander est
  /// une programmation fautive, pas un droit sans adresse.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/> est OutOfScope.</exception>
  public EndpointUrl? EndpointFor(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    return right.Name switch
    {
      nameof(DataSubjectRight.Access) => Access,
      nameof(DataSubjectRight.Rectification) => Rectification,
      nameof(DataSubjectRight.Erasure) => Erasure,
      nameof(DataSubjectRight.Restriction) => Restriction,
      nameof(DataSubjectRight.Portability) => Portability,
      nameof(DataSubjectRight.Objection) => Objection,
      _ => throw NoEndpointFor(right),
    };
  }

  /// <summary>
  /// Pose l'adresse à laquelle exercer <paramref name="right"/> — la <b>crée</b> s'il était « non
  /// configuré », la <b>remplace</b> sinon. <b>Seul ce droit est touché</b> : les cinq autres gardent
  /// leur adresse ou leur absence.
  /// </summary>
  /// <remarks>
  /// Aucune validation d'adresse ici : un <see cref="EndpointUrl"/> est valide par construction, et
  /// l'écrire n'appelle rien — configurer une adresse reste une écriture locale.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/> est OutOfScope.</exception>
  public void SetEndpoint(DataSubjectRight right, EndpointUrl endpoint)
  {
    ArgumentNullException.ThrowIfNull(right);

    switch (right.Name)
    {
      case nameof(DataSubjectRight.Access):
        Access = endpoint;
        break;
      case nameof(DataSubjectRight.Rectification):
        Rectification = endpoint;
        break;
      case nameof(DataSubjectRight.Erasure):
        Erasure = endpoint;
        break;
      case nameof(DataSubjectRight.Restriction):
        Restriction = endpoint;
        break;
      case nameof(DataSubjectRight.Portability):
        Portability = endpoint;
        break;
      case nameof(DataSubjectRight.Objection):
        Objection = endpoint;
        break;
      default:
        throw NoEndpointFor(right);
    }
  }

  /// <summary>
  /// La <b>projection des six droits et de leur état</b>, dans l'ordre du noyau partagé : chaque
  /// droit avec l'adresse qui le configure, ou son absence. C'est ce que l'écran relit — le libellé
  /// et l'article se lisant sur le droit lui-même.
  /// </summary>
  public IReadOnlyList<RightEndpoint> Rights =>
    [.. ConfigurableRights.Select(right => new RightEndpoint(right, EndpointFor(right)))];

  /// <summary>Le refus d'un droit hors des six, dit une seule fois pour la lecture et l'écriture.</summary>
  private static ArgumentOutOfRangeException NoEndpointFor(DataSubjectRight right) =>
    new(
      nameof(right),
      right,
      "OutOfScope n'a pas d'endpoint : le Paramétrage ne configure que les six droits du périmètre.");
}

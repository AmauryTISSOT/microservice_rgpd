using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// Le <b>Paramétrage</b> : la configuration applicative du service, <b>unique et propriété du
/// service</b>. Elle associe chacun des <b>six</b> droits RGPD dans le périmètre — <see
/// cref="DataSubjectRight.List"/> moins <see cref="DataSubjectRight.OutOfScope"/> — à l'<see
/// cref="ExerciseChannel"/> par lequel le service l'exercera : une adresse HTTP, un routage
/// RabbitMQ, ou rien.
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
/// ⚠️ <b>Un droit, un seul canal</b> (ADR-0027). L'état persistant est fait de <b>propriétés plates
/// nullables</b> — une pour l'adresse, deux pour le routage —, et le canal se <b>compose</b> à la
/// lecture puis se <b>décompose</b> à l'écriture. L'exclusivité est donc tenue <b>au seul endroit
/// qui écrit</b> : poser un canal efface les colonnes de l'autre, sans valeur dormante. L'état
/// illégal reste représentable dans ces champs privés ; il ne l'est plus dans le type que le reste
/// du monde manipule.
/// </para>
/// <para>
/// ⚠️ <b>Exactement les six droits, jamais <see cref="DataSubjectRight.OutOfScope"/>.</b> Cet
/// invariant vit ici, dans le domaine : <see cref="OutOfScope"/> n'est pas un droit qu'on exerce,
/// c'est le verdict qu'aucun ne l'est. Lui demander un canal n'a aucun sens, et <see
/// cref="ChannelFor"/> le refuse plutôt que de porter trois colonnes muettes de plus.
/// </para>
/// <para>
/// Les libellés et les articles ne sont <b>pas</b> recopiés ici : ils se lisent sur <see
/// cref="DataSubjectRight"/> (SharedKernel), et <c>Configuration</c> en est l'un des trois
/// consommateurs, aux côtés de <c>Qualification</c> et <c>Requests</c>.
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

  /// <summary>L'adresse d'exercice du droit d'accès (art. 15), ou <c>null</c>.</summary>
  public EndpointUrl? AccessUrl { get; private set; }

  /// <summary>L'exchange d'exercice du droit d'accès (art. 15), ou <c>null</c>.</summary>
  public ExchangeName? AccessExchange { get; private set; }

  /// <summary>La routing key d'exercice du droit d'accès (art. 15), ou <c>null</c>.</summary>
  public RoutingKey? AccessRoutingKey { get; private set; }

  /// <summary>L'adresse d'exercice du droit de rectification (art. 16), ou <c>null</c>.</summary>
  public EndpointUrl? RectificationUrl { get; private set; }

  /// <summary>L'exchange d'exercice du droit de rectification (art. 16), ou <c>null</c>.</summary>
  public ExchangeName? RectificationExchange { get; private set; }

  /// <summary>La routing key d'exercice du droit de rectification (art. 16), ou <c>null</c>.</summary>
  public RoutingKey? RectificationRoutingKey { get; private set; }

  /// <summary>L'adresse d'exercice du droit à l'effacement (art. 17), ou <c>null</c>.</summary>
  public EndpointUrl? ErasureUrl { get; private set; }

  /// <summary>L'exchange d'exercice du droit à l'effacement (art. 17), ou <c>null</c>.</summary>
  public ExchangeName? ErasureExchange { get; private set; }

  /// <summary>La routing key d'exercice du droit à l'effacement (art. 17), ou <c>null</c>.</summary>
  public RoutingKey? ErasureRoutingKey { get; private set; }

  /// <summary>L'adresse d'exercice du droit à la limitation (art. 18), ou <c>null</c>.</summary>
  public EndpointUrl? RestrictionUrl { get; private set; }

  /// <summary>L'exchange d'exercice du droit à la limitation (art. 18), ou <c>null</c>.</summary>
  public ExchangeName? RestrictionExchange { get; private set; }

  /// <summary>La routing key d'exercice du droit à la limitation (art. 18), ou <c>null</c>.</summary>
  public RoutingKey? RestrictionRoutingKey { get; private set; }

  /// <summary>L'adresse d'exercice du droit à la portabilité (art. 20), ou <c>null</c>.</summary>
  public EndpointUrl? PortabilityUrl { get; private set; }

  /// <summary>L'exchange d'exercice du droit à la portabilité (art. 20), ou <c>null</c>.</summary>
  public ExchangeName? PortabilityExchange { get; private set; }

  /// <summary>La routing key d'exercice du droit à la portabilité (art. 20), ou <c>null</c>.</summary>
  public RoutingKey? PortabilityRoutingKey { get; private set; }

  /// <summary>L'adresse d'exercice du droit d'opposition (art. 21), ou <c>null</c>.</summary>
  public EndpointUrl? ObjectionUrl { get; private set; }

  /// <summary>L'exchange d'exercice du droit d'opposition (art. 21), ou <c>null</c>.</summary>
  public ExchangeName? ObjectionExchange { get; private set; }

  /// <summary>La routing key d'exercice du droit d'opposition (art. 21), ou <c>null</c>.</summary>
  public RoutingKey? ObjectionRoutingKey { get; private set; }

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
  /// Le canal par lequel exercer <paramref name="right"/> — une adresse HTTP, un routage RabbitMQ,
  /// ou <see cref="ExerciseChannel.NotConfigured"/>. ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/>
  /// n'en a pas</b> : le demander est une programmation fautive, pas un droit sans canal.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/> est OutOfScope.</exception>
  /// <exception cref="InvalidOperationException">
  /// La ligne porte un routage à demi écrit — voir <see cref="Compose"/>.
  /// </exception>
  public ExerciseChannel ChannelFor(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    return right.Name switch
    {
      nameof(DataSubjectRight.Access) => Compose(AccessUrl, AccessExchange, AccessRoutingKey),
      nameof(DataSubjectRight.Rectification) =>
        Compose(RectificationUrl, RectificationExchange, RectificationRoutingKey),
      nameof(DataSubjectRight.Erasure) => Compose(ErasureUrl, ErasureExchange, ErasureRoutingKey),
      nameof(DataSubjectRight.Restriction) =>
        Compose(RestrictionUrl, RestrictionExchange, RestrictionRoutingKey),
      nameof(DataSubjectRight.Portability) =>
        Compose(PortabilityUrl, PortabilityExchange, PortabilityRoutingKey),
      nameof(DataSubjectRight.Objection) => Compose(ObjectionUrl, ObjectionExchange, ObjectionRoutingKey),
      _ => throw NoChannelFor(right),
    };
  }

  /// <summary>
  /// Pose le canal par lequel exercer <paramref name="right"/> — le <b>crée</b> s'il était « non
  /// configuré », le <b>remplace</b> sinon. ⚠️ <b>Poser un canal efface l'autre</b> : enregistrer un
  /// routage oublie l'adresse du même droit, et réciproquement, sans valeur dormante. <b>Seul ce
  /// droit est touché</b> : les cinq autres gardent leur canal ou leur absence.
  /// </summary>
  /// <remarks>
  /// Aucune validation ici : une <see cref="EndpointUrl"/>, un <see cref="ExchangeName"/> et une
  /// <see cref="RoutingKey"/> sont valides par construction, et l'écrire n'appelle rien — configurer
  /// un canal reste une écriture locale, y compris pour RabbitMQ (ADR-0027).
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  /// <paramref name="right"/> ou <paramref name="channel"/> est absent.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/> est OutOfScope.</exception>
  public void SetChannel(DataSubjectRight right, ExerciseChannel channel)
  {
    ArgumentNullException.ThrowIfNull(channel);

    Assign(right, channel);
  }

  /// <summary>
  /// Ramène <paramref name="right"/> à « <b>non configuré</b> » : son canal est oublié, quel qu'il
  /// fût, et c'est un état aussi normal que celui d'un service vierge. <b>Un seul geste pour les
  /// deux canaux</b> — effacer un routage et effacer une adresse, c'est ramener un droit au même
  /// état. <b>Seul ce droit est touché</b>, et effacer un droit déjà « non configuré » ne change
  /// rien.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/> est OutOfScope.</exception>
  public void ClearChannel(DataSubjectRight right) =>
    Assign(right, ExerciseChannel.NotConfigured.Instance);

  /// <summary>
  /// La <b>projection des six droits et de leur état</b>, dans l'ordre du noyau partagé : chaque
  /// droit avec le canal qui le configure, « non configuré » compris. C'est ce que l'écran relit —
  /// le libellé et l'article se lisant sur le droit lui-même.
  /// </summary>
  public IReadOnlyList<RightChannel> Rights =>
    [.. ConfigurableRights.Select(right => new RightChannel(right, ChannelFor(right)))];

  /// <summary>
  /// Le canal que portent les trois propriétés plates d'un droit. ⚠️ <b>Aucune colonne
  /// discriminante</b> : l'exclusivité étant tenue à l'écriture, un exchange présent <i>est</i> le
  /// canal RabbitMQ (ADR-0027).
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un routage à demi écrit lève</b>, au lieu de se lire comme une adresse ou comme « non
  /// configuré ». L'agrégat écrit toujours les deux propriétés du routage ensemble : une ligne qui
  /// n'en porte qu'une n'a pas été écrite par le domaine, et la lire en silence ferait disparaître un
  /// réglage au prochain enregistrement. C'est l'état illégal que les champs plats laissent
  /// représentable, et le seul endroit d'où il puisse se voir.
  /// </remarks>
  /// <exception cref="InvalidOperationException">La ligne ne porte qu'une des deux propriétés du routage.</exception>
  private static ExerciseChannel Compose(EndpointUrl? url, ExchangeName? exchange, RoutingKey? routingKey)
  {
    if (exchange is { } named)
    {
      return routingKey is { } key
        ? new ExerciseChannel.RabbitMq(new RabbitMqRouting(named, key))
        : throw HalfWrittenRouting();
    }

    if (routingKey is not null)
    {
      throw HalfWrittenRouting();
    }

    return url is { } address
      ? new ExerciseChannel.HttpEndpoint(address)
      : ExerciseChannel.NotConfigured.Instance;
  }

  /// <summary>Le refus d'un routage à demi écrit, dit une seule fois pour ses deux moitiés.</summary>
  private static InvalidOperationException HalfWrittenRouting() =>
    new(
      "Un routage RabbitMQ à demi écrit : l'exchange et la routing key vont toujours ensemble. "
      + "Cette ligne n'a pas été écrite par le Paramétrage.");

  /// <summary>
  /// Écrit le canal d'un droit dans ses trois seules propriétés. Poser et effacer passent par ce
  /// même aiguillage, pour qu'aucun des deux ne puisse toucher un autre droit que le sien — et c'est
  /// <b>ici seulement</b> que l'exclusivité se tient : les trois propriétés sont écrites d'un bloc,
  /// donc celle de l'autre canal est remise à <c>null</c> du même geste.
  /// </summary>
  private void Assign(DataSubjectRight right, ExerciseChannel channel)
  {
    ArgumentNullException.ThrowIfNull(right);

    var (url, exchange, routingKey) = Decompose(channel);

    switch (right.Name)
    {
      case nameof(DataSubjectRight.Access):
        (AccessUrl, AccessExchange, AccessRoutingKey) = (url, exchange, routingKey);
        break;
      case nameof(DataSubjectRight.Rectification):
        (RectificationUrl, RectificationExchange, RectificationRoutingKey) = (url, exchange, routingKey);
        break;
      case nameof(DataSubjectRight.Erasure):
        (ErasureUrl, ErasureExchange, ErasureRoutingKey) = (url, exchange, routingKey);
        break;
      case nameof(DataSubjectRight.Restriction):
        (RestrictionUrl, RestrictionExchange, RestrictionRoutingKey) = (url, exchange, routingKey);
        break;
      case nameof(DataSubjectRight.Portability):
        (PortabilityUrl, PortabilityExchange, PortabilityRoutingKey) = (url, exchange, routingKey);
        break;
      case nameof(DataSubjectRight.Objection):
        (ObjectionUrl, ObjectionExchange, ObjectionRoutingKey) = (url, exchange, routingKey);
        break;
      default:
        throw NoChannelFor(right);
    }
  }

  /// <summary>
  /// Les trois propriétés plates qu'un canal vaut. <b>Un canal en remplit au plus deux</b> : c'est
  /// cette exhaustivité qui interdit la valeur dormante, puisque celles qu'il ne remplit pas sont
  /// rendues à <c>null</c>.
  /// </summary>
  private static (EndpointUrl? Url, ExchangeName? Exchange, RoutingKey? RoutingKey) Decompose(
    ExerciseChannel channel) =>
    channel switch
    {
      ExerciseChannel.HttpEndpoint http => (http.Address, null, null),
      ExerciseChannel.RabbitMq rabbit => (null, rabbit.Routing.Exchange, rabbit.Routing.RoutingKey),
      _ => (null, null, null),
    };

  /// <summary>Le refus d'un droit hors des six, dit une seule fois pour la lecture et l'écriture.</summary>
  private static ArgumentOutOfRangeException NoChannelFor(DataSubjectRight right) =>
    new(
      nameof(right),
      right,
      "OutOfScope n'a pas de canal d'exercice : le Paramétrage ne configure que les six droits du périmètre.");
}

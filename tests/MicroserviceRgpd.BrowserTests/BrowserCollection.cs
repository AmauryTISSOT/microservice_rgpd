namespace MicroserviceRgpd.BrowserTests;

/// <summary>
/// <b>Les collections de la suite navigateur</b> : chacune partage un harnais — donc un conteneur
/// PostgreSQL, un service et un Chromium à elle — entre les classes qui la portent.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il y en a plusieurs, et c'est une décision de durée.</b> xUnit ne parallélise pas les tests
/// d'une même collection : tant que la suite entière n'en formait qu'une, ses deux cent soixante-cinq
/// tests s'exécutaient un par un, six minutes durant, pendant que les quatre autres projets avaient
/// fini. Les collections, elles, tournent en parallèle : découper la suite raccourcit le chemin
/// critique de la porte à passer avant PR.
/// </para>
/// <para>
/// ⚠️ <b>Ce que le découpage ne change pas</b> : à l'intérieur d'une collection, les tests restent
/// en série, et chaque collection a sa propre base. Les gestes qui balaient l'état partagé —
/// <c>DeleteAllRequestsAsync</c>, <c>ForgetEveryEndpointAsync</c> — gardent donc exactement la même
/// garantie qu'avant, à condition que <b>le découpage reste au grain de la classe</b> : deux classes
/// qui se lisent l'état l'une de l'autre doivent rester dans la même collection.
/// </para>
/// <para>
/// ⚠️ <b>Le coût est en conteneurs</b> : une collection de plus, c'est un PostgreSQL, un service et
/// un Chromium de plus, démarrés en même temps. Cinq est ce qu'un poste à huit cœurs tient sans que
/// la contention ne reprenne ce que le parallélisme donne ; en ajouter demande de le mesurer.
/// </para>
/// </remarks>
[CollectionDefinition(Name)]
public class BrowserCollection : ICollectionFixture<BrowserHarness>
{
  /// <summary>Le tableau : sa trame, sa recherche, son tri, ses actions de ligne et l'exécution.</summary>
  public const string Name = "Browser";
}

/// <summary>La création d'une demande : la modale, sa validation, son abandon. <see cref="BrowserCollection"/>.</summary>
[CollectionDefinition(Name)]
public class BrowserCreationCollection : ICollectionFixture<BrowserHarness>
{
  public const string Name = "Browser — création";
}

/// <summary>La proposition du droit et les moteurs qui la rendent. <see cref="BrowserCollection"/>.</summary>
[CollectionDefinition(Name)]
public class BrowserProposalCollection : ICollectionFixture<BrowserHarness>
{
  public const string Name = "Browser — proposition du droit";
}

/// <summary>La fiche d'une demande, telle qu'elle s'ouvre et se lit. <see cref="BrowserCollection"/>.</summary>
[CollectionDefinition(Name)]
public class BrowserSheetCollection : ICollectionFixture<BrowserHarness>
{
  public const string Name = "Browser — fiche";
}

/// <summary>La modification d'une demande et sa prolongation. <see cref="BrowserCollection"/>.</summary>
[CollectionDefinition(Name)]
public class BrowserModificationCollection : ICollectionFixture<BrowserHarness>
{
  public const string Name = "Browser — modification";
}

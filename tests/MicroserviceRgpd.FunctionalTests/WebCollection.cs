namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// <b>Les collections du service démarré tel quel</b> : chacune partage une instance de
/// <see cref="CustomWebApplicationFactory{TProgram}"/> — donc un conteneur PostgreSQL à elle —
/// entre les classes qui la portent.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il y en a plusieurs, et c'est une décision de durée.</b> xUnit ne parallélise pas les tests
/// d'une même collection : tant que toutes les classes du service ordinaire portaient la même, leurs
/// quatre cent quarante et un tests s'exécutaient un par un derrière une seule fabrique. Les
/// collections, elles, tournent en parallèle. Même raisonnement, et mêmes garde-fous, que
/// <c>BrowserCollection</c> dans la suite navigateur.
/// </para>
/// <para>
/// ⚠️ <b>Le découpage est au grain de la classe.</b> À l'intérieur d'une collection les tests
/// restent en série, sur une base qui n'est qu'à elle ; deux classes qui se lisent l'état l'une de
/// l'autre — ou qui comptent les lignes d'une table que l'autre remplit — doivent rester ensemble.
/// C'est aussi pourquoi le découpage suit les sujets, et non la balance des durées.
/// </para>
/// <para>
/// ⚠️ <b>Ces collections-ci sont celles du câblage ordinaire.</b> Les hôtes qu'un drapeau ou une
/// configuration distingue — <c>A2OnWebCollection</c>, <c>LlmOffWebCollection</c>,
/// <c>ABrokerConfiguredWebCollection</c> et les autres — ont chacun la leur, et leur propre
/// conteneur, depuis toujours : leur fabrique <b>est</b> ce qu'ils éprouvent.
/// </para>
/// </remarks>
[CollectionDefinition(Name)]
public class WebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
  /// <summary>Le contexte <c>Screening</c> : dépôt, scan, relevé, arbitrage et export.</summary>
  public const string Name = "Web";
}

/// <summary>
/// Les demandes telles qu'on les enregistre, les relit et les modifie. <see cref="WebCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public class RequestsWebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
  public const string Name = "Web — demandes";
}

/// <summary>
/// Les demandes telles qu'on les exécute, les refuse et les prolonge. <see cref="WebCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public class RequestExecutionWebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
  public const string Name = "Web — exécution des demandes";
}

/// <summary>
/// La qualification, à l'écran comme à la frontière HTTP. <see cref="WebCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public class QualificationsWebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
  public const string Name = "Web — qualifications";
}

/// <summary>
/// Le Paramétrage, et ce que toute page rend de la plateforme. <see cref="WebCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public class SettingsWebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
  public const string Name = "Web — paramétrage et plateforme";
}

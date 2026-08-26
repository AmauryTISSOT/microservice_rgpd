namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le service démarre avec son <b>vrai</b> port de scan, dialectes câblés compris — le seul hôte de
/// test qui ouvre réellement une base d'un tiers.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il n'existe que pour le canari à cinq surfaces, et c'est la seule chose qu'il justifie.</b>
/// Une doublure de scanner ne peut, par construction, rien laisser fuir d'une base qu'elle n'ouvre
/// pas : « ce qui entre est borné » et « rien de réel ne reste » ne se prouvent que contre un pilote
/// qui a réellement lu un catalogue et prélevé des valeurs. Tous les <b>écrans</b> de scan, eux,
/// restent éprouvés sur la doublure — un test d'écran n'a rien à gagner à dépendre d'un fichier.
/// </para>
/// <para>
/// ⚠️ <b>Le moteur de qualification reste doublé, lui.</b> Rien ici ne touche à la qualification :
/// laisser le rôle de verdict au câblage réel aurait demandé un modèle, c'est-à-dire un test qui ne
/// tourne jamais — et un test qui ne tourne jamais ment.
/// </para>
/// </remarks>
public sealed class ARealScannerWebApplicationFactory : CustomWebApplicationFactory<Program>
{
  protected override bool SubstitutesTheScanningPort => false;
}

/// <summary>
/// Partage une seule instance de la fabrique au vrai scanner — donc un seul conteneur PostgreSQL —
/// entre les classes de test qui l'exercent. Elle est distincte de <see cref="WebCollection"/> parce
/// qu'un hôte se bâtit une fois : deux formes d'hôte demandent deux fabriques.
/// </summary>
[CollectionDefinition(Name)]
public class ARealScannerWebCollection : ICollectionFixture<ARealScannerWebApplicationFactory>
{
  public const string Name = "Web au vrai port de scan";
}

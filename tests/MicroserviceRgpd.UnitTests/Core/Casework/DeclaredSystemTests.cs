using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Ce qu'un <c>DeclaredSystem</c> tient, et ce qu'il refuse de tenir. Le régime majoritaire du lot
/// — un système sans aucune capacité et sans adresse — est ici un cas <b>nominal</b>, pas une
/// tolérance.
/// </summary>
public class DeclaredSystemTests
{
  private static readonly DateTimeOffset Declared = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Le niveau 0 : l'export commercial parti chez l'agence n'a ni serveur, ni <c>Adapter</c>, ni
  /// capacité — et c'est précisément lui qu'il faut voir écrit dans le dossier.
  /// </summary>
  [Fact]
  public void DeclaresASystemWithNoCapabilityAndNoAdapterAtAll()
  {
    var system = ADeclaredSystem(capabilities: []);

    system.Capabilities.ShouldBeEmpty();
    system.AdapterAddress.ShouldBeNull();
    system.DeclaredOn.ShouldBe(Declared);
  }

  /// <summary>
  /// Le plancher : un système déclare <c>Locate</c>, ou rien. Sans lui, « le client déclare avoir
  /// effacé » est du vide.
  /// </summary>
  [Theory]
  [InlineData(nameof(Capability.Read))]
  [InlineData(nameof(Capability.Erase))]
  [InlineData(nameof(Capability.Rectify))]
  public void RefusesACapabilityDeclaredWithoutTheLocateFloor(string capability)
  {
    Should.Throw<ArgumentException>(
      () => ADeclaredSystem(capabilities: [Capability.FromName(capability)]));
  }

  [Fact]
  public void RefusesAWholeSetOfCapabilitiesMissingTheLocateFloor()
  {
    Should.Throw<ArgumentException>(
      () => ADeclaredSystem(capabilities: [Capability.Read, Capability.Erase]));
  }

  /// <summary>
  /// Le cas le plus intéressant du terrain, et il n'est pas un manque : la comptabilité scellée dix
  /// ans sait localiser et lire, elle ne pourra <b>jamais</b> effacer.
  /// </summary>
  [Fact]
  public void HoldsASystemThatCanLocateAndReadButWillNeverErase()
  {
    var system = ADeclaredSystem(capabilities: [Capability.Locate, Capability.Read]);

    system.Capabilities.ShouldBe([Capability.Locate, Capability.Read]);
  }

  /// <summary>
  /// L'ordre est celui du catalogue et non celui de la saisie, et un doublon ne se compte qu'une
  /// fois : deux systèmes portant les mêmes capacités s'écrivent alors pareil.
  /// </summary>
  [Fact]
  public void CataloguesCapabilitiesInTheirOwnOrderWithoutEverRepeatingOne()
  {
    var system = ADeclaredSystem(
      capabilities: [Capability.Rectify, Capability.Locate, Capability.Read, Capability.Locate]);

    system.Capabilities.ShouldBe([Capability.Locate, Capability.Read, Capability.Rectify]);
  }

  /// <summary>
  /// La révision <b>re-date</b> la déclaration : un humain vient de regarder ce système et de dire
  /// ce qu'il en sait. Garder la date d'origine ferait passer une affirmation revue hier pour une
  /// affirmation de l'an dernier.
  /// </summary>
  [Fact]
  public void RedatesTheDeclarationWhenAHumanRevisesIt()
  {
    var revisedOn = Declared.AddMonths(8);
    var system = ADeclaredSystem(capabilities: []);

    system.Revise(
      SystemLabel.From("La base de la boutique"),
      SystemContents.From("Les comptes clients, les commandes et les messages du support."),
      [Capability.Locate],
      AdapterAddress.From("https://brocanto.example/rgpd"),
      revisedOn);

    system.DeclaredOn.ShouldBe(revisedOn);
    system.Label.Value.ShouldBe("La base de la boutique");
    system.Capabilities.ShouldBe([Capability.Locate]);
    system.AdapterAddress!.Value.Value.ShouldBe("https://brocanto.example/rgpd");
  }

  /// <summary>L'identifiant ne bouge jamais : c'est ce que l'<c>Adapter</c> connaît de ce système.</summary>
  [Fact]
  public void NeverLetsARevisionMoveTheIdentifierTheAdapterKnows()
  {
    var system = ADeclaredSystem(capabilities: []);

    system.Revise(
      SystemLabel.From("Un autre nom"),
      SystemContents.From("Une autre prose."),
      [],
      adapterAddress: null,
      Declared.AddDays(1));

    system.Id.Value.ShouldBe("export-agence");
  }

  /// <summary>Une révision qui viole le plancher est refusée, et ne laisse rien derrière elle.</summary>
  [Fact]
  public void LeavesASystemUntouchedWhenARevisionViolatesTheLocateFloor()
  {
    var system = ADeclaredSystem(capabilities: [Capability.Locate]);

    Should.Throw<ArgumentException>(() => system.Revise(
      SystemLabel.From("La comptabilité scellée"),
      SystemContents.From("Les écritures comptables, conservées dix ans."),
      [Capability.Erase],
      adapterAddress: null,
      Declared.AddDays(1)));

    system.Capabilities.ShouldBe([Capability.Locate]);
    system.Label.Value.ShouldBe("L'export commercial mensuel");
  }

  /// <summary>
  /// <b>Le secret de l'<c>Adapter</c> n'a aucun emplacement, et aucun champ ne nomme le schéma du
  /// client.</b> La surface est énumérée en toutes lettres : ajouter un champ ici doit être un
  /// geste délibéré, parce que c'est là que le service cesserait de pouvoir répondre de ce qu'il
  /// détient.
  /// </summary>
  [Fact]
  public void OffersNoPlaceForAnAdapterSecretNorForAnyNameOfTheClientSchema()
  {
    var surface = typeof(DeclaredSystem)
      .GetProperties()
      .Select(property => property.Name)
      .Order(StringComparer.Ordinal);

    surface.ShouldBe(["AdapterAddress", "Capabilities", "Contents", "DeclaredOn", "Id", "Label"]);
  }

  private static DeclaredSystem ADeclaredSystem(IEnumerable<Capability> capabilities)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From("export-agence"),
      SystemLabel.From("L'export commercial mensuel"),
      SystemContents.From("L'export commercial transmis chaque mois à notre agence."),
      capabilities,
      adapterAddress: null,
      Declared);
  }
}

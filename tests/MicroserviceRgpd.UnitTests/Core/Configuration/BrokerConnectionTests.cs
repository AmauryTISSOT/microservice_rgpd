using System.Reflection;
using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UnitTests.Core.Configuration;

/// <summary>
/// La <b>connexion du déploiement au broker</b> est <b>fermée à deux cas</b>, et « absente » en est
/// un <b>cas nommé, pas un <c>null</c></b> ni un booléen nu (ADR-0028). C'est ce qui permet de lire
/// « ce déploiement sait publier » à l'appel, plutôt que de le déduire d'une clé.
/// </summary>
public class BrokerConnectionTests
{
  /// <summary>
  /// <b>Deux cas, et pas un troisième.</b> On lit la hiérarchie par réflexion plutôt qu'à l'œil : un
  /// troisième cas ajouté ailleurs dans l'assemblage doit faire rougir ce test.
  /// </summary>
  [Fact]
  public void IsClosedToExactlyTwoCases()
  {
    var cases = typeof(BrokerConnection).Assembly
      .GetTypes()
      .Where(type => type.IsSubclassOf(typeof(BrokerConnection)))
      .Select(type => type.Name)
      .Order(StringComparer.Ordinal);

    cases.ShouldBe([nameof(BrokerConnection.Absent), nameof(BrokerConnection.Configured)]);
  }

  /// <summary>
  /// <b>La fermeture est tenue par le constructeur privé</b> : personne au dehors ne peut se
  /// construire un troisième cas, et un <c>switch</c> sur les deux est donc complet.
  /// <para>
  /// ⚠️ Le constructeur de copie d'un record est exclu du compte, pour le motif écrit sur
  /// <c>ExerciseChannelTests</c> : le compilateur l'exige <c>protected</c> (CS8878).
  /// </para>
  /// </summary>
  [Fact]
  public void CannotBeConstructedFromOutsideItsOwnDeclaration()
  {
    var constructors = typeof(BrokerConnection)
      .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
      .Where(constructor => constructor.GetParameters() is not [{ ParameterType.Name: nameof(BrokerConnection) }]);

    constructors.ShouldAllBe(constructor => constructor.IsPrivate);
    constructors.ShouldNotBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>« Absente » est un cas, pas un <c>null</c></b> : un déploiement sans bus est un état
  /// légal, qui s'écrit et se reconnaît comme l'autre.
  /// </summary>
  [Fact]
  public void NamesTheAbsentCaseRatherThanLeavingAHole()
  {
    BrokerConnection absent = BrokerConnection.Absent.Instance;

    absent.ShouldNotBeNull();
    absent.ShouldBeOfType<BrokerConnection.Absent>();
    absent.ShouldBe(new BrokerConnection.Absent());
  }

  /// <summary>
  /// Un <c>switch</c> sur les deux cas <b>n'a pas de branche nulle</b> — et l'écrire ici prouve que
  /// le compilateur l'accepte comme exhaustif.
  /// </summary>
  [Theory]
  [InlineData(true, "sait publier")]
  [InlineData(false, "ne sait pas publier")]
  public void ReadsEachCaseWithoutANullBranch(bool configured, string expected)
  {
    BrokerConnection connection = configured
      ? BrokerConnection.Configured.Instance
      : BrokerConnection.Absent.Instance;

    var read = connection switch
    {
      BrokerConnection.Configured => "sait publier",
      BrokerConnection.Absent => "ne sait pas publier",
      _ => throw new InvalidOperationException("Un troisième cas est apparu."),
    };

    read.ShouldBe(expected);
  }

  /// <summary>
  /// ⚠️ <b>Une clé vide, faite d'espaces ou absente vaut la même chose</b> : aucune connexion. La
  /// règle est écrite ici, dans le noyau, et <b>à ce seul endroit</b> (ADR-0028) — un hôte de broker
  /// qui ne nomme aucune machine ne connecte rien.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("\t  ")]
  public void DeclaresNoConnectionOnAHostThatNamesNoMachine(string? hostName) =>
    BrokerConnection.Declaring(hostName).ShouldBeOfType<BrokerConnection.Absent>();

  /// <summary>
  /// <b>Un hôte nommé vaut connexion</b>, et rien n'est joint pour le dire : la règle lit ce qui est
  /// écrit, jamais un broker (ADR-0027).
  /// </summary>
  [Fact]
  public void DeclaresAConnectionAsSoonAsAHostNamesAMachine() =>
    BrokerConnection.Declaring("rabbitmq.brocanto.example.fr").ShouldBeOfType<BrokerConnection.Configured>();
}

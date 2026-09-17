using System.Reflection;
using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UnitTests.Core.Configuration;

/// <summary>
/// Le <b>canal d'exercice</b> est <b>fermé à trois cas</b>, et « non configuré » en est un <b>cas
/// nommé, pas un <c>null</c></b> (ADR-0027). Ces deux propriétés sont ce qui rend exhaustive toute
/// lecture d'un canal, sans branche nulle.
/// </summary>
public class ExerciseChannelTests
{
  /// <summary>
  /// <b>Trois cas, et pas un quatrième.</b> On lit la hiérarchie par réflexion plutôt qu'à l'œil :
  /// un quatrième cas ajouté ailleurs dans l'assemblage doit faire rougir ce test, pas passer
  /// inaperçu.
  /// </summary>
  [Fact]
  public void IsClosedToExactlyThreeCases()
  {
    var cases = typeof(ExerciseChannel).Assembly
      .GetTypes()
      .Where(type => type.IsSubclassOf(typeof(ExerciseChannel)))
      .Select(type => type.Name)
      .Order(StringComparer.Ordinal);

    cases.ShouldBe(
      [
        nameof(ExerciseChannel.HttpEndpoint),
        nameof(ExerciseChannel.NotConfigured),
        nameof(ExerciseChannel.RabbitMq),
      ]);
  }

  /// <summary>
  /// <b>La fermeture est tenue par le constructeur privé</b> : personne au dehors ne peut se
  /// construire un quatrième cas, et un <c>switch</c> sur les trois est donc complet.
  /// <para>
  /// ⚠️ <b>Le constructeur de copie d'un record est exclu du compte</b>, et il ne peut pas l'être du
  /// code : le compilateur exige qu'il soit <c>protected</c> sur un record non scellé (CS8878).
  /// C'est la seule porte qui reste, et elle exige une instance existante à recopier — un détour
  /// qu'on ne prend pas par mégarde.
  /// </para>
  /// </summary>
  [Fact]
  public void CannotBeConstructedFromOutsideItsOwnDeclaration()
  {
    var constructors = typeof(ExerciseChannel)
      .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
      .Where(constructor => constructor.GetParameters() is not [{ ParameterType.Name: nameof(ExerciseChannel) }]);

    constructors.ShouldAllBe(constructor => constructor.IsPrivate);
    constructors.ShouldNotBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>« Non configuré » est un cas, pas un <c>null</c></b> : il s'écrit, se compare et se
  /// reconnaît comme les deux autres.
  /// </summary>
  [Fact]
  public void NamesTheUnconfiguredCaseRatherThanLeavingAHole()
  {
    ExerciseChannel notConfigured = ExerciseChannel.NotConfigured.Instance;

    notConfigured.ShouldNotBeNull();
    notConfigured.ShouldBeOfType<ExerciseChannel.NotConfigured>();
    notConfigured.ShouldBe(new ExerciseChannel.NotConfigured());
  }

  /// <summary>
  /// Un <c>switch</c> sur les trois cas <b>n'a pas de branche nulle</b> — et l'écrire ici prouve que
  /// le compilateur l'accepte comme exhaustif.
  /// </summary>
  [Theory]
  [InlineData(true, "adresse")]
  [InlineData(false, "routage")]
  public void ReadsEachSpeciesWithoutANullBranch(bool http, string expected)
  {
    ExerciseChannel channel = http
      ? new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/rgpd"))
      : new ExerciseChannel.RabbitMq(
        new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

    var read = channel switch
    {
      ExerciseChannel.HttpEndpoint => "adresse",
      ExerciseChannel.RabbitMq => "routage",
      ExerciseChannel.NotConfigured => "non configuré",
      _ => throw new InvalidOperationException("Un quatrième cas est apparu."),
    };

    read.ShouldBe(expected);
  }

  /// <summary>
  /// <b>Deux canaux de même espèce et de même contenu sont égaux</b> : ce sont des records, et c'est
  /// ce qui permet de comparer un canal relu à celui qu'on a posé.
  /// </summary>
  [Fact]
  public void ComparesByContentWithinASpecies()
  {
    var address = EndpointUrl.From("https://brocanto.example.fr/rgpd");

    new ExerciseChannel.HttpEndpoint(address)
      .ShouldBe(new ExerciseChannel.HttpEndpoint(address));

    new ExerciseChannel.HttpEndpoint(address)
      .ShouldNotBe(new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/autre")));
  }
}

using System.Text;
using MicroserviceRgpd.Core.Configuration;
using Vogen;

namespace MicroserviceRgpd.UnitTests.Core.Configuration;

/// <summary>
/// Ce que le <b>routage RabbitMQ</b> et ses deux value objects refusent. Comme l'<c>EndpointUrl</c>,
/// ils protègent le contexte <c>Configuration</c> en <b>levant</b> à la construction — et
/// <b>aucune</b> de ces vérifications ne touche le broker : ni connexion, ni résolution de nom, ni
/// déclaration d'exchange (ADR-0027).
/// </summary>
public class RabbitMqRoutingTests
{
  /// <summary>Un nom de 255 octets UTF-8, écrit en caractères de <b>deux</b> octets.</summary>
  private const string TwoByteCharacter = "é";

  /// <summary>Un caractère de <b>quatre</b> octets UTF-8, hors du plan multilingue de base.</summary>
  private const string FourByteCharacter = "🗄";

  /// <summary>Le vide et les espaces seuls sont refusés : un exchange sans nom ne se publie pas.</summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t\n")]
  public void RefusesAnEmptyExchangeName(string name)
  {
    Should.Throw<ValueObjectValidationException>(() => ExchangeName.From(name));
  }

  /// <summary>
  /// Le vide et les espaces seuls sont refusés pour la routing key <b>aussi</b>, alors qu'un broker
  /// l'accepterait sur un <c>fanout</c> : une clé laissée vide se lit comme une saisie inachevée.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t\n")]
  public void RefusesAnEmptyRoutingKey(string key)
  {
    Should.Throw<ValueObjectValidationException>(() => RoutingKey.From(key));
  }

  /// <summary>
  /// <b>Les bordures sont rognées avant validation et avant stockage</b> : la valeur conservée est
  /// celle qu'on publiera, sans l'espace qu'un copier-coller a ramené.
  /// </summary>
  [Fact]
  public void TrimsTheEdgesBeforeValidatingAndBeforeStoring()
  {
    ExchangeName.From("  rgpd.exercice \n").Value.ShouldBe("rgpd.exercice");
    RoutingKey.From("\t droit.acces  ").Value.ShouldBe("droit.acces");
  }

  /// <summary>
  /// ⚠️ <b>Le plafond compte des octets, pas des caractères</b> : 255 octets passent, 256 sont
  /// refusés, et on le vérifie en <b>caractères multi-octets</b> — en ASCII, les deux limites se
  /// confondraient et le test serait muet sur ce qui est en jeu.
  /// </summary>
  [Fact]
  public void AcceptsTwoHundredFiftyFiveUtf8BytesAndRefusesTwoHundredFiftySix()
  {
    var accepted = TwoByteCharacter.Repeated(127) + "a";
    var refused = TwoByteCharacter.Repeated(128);

    Encoding.UTF8.GetByteCount(accepted).ShouldBe(ExchangeName.MaxLengthInBytes);
    Encoding.UTF8.GetByteCount(refused).ShouldBe(ExchangeName.MaxLengthInBytes + 1);

    ExchangeName.From(accepted).Value.ShouldBe(accepted);
    RoutingKey.From(accepted).Value.ShouldBe(accepted);

    Should.Throw<ValueObjectValidationException>(() => ExchangeName.From(refused));
    Should.Throw<ValueObjectValidationException>(() => RoutingKey.From(refused));
  }

  /// <summary>
  /// ⚠️ <b>Une chaîne bien en dessous de 255 caractères peut dépasser 255 octets</b> : c'est
  /// exactement ce qu'un plafond en caractères laisserait passer, et ce que le broker refuserait.
  /// </summary>
  [Fact]
  public void RefusesAShortStringThatWeighsMoreThanTwoHundredFiftyFiveBytes()
  {
    var name = FourByteCharacter.Repeated(64);

    name.Length.ShouldBeLessThan(ExchangeName.MaxLengthInBytes);
    Encoding.UTF8.GetByteCount(name).ShouldBeGreaterThan(ExchangeName.MaxLengthInBytes);

    Should.Throw<ValueObjectValidationException>(() => ExchangeName.From(name));
    Should.Throw<ValueObjectValidationException>(() => RoutingKey.From(name));
  }

  /// <summary>
  /// <b>Aucun autre contrôle de forme</b> : le jeu de caractères qu'un broker accepte lui appartient,
  /// et ce value object n'invente pas une grammaire que RabbitMQ n'impose pas.
  /// </summary>
  [Theory]
  [InlineData("rgpd.exercice")]
  [InlineData("rgpd-exercice")]
  [InlineData("amq.direct")]
  [InlineData("un nom avec des espaces")]
  public void AcceptsAnyNonEmptyNameWithinThePlafond(string name)
  {
    ExchangeName.From(name).Value.ShouldBe(name);
  }

  /// <summary>
  /// ⚠️ <b>Construire un routage n'appelle rien</b> : ni connexion au broker, ni résolution de nom.
  /// On le tient là où il se tient vraiment — <b>l'assemblage du domaine ne référence aucun client
  /// de broker ni aucune socket</b> : sans ces types, aucun corps de méthode ne <i>peut</i> ouvrir
  /// une connexion, et la clause de l'ADR-0027 n'a pas à être relue à chaque ajout de champ.
  /// </summary>
  [Fact]
  public void CannotOpenAConnectionBecauseTheDomainKnowsNoBrokerClient()
  {
    var referenced = typeof(RabbitMqRouting).Assembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name ?? string.Empty);

    referenced.ShouldNotContain(
      name => name.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Sockets", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Net.Http", StringComparison.OrdinalIgnoreCase),
      "Le domaine a appris à joindre quelque chose : ADR-0027 le lui interdit.");
  }

  /// <summary>
  /// <b>Le routage ne valide rien de son côté</b> : il n'a rien à vérifier que ses deux champs
  /// n'aient déjà refusé, et le construire n'appelle rien — ni connexion, ni résolution de nom.
  /// </summary>
  [Fact]
  public void ComposesTheTwoFieldsWithoutValidatingAnythingItself()
  {
    var routing = new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces"));

    routing.Exchange.Value.ShouldBe("rgpd.exercice");
    routing.RoutingKey.Value.ShouldBe("droit.acces");
    routing.ShouldBe(new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));
  }
}

/// <summary>Répéter un caractère, pour écrire une limite en octets sans la compter à la main.</summary>
internal static class RepeatedStringExtensions
{
  internal static string Repeated(this string value, int times) => string.Concat(Enumerable.Repeat(value, times));
}

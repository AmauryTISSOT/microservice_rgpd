using MicroserviceRgpd.Core.Configuration;
using Vogen;

namespace MicroserviceRgpd.UnitTests.Core.Configuration;

/// <summary>
/// Ce que l'<c>EndpointUrl</c> refuse. Il protège le contexte <c>Configuration</c> en <b>levant</b>
/// à la construction : l'intégrateur ne peut jamais enregistrer une adresse que le service ne
/// saurait pas appeler. Aucune de ces vérifications n'émet le moindre appel réseau.
/// </summary>
public class EndpointUrlTests
{
  /// <summary>
  /// <b>Une adresse absolue est exigée</b> : une adresse relative n'a pas d'hôte à joindre, et le
  /// service ne saurait pas où l'appeler.
  /// </summary>
  [Theory]
  [InlineData("/callback")]
  [InlineData("callback")]
  [InlineData("../callback")]
  public void RefusesARelativeAddress(string url)
  {
    Should.Throw<ValueObjectValidationException>(() => EndpointUrl.From(url));
  }

  /// <summary>Une adresse malformée ne se laisse pas construire.</summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("ht!tp://exemple.fr")]
  [InlineData("https://")]
  [InlineData("ftp://exemple.fr/donnees")]
  public void RefusesAMalformedAddress(string url)
  {
    Should.Throw<ValueObjectValidationException>(() => EndpointUrl.From(url));
  }

  /// <summary>
  /// ⚠️ <b>Une adresse porteuse d'un userinfo est refusée</b> : <c>https://jean:mot@…</c> transporte
  /// exactement le secret d'appel que la <c>Configuration</c> n'a pas à connaître.
  /// </summary>
  [Theory]
  [InlineData("https://jean:mot@exemple.fr/callback")]
  [InlineData("https://jean@exemple.fr/callback")]
  public void RefusesAnAddressCarryingUserInfo(string url)
  {
    Should.Throw<ValueObjectValidationException>(() => EndpointUrl.From(url));
  }

  /// <summary>
  /// <b>Le HTTP est accepté au même titre que le HTTPS.</b> Le contexte n'impose pas le chiffrement
  /// du transport : cet arbitrage relève du déploiement, non du value object.
  /// </summary>
  [Fact]
  public void AcceptsHttpJustAsHttps()
  {
    EndpointUrl.From("http://exemple.fr/callback").Value.ShouldBe("http://exemple.fr/callback");
  }

  /// <summary>Une adresse absolue ordinaire traverse et est conservée telle qu'elle a été comprise.</summary>
  [Fact]
  public void AcceptsAnOrdinaryAbsoluteAddress()
  {
    EndpointUrl.From("https://exemple.fr/callback").Value.ShouldBe("https://exemple.fr/callback");
  }

  /// <summary>Les bordures sont nettoyées plutôt que refusées.</summary>
  [Fact]
  public void CleansTheBordersRatherThanRefusingThem()
  {
    EndpointUrl.From("  https://exemple.fr/callback \n").Value.ShouldBe("https://exemple.fr/callback");
  }
}

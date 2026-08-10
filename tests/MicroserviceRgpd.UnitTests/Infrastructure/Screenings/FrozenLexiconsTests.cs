using System.Security.Cryptography;
using MicroserviceRgpd.Infrastructure.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Le <b>gel</b> des lexiques, ancré : les fichiers embarqués dans l'assemblage sont, octet pour
/// octet, ceux du commit <c>d413d55</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Éditer une entrée rendrait le banc non concluant.</b> Les trois lexiques ont été rédigés en
/// aveugle — un sous-agent sans aucun accès au dépôt, la seule section <c>PersonalDataCategory</c>
/// du glossaire en entrée — et gelés par le commit qui les introduit : c'est le prédicat de la cause
/// ③ de #130, exécuté par #155. Le verdict du banc, et donc ADR-0004 qui en découle, ne valent que
/// tant que ces fichiers ne bougent pas.
/// </para>
/// <para>
/// <b>Une empreinte plutôt qu'une comparaison avec <c>exploration/</c></b> : un test qui remonterait
/// au fichier de l'exploration passerait au vert le jour où <b>les deux</b> copies auraient été
/// éditées ensemble, ce qui est exactement le geste que la clause interdit. L'empreinte, elle, est
/// écrite ici et ne se recalcule pas toute seule.
/// </para>
/// <para>
/// Pour la revérifier à la main :
/// <c>git show d413d55:exploration/banc-screening/lexiques/dictionnaire-fr.tsv | sha256sum</c>.
/// </para>
/// </remarks>
public class FrozenLexiconsTests
{
  /// <summary>L'empreinte du dictionnaire français au commit du gel.</summary>
  private const string FrenchAtTheFreeze = "923ad7417b29dfe09b1bc88a198c748bacd6d5c24423ac9b0d101c14b5697dd8";

  /// <summary>L'empreinte du dictionnaire anglais au commit du gel.</summary>
  private const string EnglishAtTheFreeze = "43758022e57c9f14e2d57702360da0a552ec547b7af63543cb6898f5fbf71688";

  /// <summary>Les deux dictionnaires embarqués sont ceux du gel, et rien d'autre.</summary>
  [Theory]
  [InlineData("dictionnaire-fr.tsv", FrenchAtTheFreeze)]
  [InlineData("dictionnaire-en.tsv", EnglishAtTheFreeze)]
  public void CarriesTheLexiconsExactlyAsTheyWereFrozen(string lexicon, string atTheFreeze)
  {
    using var stream = typeof(ScreeningEngineServiceExtensions).Assembly
      .GetManifestResourceStream($"MicroserviceRgpd.Infrastructure.Screenings.Lexicons.{lexicon}")
      ?? throw new InvalidOperationException($"Le lexique gelé « {lexicon} » n'est pas embarqué dans l'assemblage.");

    Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant().ShouldBe(
      atTheFreeze,
      $"Le lexique « {lexicon} » diffère du gel d413d55. Les rééditer est interdit (#155) : "
      + "toute édition d'une entrée après le commit d'introduction rend le banc non concluant, "
      + "et avec lui le verdict dont ADR-0004 tient l'emplacement du moteur.");
  }
}

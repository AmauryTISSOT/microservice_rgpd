using MicroserviceRgpd.AspireHost;
using MicroserviceRgpd.Infrastructure.Screenings.Embeddings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// Ce que les deux drapeaux de l'AppHost font naître côté Ollama. La décision vit dans un fichier de
/// l'AppHost compilé ici par lien : aucun hôte n'est construit ni démarré — même piège que celui que
/// <see cref="AppHostQualificationWiringTests"/> décrit —, et c'est pourtant <b>la</b> décision que
/// l'AppHost applique, non une copie.
/// </summary>
public class AppHostOllamaWiringTests
{
  [Fact]
  public void BothFlagsOffLeaveOllamaOutOfTheStack()
  {
    var wiring = OllamaWiring.For(llmIsOn: false, embeddingsAreOn: false);

    wiring.ShouldBe(new OllamaWiring(PullsQualificationModel: false, PullsEncoder: false, RequiresGpu: false));
    wiring.Exists.ShouldBeFalse();
  }

  /// <summary>
  /// La détection seule tient sur un poste sans carte graphique : l'encodeur est léger, et exiger le
  /// GPU pour lui ferait refuser de démarrer une pile qui n'en a pas besoin.
  /// </summary>
  [Fact]
  public void DetectionAloneBringsOllamaWithTheEncoderOnlyAndNoGpu()
  {
    var wiring = OllamaWiring.For(llmIsOn: false, embeddingsAreOn: true);

    wiring.ShouldBe(new OllamaWiring(PullsQualificationModel: false, PullsEncoder: true, RequiresGpu: false));
    wiring.Exists.ShouldBeTrue();
  }

  [Fact]
  public void LlmAloneKeepsTodaysStack()
  {
    var wiring = OllamaWiring.For(llmIsOn: true, embeddingsAreOn: false);

    wiring.ShouldBe(new OllamaWiring(PullsQualificationModel: true, PullsEncoder: false, RequiresGpu: true));
    wiring.Exists.ShouldBeTrue();
  }

  [Fact]
  public void BothFlagsOnShareOneOllamaThatPullsBothModels()
  {
    var wiring = OllamaWiring.For(llmIsOn: true, embeddingsAreOn: true);

    wiring.ShouldBe(new OllamaWiring(PullsQualificationModel: true, PullsEncoder: true, RequiresGpu: true));
    wiring.Exists.ShouldBeTrue();
  }

  [Theory]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData("false", false)]
  [InlineData("true", true)]
  [InlineData("True", true)]
  public void AFlagIsOffUnlessItSaysTrue(string? raw, bool expected)
  {
    AppHostFlag.IsOn("Screening:Embeddings:Enabled", raw).ShouldBe(expected);
  }

  [Theory]
  [InlineData("oui")]
  [InlineData("1")]
  [InlineData("ture")]
  public void AFlagThatIsNeitherTrueNorFalseStopsTheStart(string raw)
  {
    Should.Throw<InvalidOperationException>(() => AppHostFlag.IsOn("Screening:Embeddings:Enabled", raw))
      .Message.ShouldContain("Screening:Embeddings:Enabled");
  }

  /// <summary>
  /// Le tag tiré est celui du manifest, lu dans le fichier même que le service embarque : un tag
  /// recopié dans la configuration de l'AppHost finirait par tirer un encodeur que le modèle n'a
  /// jamais vu, et le service refuserait alors de détecter.
  /// </summary>
  [Fact]
  public void PullsTheEncoderTheArtefactWasMeasuredOn()
  {
    var manifest = Path.Combine(
      AppHostQualificationWiringTests.RepositoryRoot(),
      "src/MicroserviceRgpd.Infrastructure/Screenings/Embeddings/Artefact/manifest.json");

    EncoderModel.TagFrom(manifest).ShouldBe(A2Artefact.Load(A2Artefact.OpenEmbedded).EncoderTag);
  }
}

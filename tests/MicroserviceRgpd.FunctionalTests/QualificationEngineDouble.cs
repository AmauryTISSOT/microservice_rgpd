using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Un moteur de qualification reduit a ce dont un test de contrat a besoin : un avis qu on lui
/// dicte, ou un silence, et le texte qu il a recu.
/// </summary>
/// <remarks>
/// La doublure se pose sur le port, jamais sur des reponses HTTP enregistrees : un enregistrement
/// pourrit en silence et rend indiscernable une regression de code d une montee de moteur. Elle
/// tient indifferemment l un ou l autre role — c est precisement ce que « le meme port » veut dire.
/// </remarks>
/// <param name="confidence">
/// La confiance que ce moteur declare par defaut. Absente pour le lexique : le lexique n a aucun avis
/// sur sa propre fiabilite, et la doublure ne doit pas rendre atteignable en test un etat que le
/// vrai moteur n atteint jamais.
/// </param>
/// <param name="justification">La phrase que ce moteur justifie par defaut, ou rien s il ne justifie pas.</param>
/// <param name="engine">
/// L identite sous laquelle ce moteur se declare. Toujours presente : un avis dont on ignore de quel
/// moteur il releve est un avis dont la trace d audit ne pourrait plus repondre.
/// </param>
public sealed class QualificationEngineDouble(
  DeclaredConfidence? confidence = null,
  string? justification = null,
  QualificationEngineIdentity? engine = null) : IQualificationEngine
{
  private static readonly QualificationEngineIdentity Anonymous = new("double", "1.0.0");

  private readonly DeclaredConfidence? _confidence = confidence;
  private readonly string? _justification = justification;

  /// <summary>Le verdict que le moteur rendra au prochain appel.</summary>
  public Qualification Qualification { get; set; } = Qualification.OutOfScope;

  /// <summary>La confiance dont il assortira son avis.</summary>
  public DeclaredConfidence? DeclaredConfidence { get; set; } = confidence;

  /// <summary>La phrase par laquelle il justifiera son avis, ou rien.</summary>
  public string? Justification { get; set; } = justification;

  /// <summary>
  /// Ce qui empeche ce moteur de rendre un avis. Une seule propriete pour toutes les raisons : le
  /// domaine n a pas a les distinguer, un avis manquant est un avis manquant.
  /// </summary>
  public Exception? Silence { get; set; }

  /// <summary>L identite que le moteur joint a son avis.</summary>
  public QualificationEngineIdentity Engine { get; set; } = engine ?? Anonymous;

  /// <summary>Le texte tel que le moteur l a recu — ce qui permet de verifier ce que la frontiere en a fait.</summary>
  public RightsRequestText? ReceivedText { get; private set; }

  /// <summary>Le nombre d appels recus depuis la derniere remise a zero.</summary>
  public int CallCount { get; private set; }

  /// <summary>
  /// Ce que le moteur fait durer avant de repondre. Une seule chose l exige : verifier qu une
  /// annulation de l appelant interrompt un travail <b>en cours</b>, ce qu un moteur instantane ne
  /// laisse jamais observer.
  /// </summary>
  public TimeSpan Delay { get; set; } = TimeSpan.Zero;

  /// <summary>Signale que le moteur a commence a travailler, avant meme d avoir repondu.</summary>
  public Task Started => _started.Task;

  /// <summary>
  /// Vrai si le travail de ce moteur a ete interrompu par l annulation de l appelant. C est la seule
  /// facon de distinguer un travail reellement arrete d une reponse simplement ignoree.
  /// </summary>
  public bool Interrupted { get; private set; }

  private TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>
  /// Ramene la doublure a son etat de depart. La fabrique est partagee par toute la collection de
  /// tests : sans cela, un test qui omettrait de dicter son avis heriterait de celui du precedent,
  /// et passerait — ou echouerait — pour une raison qui ne le regarde pas.
  /// </summary>
  public void Reset()
  {
    Qualification = Qualification.OutOfScope;
    DeclaredConfidence = _confidence;
    Justification = _justification;
    Silence = null;
    Engine = engine ?? Anonymous;
    ReceivedText = null;
    CallCount = 0;
    Delay = TimeSpan.Zero;
    Interrupted = false;
    _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  }

  public async Task<QualificationOpinion> QualifyAsync(
    RightsRequestText text,
    CancellationToken cancellationToken = default)
  {
    ReceivedText = text;
    CallCount++;
    _started.TrySetResult();

    if (Delay > TimeSpan.Zero)
    {
      try
      {
        await Task.Delay(Delay, cancellationToken);
      }
      catch (OperationCanceledException)
      {
        // Le travail en cours s arrete vraiment : c est ce que le GPU exige, une generation
        // orpheline prenant la place de celui qui est reste.
        Interrupted = true;

        throw;
      }
    }

    if (Silence is not null)
    {
      throw Silence;
    }

    return new QualificationOpinion(Qualification, Engine, DeclaredConfidence, Justification);
  }
}

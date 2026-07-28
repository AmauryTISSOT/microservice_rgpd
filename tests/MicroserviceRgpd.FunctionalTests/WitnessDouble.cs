using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le moteur temoin reduit a ce dont un test de contrat a besoin : un verdict qu on lui dicte, et
/// le texte qu il a recu.
/// </summary>
/// <remarks>
/// La doublure se pose sur le port, jamais sur des reponses HTTP enregistrees : un enregistrement
/// pourrit en silence et rend indiscernable une regression de code d une montee de moteur.
/// </remarks>
public sealed class WitnessDouble : IQualificationEngine
{
  /// <summary>Le verdict que le moteur rendra au prochain appel.</summary>
  public Qualification Verdict { get; set; } = Qualification.OutOfScope;

  /// <summary>
  /// Ramene la doublure a son etat de depart. La fabrique est partagee par toute la collection de
  /// tests : sans cela, un test qui omettrait de dicter son verdict heriterait de celui du
  /// precedent, et passerait — ou echouerait — pour une raison qui ne le regarde pas.
  /// </summary>
  public void Reset()
  {
    Verdict = Qualification.OutOfScope;
    ReceivedText = null;
    CallCount = 0;
  }

  /// <summary>Le texte tel que le moteur l a recu — ce qui permet de verifier ce que la frontiere en a fait.</summary>
  public RightsRequestText? ReceivedText { get; private set; }

  /// <summary>Le nombre d appels recus depuis la derniere remise a zero.</summary>
  public int CallCount { get; private set; }

  public Task<QualificationOpinion> QualifyAsync(
    RightsRequestText text,
    CancellationToken cancellationToken = default)
  {
    ReceivedText = text;
    CallCount++;

    // Ni confiance, ni justification : le lexique n en produit pas, et la doublure ne doit pas
    // rendre atteignable en test un etat que le vrai moteur n atteint jamais.
    return Task.FromResult(new QualificationOpinion(Verdict));
  }
}

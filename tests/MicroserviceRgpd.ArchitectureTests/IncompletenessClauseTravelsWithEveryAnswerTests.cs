using Ardalis.Result;
using Mediator;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Toute réponse qui rend un <c>Screening</c> ou une <c>ScreenedColumn</c> porte la
/// <c>Clause d'incomplétude</c>.</b>
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Sans ce garde, la décision de cadrage 11 redevient une intention.</b> La clause a été
/// choisie <b>structurée plutôt qu'en prose</b> pour une seule raison — qu'elle soit
/// <b>testable</b> — et cet argument tombe rétroactivement s'il n'existe aucun test : une prose
/// aurait été plus lisible, elle n'a été écartée que parce qu'aucun test ne peut affirmer quoi que
/// ce soit sur le contenu d'un paragraphe.
/// </para>
/// <para>
/// <b>La règle porte sur les types rendus par les gestes UseCases</b>, et non sur des réponses HTTP :
/// <c>Screening</c> n'a aucune route publique, et sa surface est un jeu de commandes et de requêtes.
/// </para>
/// <para>
/// ⚠️ <b>Elle est écrite par la négative, et c'est délibéré.</b> Une liste des gestes qui doivent
/// porter la clause n'aurait protégé que ce qu'on a pensé à y écrire ; celle-ci interroge <b>tout</b>
/// ce que le contexte rend, et c'est le geste qu'on ajoutera un jour sans y penser qui la fera
/// rougir.
/// </para>
/// </remarks>
public class IncompletenessClauseTravelsWithEveryAnswerTests
{
  /// <summary>Ce qu'un rendu ne peut pas exposer sans clause : le rapport, et sa ligne.</summary>
  private static readonly Type[] WhatCannotTravelAlone = [typeof(Screening), typeof(ScreenedColumn)];

  /// <summary>
  /// Aucun gestionnaire du contexte ne rend un rapport ni une de ses lignes autrement que dans un
  /// <see cref="ScreeningAnswer{T}"/>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le dépôt rend une <c>ScreeningId</c></b>, et c'est ce qui le laisse passer : il ne
  /// <em>rend</em> aucun rapport à lire — il redirige vers la lecture, qui porte la clause. Rendre
  /// l'agrégat aurait ouvert un second chemin vers un rapport, sans clause, pour une commodité nulle.
  /// </remarks>
  [Fact]
  public void RendersNoScreeningNorScreenedColumnOutsideAScreeningAnswer()
  {
    var bare = HandlersOfTheContext()
      .Select(handler => (handler.Handler, Answer: AnswerOf(handler.Interface)))
      .Where(rendered => Exposes(rendered.Answer) && !IsAScreeningAnswer(rendered.Answer))
      .Select(rendered => $"  {rendered.Handler.FullName} → {Readable(rendered.Answer)}")
      .Order(StringComparer.Ordinal)
      .ToArray();

    bare.ShouldBeEmpty(
      "Un geste de Screening rend un rapport ou une de ses lignes sans clause d'incomplétude :"
      + Environment.NewLine + string.Join(Environment.NewLine, bare) + Environment.NewLine
      + "La clause est une propriété de la réponse, jamais une mention en pied de page : elle a été "
      + "choisie structurée pour être testable, et sans ce test l'argument qui l'a fait préférer à "
      + "une prose tombe.");
  }

  /// <summary>
  /// <b>Le garde mord, et on le prouve.</b> Un garde qu'on n'a jamais vu rouge est un garde dont on
  /// ne sait rien : on lui présente ici les deux formes qu'il doit refuser et celle qu'il doit
  /// laisser passer.
  /// </summary>
  [Fact]
  public void SeesAReportRenderedWithoutItsClause()
  {
    Exposes(typeof(Screening)).ShouldBeTrue();
    Exposes(typeof(IReadOnlyList<ScreenedColumn>)).ShouldBeTrue();
    Exposes(typeof(Result<Screening>)).ShouldBeTrue();

    // Ce qui passe : l'enveloppe qui porte la clause, y compris lorsqu'elle est elle-même enveloppée.
    IsAScreeningAnswer(typeof(ScreeningAnswer<Screening>)).ShouldBeTrue();
    IsAScreeningAnswer(typeof(Result<ScreeningAnswer<Screening>>)).ShouldBeTrue();

    // ⚠️ Et le déballage s'arrête à elle : sans cet arrêt, ScreeningAnswer<Screening> se lirait
    // comme une exposition nue, et le garde aurait crié rouge sur la seule forme correcte.
    Exposes(typeof(ScreeningAnswer<Screening>)).ShouldBeFalse();

    // Ce que le dépôt rend, et qui n'expose aucun rapport à lire.
    Exposes(typeof(Result<ScreeningId>)).ShouldBeFalse();
  }

  /// <summary>
  /// Le garde ne serait rien s'il ne trouvait aucun geste : un contexte sans gestionnaire
  /// respecterait la règle et ne servirait à rien.
  /// </summary>
  [Fact]
  public void FindsTheGesturesOfTheContextRatherThanAnEmptyList()
  {
    HandlersOfTheContext().ShouldNotBeEmpty(
      "Aucun geste de Screening n'a été trouvé dans UseCases : le garde afficherait vert pour "
      + "toujours.");
  }

  /// <summary>
  /// Les gestionnaires que le contexte héberge dans <c>UseCases</c>, avec l'interface de
  /// <c>Mediator</c> par laquelle ils répondent.
  /// </summary>
  private static IReadOnlyList<(Type Handler, Type Interface)> HandlersOfTheContext()
  {
    return
    [
      .. typeof(Constants).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: false, IsNested: false })
        .Where(type => type.Namespace?.Contains(ContextInspector.Screening, StringComparison.Ordinal) == true)
        .SelectMany(
          type => type.GetInterfaces().Where(IsAHandler),
          (type, contract) => (Handler: type, Interface: contract)),
    ];
  }

  /// <summary>Les deux contrats de <c>Mediator</c> par lesquels un geste rend quelque chose.</summary>
  private static bool IsAHandler(Type contract)
  {
    if (!contract.IsGenericType)
    {
      return false;
    }

    var definition = contract.GetGenericTypeDefinition();

    return definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>);
  }

  /// <summary>Ce que ce contrat rend : le second paramètre générique, toujours.</summary>
  private static Type AnswerOf(Type contract) => contract.GetGenericArguments()[1];

  private static bool IsAScreeningAnswer(Type answer)
  {
    return Unwrapped(answer).Any(part =>
      part.IsGenericType && part.GetGenericTypeDefinition() == typeof(ScreeningAnswer<>));
  }

  /// <summary>
  /// Ce type met-il un rapport ou une de ses lignes sous les yeux de quelqu'un ? Les enveloppes sont
  /// déballées — un <c>Result&lt;Screening&gt;</c> rend un rapport, une <c>IReadOnlyList</c> aussi.
  /// </summary>
  private static bool Exposes(Type answer)
  {
    return Unwrapped(answer).Any(part => WhatCannotTravelAlone.Contains(part));
  }

  /// <summary>
  /// Le type et tout ce qu'il enveloppe, récursivement : <c>Result&lt;T&gt;</c>, les collections,
  /// les nullables et les génériques imbriqués. ⚠️ Le déballage <b>s'arrête</b> à un
  /// <see cref="ScreeningAnswer{T}"/>, qui est précisément l'enveloppe qui porte la clause : le
  /// déballer ferait de <c>ScreeningAnswer&lt;Screening&gt;</c> une exposition nue.
  /// </summary>
  private static IEnumerable<Type> Unwrapped(Type answer)
  {
    yield return answer;

    if (!answer.IsGenericType)
    {
      yield break;
    }

    if (answer.GetGenericTypeDefinition() == typeof(ScreeningAnswer<>))
    {
      yield break;
    }

    foreach (var argument in answer.GetGenericArguments().SelectMany(Unwrapped))
    {
      yield return argument;
    }
  }

  private static string Readable(Type answer)
  {
    return answer.IsGenericType
      ? $"{answer.Name.Split('`')[0]}<{string.Join(", ", answer.GetGenericArguments().Select(Readable))}>"
      : answer.Name;
  }
}

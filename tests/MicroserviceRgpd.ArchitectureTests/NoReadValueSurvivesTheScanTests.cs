using System.Reflection;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Aucune valeur lue n'atteint ce qui est enregistré.</b> Un <c>ColumnPreview</c> vit en mémoire
/// du processus et meurt avec la session d'arbitrage ; <c>Screening</c>, <c>ScreenedColumn</c>,
/// <c>ScreenedListing</c> et <c>ColumnListing</c>, eux, descendent en base ou traversent la
/// détection. Rien du premier ne doit pouvoir se poser dans les seconds.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Sans ce garde, <c>Rien de réel ne reste</c> redevient une intention.</b> Le type descend
/// dans <c>Core</c> pour que le port sortant le nomme, et c'est très exactement ce qui le met à
/// portée de main de qui écrira, un jour, « et si on gardait les valeurs sur la ligne, pour ne pas
/// avoir à rescanner ». Ce jour-là, ce test est ce qui rougit.
/// </para>
/// <para>
/// ⚠️ <b>Il est écrit par la <em>fermeture transitive</em>, et c'est délibéré.</b> Un test qui
/// énumérerait les champs de ces quatre types n'aurait protégé que ce qu'on a pensé à y écrire :
/// celui-ci suit tout ce qu'on peut atteindre depuis leur surface publique, et c'est le type
/// intermédiaire qu'on ajoutera un jour sans y penser qui le fera rougir.
/// </para>
/// <para>
/// <b>La raison d'absence, elle, passe — et c'est le seul membre d'un aperçu qui passe.</b> Ce n'est
/// pas une entorse : <b>une raison n'est pas une valeur lue</b>. Sans elle, les quatre comptes de la
/// clause d'incomplétude seraient incalculables une heure après le scan.
/// </para>
/// </remarks>
public class NoReadValueSurvivesTheScanTests
{
  /// <summary>Ce qui reste : ce que la base enregistre, et ce que la détection se passe de main en main.</summary>
  private static readonly Type[] WhatSurvivesTheScan =
  [
    typeof(Screening),
    typeof(ScreenedColumn),
    typeof(ScreenedListing),
    typeof(ColumnListing),
  ];

  /// <summary>Ce qui meurt : l'aperçu, et la valeur qu'il porte.</summary>
  private static readonly Type[] WhatDiesWithTheSession =
  [
    typeof(ColumnPreview),
    typeof(PreviewedValue),
  ];

  [Fact]
  public void LetsNoReadValueReachAnythingThatSurvivesTheScan()
  {
    var caught = WhatSurvivesTheScan
      .SelectMany(survivor => ReachableFrom(survivor)
        .Where(WhatDiesWithTheSession.Contains)
        .Select(dying => $"  {survivor.Name} → {dying.Name}"))
      .Order(StringComparer.Ordinal)
      .ToArray();

    caught.ShouldBeEmpty(
      "Une valeur lue est atteignable depuis un type qui survit au scan :"
      + Environment.NewLine + string.Join(Environment.NewLine, caught) + Environment.NewLine
      + "Un ColumnPreview meurt avec la session d'arbitrage ; ce qui le rejoint sur une ligne "
      + "enregistrée fait du service un détenteur durable de données personnelles du client, avec "
      + "chiffrement au repos, purge et droit d'accès sur nos propres sauvegardes. Seule la raison "
      + "d'absence descend en base, et une raison n'est pas une valeur lue.");
  }

  /// <summary>
  /// <b>Le garde mord, et on le prouve.</b> Un garde qu'on n'a jamais vu rouge est un garde dont on
  /// ne sait rien : on lui présente ici la forme qu'il doit refuser — l'aperçu atteint bien sa
  /// valeur — et celle qu'il doit laisser passer.
  /// </summary>
  [Fact]
  public void SeesAValueThatMayNotTravelAndAReasonThatMay()
  {
    ReachableFrom(typeof(ColumnPreview)).ShouldContain(typeof(PreviewedValue));

    // La raison d'absence est enregistrée, et c'est écrit : elle est atteignable depuis la ligne.
    ReachableFrom(typeof(ScreenedColumn)).ShouldContain(typeof(PreviewAbsenceReason));

    // Et le garde trouve bien quelque chose à lire, sans quoi il afficherait vert pour toujours.
    ReachableFrom(typeof(Screening)).ShouldContain(typeof(ScreenedColumn));
  }

  /// <summary>
  /// Tout ce qu'on peut atteindre depuis la surface publique d'un type, de proche en proche, sans
  /// jamais sortir de <c>Core</c> — les types du cadre ne portent rien de ce contexte.
  /// </summary>
  private static HashSet<Type> ReachableFrom(Type root)
  {
    var reached = new HashSet<Type>();
    var toVisit = new Stack<Type>([root]);

    while (toVisit.TryPop(out var current))
    {
      foreach (var neighbour in NeighboursOf(current).SelectMany(Unwrapped))
      {
        if (neighbour.Assembly != typeof(Screening).Assembly || !reached.Add(neighbour))
        {
          continue;
        }

        toVisit.Push(neighbour);
      }
    }

    return reached;
  }

  /// <summary>
  /// Ce qu'un type nomme sur sa surface publique : ce qu'il porte, ce qu'il rend, et ce qu'on lui
  /// donne. ⚠️ <b>Les trois, et non les seules propriétés</b> — une fabrique qui prendrait un aperçu
  /// pour le poser ailleurs ne porterait aucune propriété, et serait passée.
  /// </summary>
  private static IEnumerable<Type> NeighboursOf(Type type)
  {
    const BindingFlags Surface =
      BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    foreach (var property in type.GetProperties(Surface))
    {
      yield return property.PropertyType;
    }

    foreach (var field in type.GetFields(Surface))
    {
      yield return field.FieldType;
    }

    foreach (var method in type.GetMethods(Surface).Cast<MethodBase>().Concat(type.GetConstructors(Surface)))
    {
      foreach (var parameter in method.GetParameters())
      {
        yield return parameter.ParameterType;
      }

      if (method is MethodInfo { ReturnType: var returned })
      {
        yield return returned;
      }
    }
  }

  /// <summary>
  /// Le type lui-même, et ceux qu'il enveloppe : <c>IReadOnlyList&lt;ColumnPreview&gt;</c> porte un
  /// aperçu tout autant qu'un champ nu, et un tableau aussi.
  /// </summary>
  private static IEnumerable<Type> Unwrapped(Type type)
  {
    yield return type;

    if (type.IsArray && type.GetElementType() is { } element)
    {
      foreach (var inner in Unwrapped(element))
      {
        yield return inner;
      }
    }

    if (!type.IsGenericType)
    {
      yield break;
    }

    foreach (var inner in type.GetGenericArguments().SelectMany(Unwrapped))
    {
      yield return inner;
    }
  }
}

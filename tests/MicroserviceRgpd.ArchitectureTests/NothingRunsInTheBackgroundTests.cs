using Mono.Cecil;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Rien ne tourne.</b> Aucun <c>IHostedService</c>, aucun <c>BackgroundService</c>, aucune
/// minuterie, aucun ordonnanceur : la file de l'<c>Operator</c> est une <b>requête</b>, évaluée à
/// chaque affichage.
/// </summary>
/// <remarks>
/// <para>
/// C'est le garde le plus important de la surface, et il ne porte pas sur le confort d'architecture :
/// un processus de fond interrompu rendrait une file <b>vide et rassurante</b>, soit l'<c>Omission
/// silencieuse</c> sous sa forme la plus dangereuse, et ferait dépendre la preuve de ce qu'un
/// <c>cron</c> ait tourné. Un retard non détecté deviendrait un retard inexistant.
/// </para>
/// <para>
/// <b>Il lit l'IL compilé, et non le code source.</b> Un test sur les seules déclarations de types
/// afficherait vert sur un <c>new Timer(…)</c> caché dans un corps de méthode — c'est-à-dire sur la
/// dérive même que l'on craint. Ici, une variable locale, un <c>call</c> et un champ valent aveu au
/// même titre qu'une classe qui hériterait de <c>BackgroundService</c>.
/// </para>
/// <para>
/// ⚠️ <b><c>TimeProvider</c> n'est pas visé, et c'est délibéré.</b> Il <em>dit l'heure</em> quand on
/// la lui demande ; il ne rappelle personne. Ce que le garde interdit est ce qui <em>déclenche</em>.
/// C'est pourquoi les noms sont énumérés en toutes lettres plutôt que reconnus par un motif :
/// « Timer » attraperait <c>TimeProvider</c>, et un garde qui crie faux finit par être désactivé.
/// </para>
/// <para>
/// ⚠️ <b>La liste est écrite à la main, et l'allonger est un geste délibéré.</b> Le jour où une
/// bibliothèque d'ordonnancement entre dans le dépôt, c'est ici qu'on le verra — et la bonne réponse
/// sera de la sortir, jamais d'assouplir la règle.
/// </para>
/// </remarks>
public class NothingRunsInTheBackgroundTests
{
  /// <summary>
  /// Ce qui <b>déclenche</b> tout seul, par son nom complet. Ni <c>TimeProvider</c>, qui répond quand
  /// on l'interroge, ni <c>Task.Delay</c>, qui attend dans un échange déjà en cours à la demande de
  /// quelqu'un.
  /// </summary>
  private static readonly string[] WhatWouldRunOnItsOwn =
  [
    "Microsoft.Extensions.Hosting.IHostedService",
    "Microsoft.Extensions.Hosting.IHostedLifecycleService",
    "Microsoft.Extensions.Hosting.BackgroundService",
    "System.Threading.Timer",
    "System.Threading.PeriodicTimer",
    "System.Timers.Timer",
    "Microsoft.Extensions.Hosting.ITimer",
  ];

  /// <summary>
  /// Les paquets qu'un ordonnanceur amène avec lui. Reconnus par le <b>début</b> de leur espace de
  /// noms : aucun type de ces familles n'a de raison d'entrer, quel que soit son nom.
  /// </summary>
  private static readonly string[] SchedulingLibraries = ["Quartz", "Hangfire", "Cronos", "NCrontab"];

  public static TheoryData<string> ProductionAssemblies => [.. ProductionAssembly.All];

  [Theory]
  [MemberData(nameof(ProductionAssemblies))]
  public void NoAssemblyCarriesAnythingThatWouldRunOnItsOwn(string assembly)
  {
    var machinery = MachineryIn(ProductionAssembly.PathOf(assembly));

    machinery.ShouldBeEmpty(
      $"{assembly} porte de la machinerie qui tourne :" + Environment.NewLine
      + string.Join(Environment.NewLine, machinery) + Environment.NewLine
      + "La file est une requête, évaluée à chaque affichage : un processus arrêté rendrait une file "
      + "vide et rassurante, et un retard non détecté deviendrait un retard inexistant.");
  }

  /// <summary>
  /// <b>Le garde mord, et on le prouve.</b> Il voit la minuterie du témoin
  /// <see cref="Fixtures.Casework.AScreenThatWouldRunOnItsOwn"/> — y compris celle qui se cache dans un
  /// corps de méthode, à l'endroit qu'un test de signatures ne regarderait pas.
  /// </summary>
  /// <remarks>
  /// Un garde qu'on n'a jamais vu rouge est un garde dont on ne sait rien : celui-ci s'exerce sur
  /// l'assemblage de tests, où le témoin vit, et jamais sur la production, qui doit rester vide.
  /// </remarks>
  [Fact]
  public void SeesTheMachineryEvenWhenItHidesInAMethodBody()
  {
    var machinery = MachineryIn(Path.Combine(AppContext.BaseDirectory, "MicroserviceRgpd.ArchitectureTests.dll"));

    machinery.ShouldContain(found => found.Contains("System.Threading.PeriodicTimer", StringComparison.Ordinal));
    machinery.ShouldContain(found => found.Contains("System.Threading.Timer", StringComparison.Ordinal));
  }

  /// <summary>
  /// <b>Aucun drapeau persisté d'échéance non plus.</b> Le dépassement est un calcul sur la date de
  /// réception ; une colonne qui le mémoriserait aurait besoin de quelque chose pour la mettre à
  /// jour — et ce quelque chose serait le processus que le garde ci-dessus interdit.
  /// </summary>
  /// <remarks>
  /// La règle porte sur le <b>domaine</b>, là où un état se persiste : les propriétés de tous ses
  /// types sont lues, et aucune ne doit nommer un retard, une échéance atteinte ou une relance. Ce
  /// que le domaine ne sait pas dire, aucune migration n'aura de raison d'écrire.
  /// </remarks>
  [Fact]
  public void NoDomainTypeCarriesAPersistedFlagOfADeadlineReached()
  {
    using var core = ModuleDefinition.ReadModule(ProductionAssembly.PathOf("MicroserviceRgpd.Core"));

    var flags = core.GetTypes()
      .Where(type => type.DeclaringType is null)
      .SelectMany(type => type.Properties.Select(property => $"  {type.Name}.{property.Name}"))
      .Where(name => Suspicious(name))
      .Order(StringComparer.Ordinal)
      .ToArray();

    flags.ShouldBeEmpty(
      "Le domaine porte ce qui ressemble à un drapeau d'échéance :" + Environment.NewLine
      + string.Join(Environment.NewLine, flags) + Environment.NewLine
      + "Il n'existe pas d'état « en retard » : le dépassement est un calcul fait à l'instant où "
      + "l'Operator regarde, et un état persisté ferait dépendre la preuve d'une minuterie.");
  }

  /// <summary>
  /// Les mots qui trahiraient un état atteint plutôt qu'un fait déclaré. <c>Deadline</c> n'en est pas :
  /// une échéance <b>calculée</b> est un fait du calendrier, et <c>StatutoryDeadline</c> ne se persiste
  /// nulle part.
  /// </summary>
  private static bool Suspicious(string name)
  {
    return name.Contains("IsLate", StringComparison.Ordinal)
      || name.Contains("IsOverdue", StringComparison.Ordinal)
      || name.Contains("Overdue", StringComparison.Ordinal)
      || name.Contains("DeadlineReached", StringComparison.Ordinal)
      || name.Contains("Reminded", StringComparison.Ordinal)
      || name.Contains("NextRun", StringComparison.Ordinal)
      || name.Contains("LastRun", StringComparison.Ordinal)
      || name.Contains("Attempts", StringComparison.Ordinal);
  }

  /// <summary>
  /// Tout ce que l'IL de cet assemblage met en jeu et qui tournerait de lui-même : héritage,
  /// interfaces, champs, propriétés, signatures et <b>corps de méthode</b>.
  /// </summary>
  private static IReadOnlyList<string> MachineryIn(string assemblyPath)
  {
    using var module = ModuleDefinition.ReadModule(assemblyPath);

    return
    [
      .. module.GetTypes()
        .SelectMany(type => Reaches(type).Select(reached => (Type: type, Reached: reached)))
        .Where(found => WouldRun(found.Reached))
        .Select(found => $"  {found.Type.FullName} → {found.Reached}")
        .Distinct()
        .Order(StringComparer.Ordinal),
    ];
  }

  private static bool WouldRun(string fullName)
  {
    return WhatWouldRunOnItsOwn.Contains(fullName, StringComparer.Ordinal)
      || SchedulingLibraries.Any(library =>
        fullName.StartsWith(library + ".", StringComparison.Ordinal));
  }

  /// <summary>
  /// Les noms complets des types que ce type met en jeu, où qu'ils se trouvent. Les génériques sont
  /// déballés : un <c>List&lt;Timer&gt;</c> nomme <c>Timer</c>.
  /// </summary>
  private static IEnumerable<string> Reaches(TypeDefinition type)
  {
    foreach (var reached in Named(type.BaseType))
    {
      yield return reached;
    }

    foreach (var reached in type.Interfaces.SelectMany(implemented => Named(implemented.InterfaceType)))
    {
      yield return reached;
    }

    foreach (var reached in type.Fields.SelectMany(field => Named(field.FieldType)))
    {
      yield return reached;
    }

    foreach (var reached in type.Properties.SelectMany(property => Named(property.PropertyType)))
    {
      yield return reached;
    }

    foreach (var method in type.Methods)
    {
      foreach (var reached in Named(method.ReturnType))
      {
        yield return reached;
      }

      foreach (var reached in method.Parameters.SelectMany(parameter => Named(parameter.ParameterType)))
      {
        yield return reached;
      }

      foreach (var reached in Body(method))
      {
        yield return reached;
      }
    }
  }

  private static IEnumerable<string> Body(MethodDefinition method)
  {
    if (!method.HasBody)
    {
      return [];
    }

    return method.Body.Variables.SelectMany(local => Named(local.VariableType))
      .Concat(method.Body.Instructions.SelectMany(instruction => Named(instruction.Operand)));
  }

  private static IEnumerable<string> Named(object? operand)
  {
    return operand switch
    {
      TypeReference type => Named(type),
      GenericInstanceMethod generic => Named(generic.DeclaringType)
        .Concat(generic.GenericArguments.SelectMany(Named)),
      MethodReference method => Named(method.DeclaringType).Concat(Named(method.ReturnType)),
      FieldReference field => Named(field.DeclaringType).Concat(Named(field.FieldType)),
      _ => [],
    };
  }

  private static IEnumerable<string> Named(TypeReference? reference)
  {
    switch (reference)
    {
      case null or GenericParameter:
        return [];
      case GenericInstanceType generic:
        return Named(generic.ElementType).Concat(generic.GenericArguments.SelectMany(Named));
      case TypeSpecification specification:
        return Named(specification.ElementType);
      default:
        return [reference.FullName];
    }
  }
}

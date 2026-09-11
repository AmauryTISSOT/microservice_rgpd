using Mono.Cecil.Cil;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// Lit l'<b>IL compilé</b> d'un assemblage et dit quels types d'un contexte en atteignent un autre.
/// <para>
/// L'IL plutôt que la réflexion, et l'IL plutôt que les signatures : la réflexion ne montre pas ce
/// qu'un corps de méthode appelle, et une surface publique irréprochable peut couvrir un appel
/// direct au moteur d'en face — la fuite même que l'on craint. Ici, un <c>call</c>, un
/// <c>ldsfld</c>, une variable locale ou un <c>catch</c> valent aveu au même titre qu'un paramètre.
/// </para>
/// <para>
/// L'appartenance d'un type à un contexte se lit à son <b>espace de noms</b>, parce que le dépôt
/// est découpé par couche et que les contextes s'y lisent au dossier — <c>Core/Qualifications/</c>,
/// <c>Core/Casework/</c>, <c>Core/SharedKernel/</c>, et de même dans les trois autres couches.
/// Un segment est reconnu par son <b>préfixe</b> : le dossier dit <c>Qualifications</c> là où le
/// contexte se nomme <c>Qualification</c>.
/// </para>
/// <para>
/// ⚠️ Le préfixe attrape aussi ce qui n'est pas un pluriel : un dossier <c>Configurations/</c> se lit
/// comme du <c>Configuration</c>. C'est pourquoi le point de montage de <c>Web</c>, qui portait ce
/// nom par héritage du gabarit, s'appelle <c>Composition/</c> depuis l'ADR-0016.
/// </para>
/// </summary>
internal static class ContextInspector
{
  internal const string Casework = "Casework";
  internal const string Qualification = "Qualification";
  internal const string Screening = "Screening";
  internal const string Configuration = "Configuration";
  internal const string SharedKernel = "SharedKernel";

  /// <summary>
  /// Tout ce qui peut être d'un côté ou de l'autre d'une frontière : les quatre contextes bornés, et
  /// le noyau partagé qui n'en est pas un. La liste est écrite en toutes lettres plutôt que
  /// découverte, et <see cref="ContextRosterTests"/> l'ancre sur les glossaires du dépôt — un nom
  /// qui ne désignerait aucun dossier réel rendrait ses règles vertes pour toujours.
  /// </summary>
  internal static readonly string[] All = [Qualification, Casework, Screening, Configuration, SharedKernel];

  /// <summary>
  /// Toutes les traversées <paramref name="from"/> → <paramref name="to"/> portées par l'IL de
  /// <paramref name="assemblyPath"/>. Vide vaut respect de la règle.
  /// </summary>
  internal static IReadOnlyList<CrossContextReference> Inspect(string assemblyPath, string from, string to)
  {
    using var module = ModuleDefinition.ReadModule(assemblyPath);
    var assembly = Path.GetFileNameWithoutExtension(assemblyPath);

    // Une même traversée se relève plusieurs fois — un ldsfld nomme à la fois le type qui déclare
    // le champ et le type du champ. On la rapporte une fois : un rapport qui se répète se lit mal.
    return
    [
      .. module.GetTypes()
        .Where(type => BelongsTo(type, from))
        .SelectMany(type => Reaches(type)
          .Where(reached => BelongsTo(reached.Type, to))
          .Select(reached => new CrossContextReference(assembly, FullNameOf(type), FullNameOf(reached.Type), reached.Site)))
        .Distinct(),
    ];
  }

  /// <summary>
  /// Les types du dépôt qui n'habitent <b>aucun</b> contexte et qui en atteignent <b>plusieurs</b>,
  /// avec la liste de ceux qu'ils touchent. Vide vaut respect de la règle.
  /// </summary>
  /// <remarks>
  /// <para>
  /// C'est l'angle mort de la matrice, et il est large d'un fichier :
  /// <c>MicroserviceRgpd.Infrastructure.Data.AppDbContext</c> ne porte aucun segment de contexte
  /// dans son espace de noms, n'est donc <b>jamais un <c>from</c> ni un <c>to</c></b>, et
  /// n'apparaît dans aucune paire ordonnée. Un
  /// <c>Infrastructure/Data/Reporting/ScreeningExportService.cs</c> qui lirait les colonnes
  /// retenues pour pré-remplir le <c>Manifest</c> laisserait <c>ContextIsolationTests</c> vert sur
  /// toute la matrice.
  /// </para>
  /// <para>
  /// ⚠️ <b>Les types imbriqués et engendrés remontent à celui qui les entoure</b>, et ne sont pas
  /// écartés comme dans <see cref="TypesIn"/>. La question n'est pas ici « qui habite ce dossier »
  /// mais « que touche ce fichier » : deux traversées cachées dans deux fermetures d'une même
  /// classe sont deux contextes atteints par elle, et les compter séparément aurait rendu le garde
  /// contournable par une lambda.
  /// </para>
  /// </remarks>
  internal static IReadOnlyList<ContextlessReach> ContextlessTypesReachingSeveralContexts(string assemblyPath)
  {
    using var module = ModuleDefinition.ReadModule(assemblyPath);
    var assembly = Path.GetFileNameWithoutExtension(assemblyPath);

    return
    [
      .. module.GetTypes()
        .Where(BelongsToNoContext)
        .GroupBy(type => FullNameOf(Outermost(type)))
        .Select(sameFile => new ContextlessReach(
          assembly,
          sameFile.Key,
          [
            .. All.Where(context => sameFile
              .SelectMany(Reaches)
              .Any(reached => BelongsTo(reached.Type, context)))
              .Order(StringComparer.Ordinal),
          ]))
        .Where(reach => reach.Contexts.Count > 1)
        .OrderBy(reach => reach.Type, StringComparer.Ordinal),
    ];
  }

  /// <summary>
  /// Les types de premier plan qu'un contexte héberge dans cet assemblage. Les types imbriqués sont
  /// hors sujet : la question posée est celle des habitants d'un dossier, et le compilateur en
  /// engendre d'autres — fermetures, machines à états — qui ne sont écrits par personne.
  /// </summary>
  internal static IReadOnlyList<string> TypesIn(string assemblyPath, string context)
  {
    using var module = ModuleDefinition.ReadModule(assemblyPath);

    return
    [
      .. module.GetTypes()
        .Where(type => type.DeclaringType is null && !IsCompilerGenerated(type) && BelongsTo(type, context))
        .Select(FullNameOf),
    ];
  }

  /// <summary>
  /// Tout ce que ce type atteint, et l'endroit d'où il l'atteint. La surface d'abord — héritage,
  /// champs, propriétés, signatures, attributs — puis les corps de méthode, que rien d'autre ne
  /// regarde.
  /// </summary>
  private static IEnumerable<(TypeReference Type, string Site)> Reaches(TypeDefinition type)
  {
    const string Surface = "signature";

    foreach (var reached in Unwrap(type.BaseType).Concat(type.Interfaces.SelectMany(implemented => Unwrap(implemented.InterfaceType))))
    {
      yield return (reached, Surface);
    }

    foreach (var reached in Attributed(type).Concat(Constrained(type.GenericParameters)))
    {
      yield return (reached, Surface);
    }

    foreach (var reached in type.Fields.SelectMany(field => Unwrap(field.FieldType).Concat(Attributed(field))))
    {
      yield return (reached, Surface);
    }

    foreach (var reached in type.Properties.SelectMany(property => Unwrap(property.PropertyType).Concat(Attributed(property))))
    {
      yield return (reached, Surface);
    }

    foreach (var reached in type.Events.SelectMany(raised => Unwrap(raised.EventType).Concat(Attributed(raised))))
    {
      yield return (reached, Surface);
    }

    foreach (var method in type.Methods)
    {
      var signature = Unwrap(method.ReturnType)
        .Concat(method.Parameters.SelectMany(parameter => Unwrap(parameter.ParameterType)))
        .Concat(Attributed(method))
        .Concat(Constrained(method.GenericParameters));

      foreach (var reached in signature)
      {
        yield return (reached, $"signature de {method.Name}");
      }

      foreach (var reached in Body(method))
      {
        yield return (reached, $"corps de {method.Name}");
      }
    }
  }

  /// <summary>
  /// Ce qu'un corps de méthode touche : ses variables locales, les types qu'il rattrape, et les
  /// opérandes de ses instructions — types, méthodes appelées et champs lus ou écrits, avec le type
  /// qui les déclare.
  /// </summary>
  private static IEnumerable<TypeReference> Body(MethodDefinition method)
  {
    if (!method.HasBody)
    {
      return [];
    }

    var body = method.Body;

    return body.Variables.SelectMany(local => Unwrap(local.VariableType))
      .Concat(body.ExceptionHandlers.SelectMany(handler => Unwrap(handler.CatchType)))
      .Concat(body.Instructions.SelectMany(instruction => Unwrap(instruction.Operand)));
  }

  /// <summary>Les types qu'une opérande d'instruction met en jeu, quelle que soit sa forme.</summary>
  private static IEnumerable<TypeReference> Unwrap(object? operand)
  {
    return operand switch
    {
      TypeReference type => Unwrap(type),
      GenericInstanceMethod generic => Called(generic)
        .Concat(generic.GenericArguments.SelectMany(Unwrap)),
      MethodReference method => Called(method),
      FieldReference field => Unwrap(field.DeclaringType).Concat(Unwrap(field.FieldType)),
      CallSite site => Unwrap(site.ReturnType)
        .Concat(site.Parameters.SelectMany(parameter => Unwrap(parameter.ParameterType))),
      _ => [],
    };
  }

  /// <summary>
  /// Ce qu'une méthode appelée met en jeu : le type qui la déclare, ce qu'elle rend, et ce qu'elle
  /// prend.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle est nommée, et n'est surtout pas une surcharge de <c>Unwrap</c>.</b> Un
  /// <c>GenericInstanceMethod</c> transtypé en <c>MethodReference</c> se relierait à la surcharge
  /// <c>object</c>, dont la branche générique est choisie sur le type <b>réel</b> de l'opérande :
  /// la méthode se rappellerait elle-même sans fin, et le garde tomberait par débordement de pile
  /// au lieu d'afficher rouge ou vert. Un appel générique — <c>Select&lt;T, R&gt;</c> — suffit à le
  /// déclencher, ce que le premier contexte à porter du code a immédiatement produit.
  /// </remarks>
  private static IEnumerable<TypeReference> Called(MethodReference method)
  {
    return Unwrap(method.DeclaringType)
      .Concat(Unwrap(method.ReturnType))
      .Concat(method.Parameters.SelectMany(parameter => Unwrap(parameter.ParameterType)));
  }

  /// <summary>
  /// Déballe un type composé jusqu'aux types nommés qu'il contient : <c>List&lt;Qualification&gt;</c>
  /// atteint <c>Qualification</c>, et un tableau ou une référence de la même manière. Sans cela, un
  /// contexte s'atteindrait derrière n'importe quel générique.
  /// </summary>
  private static IEnumerable<TypeReference> Unwrap(TypeReference? reference)
  {
    switch (reference)
    {
      case null or GenericParameter:
        return [];
      case GenericInstanceType generic:
        return Unwrap(generic.ElementType).Concat(generic.GenericArguments.SelectMany(Unwrap));
      case TypeSpecification specification:
        return Unwrap(specification.ElementType);
      default:
        return [reference];
    }
  }

  /// <summary>
  /// L'attribut lui-même, et ce qu'il nomme : un <c>typeof</c> passé en argument lie les deux
  /// assemblages aussi sûrement qu'un appel — <c>[JsonConverter(typeof(…))]</c> en est l'exemple
  /// que ce dépôt écrit déjà — et ne se voit ni en signature, ni dans un corps de méthode.
  /// </summary>
  private static IEnumerable<TypeReference> Attributed(ICustomAttributeProvider provider)
  {
    return provider.CustomAttributes.SelectMany(
      attribute => Unwrap(attribute.AttributeType)
        .Concat(attribute.ConstructorArguments.SelectMany(Unwrap))
        .Concat(attribute.Properties.Select(named => named.Argument).SelectMany(Unwrap))
        .Concat(attribute.Fields.Select(named => named.Argument).SelectMany(Unwrap)));
  }

  /// <summary>
  /// Un argument d'attribut porte deux types : le sien — une énumération d'en face compte — et,
  /// s'il s'agit d'un <c>typeof</c>, celui qu'il désigne. Les tableaux et les arguments boîtés
  /// dans un paramètre <c>object</c> se déballent d'un cran.
  /// </summary>
  private static IEnumerable<TypeReference> Unwrap(CustomAttributeArgument argument)
  {
    var named = argument.Value switch
    {
      TypeReference designated => Unwrap(designated),
      CustomAttributeArgument boxed => Unwrap(boxed),
      CustomAttributeArgument[] several => several.SelectMany(Unwrap),
      _ => [],
    };

    return Unwrap(argument.Type).Concat(named);
  }

  private static IEnumerable<TypeReference> Constrained(IEnumerable<GenericParameter> parameters)
  {
    return parameters.SelectMany(parameter => parameter.Constraints.SelectMany(constraint => Unwrap(constraint.ConstraintType)));
  }

  /// <summary>
  /// Le dépôt, et lui seul. Un type de bibliothèque n'habite <b>aucun</b> de nos contextes, quel que
  /// soit le mot qu'il porte dans son espace de noms.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Sans cette borne, <c>Ardalis.SharedKernel.IAggregateRoot</c> se lit comme le noyau partagé
  /// du dépôt</b>, et le premier agrégat de <c>Screening</c> se dénonce en implémentant un marqueur de
  /// paquet. Le noyau partagé de ce dépôt est <c>MicroserviceRgpd.Core.SharedKernel</c> — la taxonomie
  /// écrite par le RGPD — et c'est de lui, et de rien d'autre, que <c>docs/adr/0003</c> tient
  /// <c>Screening</c> à l'écart. Le cas ne s'était jamais présenté : les deux contextes qui portaient
  /// du code ont tous deux la traversée vers le noyau <b>permise</b>, et le faux positif y était
  /// couvert par une permission légitime.
  /// <para>
  /// <b>Ce n'est pas un élargissement de la liste blanche</b>, qui reste à deux lignes : c'est
  /// l'inspecteur qui cesse de confondre un paquet NuGet avec un contexte du dépôt.
  /// </para>
  /// </remarks>
  private const string Repository = "MicroserviceRgpd.";

  /// <summary>
  /// L'espace de noms d'un type imbriqué est vide : c'est celui du type qui l'entoure qui dit où il
  /// vit. Une fermeture engendrée dans un gestionnaire de <c>Casework</c> reste du <c>Casework</c>.
  /// </summary>
  private static TypeReference Outermost(TypeReference reference)
  {
    var outermost = reference;

    while (outermost.DeclaringType is not null)
    {
      outermost = outermost.DeclaringType;
    }

    return outermost;
  }

  /// <summary>
  /// Un type <b>du dépôt</b> qui n'habite aucun des contextes déclarés. Un type de bibliothèque n'en
  /// est pas un : il n'a jamais eu de frontière à respecter.
  /// </summary>
  private static bool BelongsToNoContext(TypeReference reference)
  {
    var inhabited = Outermost(reference).Namespace ?? string.Empty;

    return inhabited.StartsWith(Repository, StringComparison.Ordinal)
      && !All.Any(context => BelongsTo(reference, context));
  }

  private static bool BelongsTo(TypeReference reference, string context)
  {
    var inhabited = Outermost(reference).Namespace ?? string.Empty;

    if (!inhabited.StartsWith(Repository, StringComparison.Ordinal))
    {
      return false;
    }

    return inhabited
      .Split('.')
      .Any(segment => segment.StartsWith(context, StringComparison.Ordinal));
  }

  private static string FullNameOf(TypeReference reference)
  {
    return reference.FullName.Replace('/', '+');
  }

  private static bool IsCompilerGenerated(ICustomAttributeProvider provider)
  {
    return provider.CustomAttributes.Any(
      attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
  }
}

/// <summary>
/// Une traversée de frontière relevée dans l'IL : qui atteint quoi, et d'où. Le lieu compte autant
/// que le fait — « corps de <c>Handle</c> » et « signature » ne se corrigent pas de la même façon.
/// </summary>
internal sealed record CrossContextReference(string Assembly, string SourceType, string TargetType, string Site)
{
  public override string ToString()
  {
    return $"  {SourceType} → {TargetType}  ({Site}, dans {Assembly})";
  }
}

/// <summary>
/// Un type qui n'appartient à aucun contexte, et les contextes qu'il atteint. Le rapport les nomme
/// tous : « il en touche deux » n'apprend rien à qui doit décider lequel sortir.
/// </summary>
internal sealed record ContextlessReach(string Assembly, string Type, IReadOnlyList<string> Contexts)
{
  public override string ToString()
  {
    return $"  {Type} → {string.Join(", ", Contexts)}  (dans {Assembly})";
  }
}

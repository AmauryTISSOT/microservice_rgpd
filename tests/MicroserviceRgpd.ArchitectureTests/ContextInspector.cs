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
/// </summary>
internal static class ContextInspector
{
  internal const string Casework = "Casework";
  internal const string Qualification = "Qualification";
  internal const string SharedKernel = "SharedKernel";

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
      GenericInstanceMethod generic => Unwrap((MethodReference)generic)
        .Concat(generic.GenericArguments.SelectMany(Unwrap)),
      MethodReference method => Unwrap(method.DeclaringType)
        .Concat(Unwrap(method.ReturnType))
        .Concat(method.Parameters.SelectMany(parameter => Unwrap(parameter.ParameterType))),
      FieldReference field => Unwrap(field.DeclaringType).Concat(Unwrap(field.FieldType)),
      CallSite site => Unwrap(site.ReturnType)
        .Concat(site.Parameters.SelectMany(parameter => Unwrap(parameter.ParameterType))),
      _ => [],
    };
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

  private static IEnumerable<TypeReference> Attributed(ICustomAttributeProvider provider)
  {
    return provider.CustomAttributes.SelectMany(attribute => Unwrap(attribute.AttributeType));
  }

  private static IEnumerable<TypeReference> Constrained(IEnumerable<GenericParameter> parameters)
  {
    return parameters.SelectMany(parameter => parameter.Constraints.SelectMany(constraint => Unwrap(constraint.ConstraintType)));
  }

  /// <summary>
  /// L'espace de noms d'un type imbriqué est vide : c'est celui du type qui l'entoure qui dit où il
  /// vit. Une fermeture engendrée dans un gestionnaire de <c>Casework</c> reste du <c>Casework</c>.
  /// </summary>
  private static bool BelongsTo(TypeReference reference, string context)
  {
    var outermost = reference;

    while (outermost.DeclaringType is not null)
    {
      outermost = outermost.DeclaringType;
    }

    return (outermost.Namespace ?? string.Empty)
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

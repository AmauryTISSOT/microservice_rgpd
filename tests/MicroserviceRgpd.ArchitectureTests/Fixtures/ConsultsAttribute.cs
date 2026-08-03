namespace MicroserviceRgpd.ArchitectureTests.Fixtures;

/// <summary>
/// Un attribut qui nomme un type. Il vit hors des deux contextes-témoins, exprès : ce qui traverse
/// la frontière est son <b>argument</b>, pas lui.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class ConsultsAttribute : Attribute
{
  internal ConsultsAttribute(Type engine)
  {
    Engine = engine;
  }

  internal Type Engine { get; }
}

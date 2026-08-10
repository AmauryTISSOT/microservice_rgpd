using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Screening;

/// <summary>
/// Le témoin positif, et il est l'exact pendant du précédent : un type de <c>Screening</c> qui
/// atteint le <b>vrai</b> noyau partagé du dépôt.
/// <para>
/// C'est le geste que <c>docs/adr/0003</c> interdit nommément — rattacher une colonne à un
/// <c>DataSubjectRight</c> — et il doit rester <b>vu</b> après que l'inspecteur a appris à ignorer
/// les paquets. Sans lui, la borne posée sur les espaces de noms pourrait tout éteindre sans que
/// rien ne passe au rouge.
/// </para>
/// </summary>
internal sealed class AScreeningTypeThatReachesTheSharedKernel
{
  internal static string Attach()
  {
    return DataSubjectRight.Access.Name;
  }
}

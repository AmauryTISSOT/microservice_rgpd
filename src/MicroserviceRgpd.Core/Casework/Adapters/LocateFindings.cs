namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce qu'un <c>locate</c> a rendu, <b>une fois relu</b> : un noyau certain de références opaques, et
/// des réserves motivées que seul un humain tranchera.
/// </summary>
/// <remarks>
/// <para>
/// <b>La forme vient du prototypage, et la prose seule ne l'avait pas trouvée.</b> Un <c>Adapter</c>
/// qui ne rendrait qu'une liste obligerait l'application à trancher elle-même le doute — c'est-à-dire
/// à fusionner à tort, ou à écarter par prudence. Les deux erreurs sont graves et l'une est
/// irréversible : le doute remonte donc <b>tel quel</b>, motivé, jusqu'à l'<c>Operator</c>.
/// </para>
/// <para>
/// ⚠️ <b>Rien n'est complété, et rien n'est écarté en silence.</b> Une réserve sans référence lisible
/// ou sans motif lisible, une désignation d'une nature que le vocabulaire ignore : tout cela est une
/// <see cref="AdapterFailure"/> — une <b>panne</b> —, jamais une ligne qu'on laisse tomber. Écarter
/// en silence une ligne qui nomme les données de quelqu'un serait l'<c>Omission silencieuse</c>
/// fabriquée par le service lui-même, et le motif que le service aurait inventé deviendrait, une
/// heure plus tard, une prose que personne n'a écrite.
/// </para>
/// </remarks>
/// <param name="Certain">Le noyau certain, sans doublon et dans l'ordre où l'<c>Adapter</c> l'a rendu.</param>
/// <param name="Reserved">Les réserves, nées <see cref="ReservationState.Awaiting"/>.</param>
public sealed record LocateFindings(
  IReadOnlyList<OpaqueReference> Certain,
  IReadOnlyList<Reservation> Reserved)
{
  /// <summary>Un <c>Locate</c> sans rattachement : le <b>zéro unique</b>, sans variante à distinguer.</summary>
  public static LocateFindings Nothing { get; } = new([], []);

  /// <summary>Le service détient-il un rattachement certain, ou une réserve à faire arbitrer ?</summary>
  public bool IsNothing => Certain.Count == 0 && Reserved.Count == 0;

  /// <summary>
  /// Relit ce que le fil a porté, ou déclare la <b>panne</b> d'un corps que le contrat ne prévoit
  /// pas.
  /// </summary>
  /// <param name="served">Le corps servi, ou <c>null</c> — un corps absent est le zéro unique.</param>
  /// <param name="declaredSystem">Le système à nommer si le corps n'est pas relisible.</param>
  /// <exception cref="AdapterFailure">Une réserve, une référence ou une désignation n'est pas relisible.</exception>
  public static LocateFindings ReadFrom(LocateOnTheWire? served, DeclaredSystemId declaredSystem)
  {
    if (served is null)
    {
      return Nothing;
    }

    var certain = new List<OpaqueReference>();

    foreach (var reference in served.Certain ?? [])
    {
      var read = Read(() => OpaqueReference.Of(reference), declaredSystem);

      if (!certain.Contains(read))
      {
        certain.Add(read);
      }
    }

    var reserved = new List<Reservation>();

    foreach (var one in served.Reserved ?? [])
    {
      var read = ReservationFrom(one, declaredSystem);

      // Deux fois la même référence, ce serait deux fois la même ligne : la seconde n'apprend rien
      // et ferait arbitrer deux fois le même doute.
      if (!reserved.Any(known => known.Reference == read.Reference))
      {
        reserved.Add(read);
      }
    }

    return new LocateFindings(certain, reserved);
  }

  private static Reservation ReservationFrom(ReservedOnTheWire? reserved, DeclaredSystemId declaredSystem)
  {
    if (reserved is null)
    {
      throw Broken(declaredSystem, "a rendu une réserve vide, qui ne dit rien de ce qu'il a trouvé");
    }

    var designations = (reserved.Designations ?? [])
      .Select(designation => DesignationFrom(designation, declaredSystem))
      .ToArray();

    return Read(
      () => new Reservation(
        OpaqueReference.Of(reserved.Reference),
        reserved.Reason!,
        designations),
      declaredSystem);
  }

  private static Designation DesignationFrom(DesignationOnTheWire? designation, DeclaredSystemId declaredSystem)
  {
    var kind = DesignationKind.FromToken(designation?.Kind);

    if (kind is null)
    {
      // Le vocabulaire est fermé des deux côtés du fil. Un cinquième mot n'est pas une nature qu'on
      // devine, et la ranger « au mieux » ferait chercher la personne sous une clé inventée ici.
      throw Broken(
        declaredSystem,
        $"a proposé une désignation de nature « {designation?.Kind} », que le contrat ne connaît pas");
    }

    return Read(() => Designation.Of(kind, designation!.Value), declaredSystem);
  }

  /// <summary>
  /// Ce que le type du domaine rend, ou la <b>panne</b> que son refus révèle. Un refus ici n'est pas
  /// une programmation fautive du service : c'est l'<c>Adapter</c> qui a écrit quelque chose que le
  /// contrat ne prévoit pas.
  /// </summary>
  private static T Read<T>(Func<T> cross, DeclaredSystemId declaredSystem)
  {
    try
    {
      return cross();
    }
    catch (ArgumentException refused)
    {
      throw Broken(declaredSystem, $"a servi un corps que le contrat ne prévoit pas : {refused.Message.Split(" (Parameter")[0]}");
    }
  }

  private static AdapterFailure Broken(DeclaredSystemId declaredSystem, string what)
  {
    return new AdapterFailure($"L'Adapter de « {declaredSystem.Value} » {what}.");
  }
}

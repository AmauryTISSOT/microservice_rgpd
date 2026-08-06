namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce qu'un <c>Adapter</c> nomme pour désigner ce qu'il a rattaché à la personne — <b>dans son
/// vocabulaire, et le service ne la déréférence jamais</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est opaque de bout en bout</b>, et c'est tout son propos. Le service ne saura jamais que
/// « #1203 » est une ligne d'une table héritée, ni que « journal:2026-04 » est un fichier tourné :
/// il n'ouvre pas ces mots, il les recopie. Le grain du champ est fermé dans le <c>Manifest</c>, et
/// il l'est aussi au retour.
/// </para>
/// <para>
/// ⚠️ <b>Elle n'est pas une <see cref="Designation"/>, et les deux ne se convertissent pas.</b> Une
/// désignation <b>pointe vers</b> la personne et sert les appels suivants ; une référence opaque
/// désigne une ligne <b>chez le client</b> et n'a de sens que là-bas. Une réserve qui n'apporte
/// qu'une référence reste donc <b>locale</b>, quoi qu'un <c>Operator</c> en arbitre.
/// </para>
/// <para>
/// <b>C'est du nominatif par ricochet</b> — elle désigne les données de quelqu'un — et elle meurt
/// avec le <see cref="Case"/>. Aucune valeur de ce type n'entre au <c>Ledger</c>.
/// </para>
/// </remarks>
public sealed record OpaqueReference
{
  /// <summary>Le plafond, en unités UTF-16. Large : une clé composite du client y tient.</summary>
  public const int MaxValueLength = 200;

  private OpaqueReference(string value)
  {
    Value = value;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private OpaqueReference()
  {
    Value = string.Empty;
  }

  /// <summary>Le mot de l'application, relu tel quel, bordures nettoyées et rien d'autre.</summary>
  public string Value { get; private set; }

  /// <summary>
  /// Prend une référence, ou refuse. Le refus est une <b>programmation fautive</b> : ce qui arrive
  /// d'un <c>Adapter</c> a déjà été trié par la lecture du corps, qui écarte plutôt qu'elle ne casse.
  /// </summary>
  /// <exception cref="ArgumentException">La valeur est vide, démesurée, ou porte un caractère de contrôle.</exception>
  public static OpaqueReference Of(string? value)
  {
    return new OpaqueReference(
      DeclaredText.OrThrow(value, "La référence rendue par l'Adapter", MaxValueLength, nameof(value)));
  }
}

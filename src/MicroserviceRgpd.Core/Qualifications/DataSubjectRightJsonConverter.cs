using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// La projection d'un <see cref="DataSubjectRight"/> sur un fil JSON : son <b>nom canonique
/// anglais</b>, et rien d'autre.
/// </summary>
/// <remarks>
/// <para>
/// Le convertisseur est <b>attaché au type</b> par <see cref="JsonConverterAttribute"/> plutôt
/// qu'enregistré dans chaque jeu d'options. Le domaine traverse deux frontières — le contrat public
/// et le sidecar — et un enregistrement par frontière est un enregistrement qu'on oubliera : le
/// jour de l'oubli, l'ordinal du <c>SmartEnum</c> sortirait sur le fil sans que rien ne l'annonce.
/// </para>
/// <para>
/// Une valeur hors taxonomie est refusée, jamais rangée dans un cas par défaut. Le domaine ne
/// connaît pas de « droit inconnu » — <see cref="DataSubjectRight.OutOfScope"/> est un <b>verdict</b>,
/// pas une poubelle — et un moteur qui émettrait autre chose est en panne, pas approximatif.
/// </para>
/// </remarks>
public sealed class DataSubjectRightJsonConverter : JsonConverter<DataSubjectRight>
{
  /// <summary>
  /// Le <c>null</c> du fil est traité ici plutôt qu'escamoté par le sérialiseur : sans cela, un
  /// <c>["Erasure", null]</c> se lirait en silence comme une liste portant un trou, et le trou
  /// n'atteindrait le domaine que plus tard, méconnaissable.
  /// </summary>
  public override bool HandleNull => true;

  /// <inheritdoc />
  public override DataSubjectRight Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.String)
    {
      throw new JsonException(
        $"Un droit se lit comme une chaîne portant son nom canonique, non comme {reader.TokenType}.");
    }

    var name = reader.GetString();

    if (name is null || !DataSubjectRight.TryFromName(name, out var right))
    {
      throw new JsonException(
        $"« {name} » n'appartient pas à la taxonomie. Les sept noms canoniques sont : " +
        $"{string.Join(", ", DataSubjectRight.List.Select(known => known.Name))}.");
    }

    return right;
  }

  /// <inheritdoc />
  public override void Write(Utf8JsonWriter writer, DataSubjectRight value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);
    ArgumentNullException.ThrowIfNull(value);

    writer.WriteStringValue(value.Name);
  }
}

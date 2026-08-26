namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Un pas franchi par le <c>Scan</c>, rapporté <b>pendant</b> qu'il court. C'est ce qui permet à
/// l'écran d'attente de compter <b>réellement</b> — « table 148 sur 312 » — plutôt que d'inventer un
/// pourcentage.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun pas n'est rapporté avant que le catalogue ait répondu.</b> Avant lui, aucun
/// dénominateur n'est honnête, et un total annoncé trop tôt est très exactement le chiffre inventé
/// que <c>ScanProgress</c> refuse. Le premier pas est donc toujours
/// <see cref="CatalogueRead(int)"/>, et c'est lui qui apporte le dénominateur des suivants.
/// </para>
/// <para>
/// ⚠️ <b>Le grain du prélèvement est la table, jamais la colonne.</b> Le prélèvement coûte une
/// requête par table : compter en colonnes ferait avancer la barre par bonds de largeur variable,
/// et le dénominateur ne dirait plus le travail restant.
/// </para>
/// <para>
/// ⚠️ <b>Il ne porte aucun nom de table.</b> Un nom de table est du schéma du client ; il n'a rien à
/// faire dans un objet que l'écran d'attente rafraîchit toutes les deux secondes, et le compte seul
/// suffit à ce que l'<c>Operator</c> estime ce qu'il reste.
/// </para>
/// </remarks>
public sealed class ScanStep
{
  private ScanStep(ScanPhase phase, int done, int total)
  {
    Phase = phase;
    Done = done;
    Total = total;
  }

  /// <summary>La phase dont ce pas rend compte.</summary>
  public ScanPhase Phase { get; }

  /// <summary>Ce qui est fait, dans l'unité de la phase.</summary>
  public int Done { get; }

  /// <summary>Ce qu'il y a à faire, dans la même unité. Le dénominateur, et il est réel.</summary>
  public int Total { get; }

  /// <summary>
  /// Le catalogue a répondu : voici le nombre de tables qu'il a rendues. C'est le pas qui rend un
  /// dénominateur honnête possible, et rien ne se compte avant lui.
  /// </summary>
  public static ScanStep CatalogueRead(int tableCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(tableCount);

    return new ScanStep(ScanPhase.Cataloguing, tableCount, tableCount);
  }

  /// <summary>Une table de plus a été prélevée, sur le nombre que le catalogue a rendu.</summary>
  public static ScanStep TableSampled(int tablesSampled, int tableCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(tablesSampled);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(tablesSampled, tableCount);

    return new ScanStep(ScanPhase.Sampling, tablesSampled, tableCount);
  }
}

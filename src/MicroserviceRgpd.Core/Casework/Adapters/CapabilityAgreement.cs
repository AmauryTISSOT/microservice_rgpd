namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce que la vérification a constaté d'<b>une</b> <see cref="Capability"/> sur <b>un</b>
/// <see cref="DeclaredSystem"/> : le catalogue déclaré et l'<c>Adapter</c> disent-ils la même chose ?
/// </summary>
/// <remarks>
/// <para>
/// <b>Rien ici ne corrige quoi que ce soit.</b> Un écart constaté est rapporté, jamais réparé : le
/// <c>Manifest</c> est ce qu'un humain a affirmé, et un programme qui le réécrirait sur la réponse
/// d'un <c>Adapter</c> déciderait, à la place de cet humain, lequel des deux avait tort.
/// </para>
/// <para>
/// <b><see cref="Unverifiable"/> et <see cref="Unknown"/> ne fusionnent pas</b>, pour la même raison
/// que <c>OutOfReach</c> et <c>Untreated</c> ne fusionnent pas sur un <c>Step</c> : le premier est
/// <b>structurel et annonçable au premier jour</b> — aucune vérification non destructrice
/// n'exercera jamais <see cref="Capability.Erase"/> —, le second est un <b>constat du jour</b>, que
/// la prochaine vérification peut lever. Les confondre ferait espérer d'une nouvelle passe ce
/// qu'aucune passe ne rendra jamais.
/// </para>
/// </remarks>
public sealed class CapabilityAgreement : SmartEnum<CapabilityAgreement>
{
  /// <summary>Déclarée au catalogue, et servie par l'<c>Adapter</c>. Les deux moitiés s'accordent.</summary>
  public static readonly CapabilityAgreement Agreed =
    new(nameof(Agreed), 0, "déclarée et servie", isDisagreement: false);

  /// <summary>
  /// Déclarée au catalogue, et l'<c>Adapter</c> ne la sert pas. <b>Le <c>Manifest</c> vieillit, et
  /// il vient de mentir</b> : ce qu'un dossier compterait sur ce système n'existe pas.
  /// </summary>
  public static readonly CapabilityAgreement NotServed =
    new(nameof(NotServed), 1, "déclarée, non servie", isDisagreement: true);

  /// <summary>
  /// Servie par l'<c>Adapter</c>, et absente du catalogue. L'écart inverse, et le plus proche de
  /// l'<c>Omission silencieuse</c> : le service ne s'en servira jamais, faute de l'avoir lu.
  /// </summary>
  public static readonly CapabilityAgreement NotDeclared =
    new(nameof(NotDeclared), 2, "servie, non déclarée", isDisagreement: true);

  /// <summary>
  /// Déclarée au catalogue, et <b>hors d'atteinte de toute vérification</b> : l'exercer serait la
  /// seule façon de savoir qu'elle est servie.
  /// <para>
  /// C'est le sort de <see cref="Capability.Erase"/> et de <see cref="Capability.Rectify"/>, qu'une
  /// sonde ne touchera jamais — elle détruirait ou réécrirait des données réelles pour vérifier une
  /// ligne de catalogue. C'est aussi celui de <see cref="Capability.Read"/>, qu'on ne sonde pas non
  /// plus : sa réponse est faite de données personnelles, et les faire entrer au titre d'une
  /// vérification est exactement ce que le service refuse de faire.
  /// </para>
  /// </summary>
  public static readonly CapabilityAgreement Unverifiable =
    new(nameof(Unverifiable), 3, "déclarée, non vérifiable", isDisagreement: false);

  /// <summary>
  /// La vérification n'a rien pu conclure aujourd'hui : l'<c>Adapter</c> a refusé le secret du
  /// déploiement, ou n'a rien répondu du tout.
  /// <para>
  /// <b>Ce n'est ni un accord ni un écart</b>, et le dire serait inventer. C'est en revanche une
  /// ligne présente, vue par qui exploite, plutôt qu'une ligne manquante que nulle relecture ne
  /// lèverait.
  /// </para>
  /// </summary>
  public static readonly CapabilityAgreement Unknown =
    new(nameof(Unknown), 4, "sans conclusion", isDisagreement: false);

  private CapabilityAgreement(string name, int value, string frenchLabel, bool isDisagreement)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    IsDisagreement = isDisagreement;
  }

  /// <summary>Le libellé destiné à l'humain qui exploite. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Le catalogue et l'<c>Adapter</c> se contredisent-ils ? <b>Ni l'ignorance ni l'impossibilité n'en
  /// sont</b> : un écart est une contradiction constatée, et le compter large ferait tenir pour
  /// menteur un catalogue dont on n'a rien su vérifier.
  /// </summary>
  public bool IsDisagreement { get; }
}

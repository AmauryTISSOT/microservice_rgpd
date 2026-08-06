using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce que l'<c>Operator</c> <b>déclare</b> lorsqu'il prolonge de deux mois au titre de l'art. 12.3 :
/// un motif, et la date à laquelle il a informé la personne de la prolongation et de ses motifs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le service ne prolonge rien et n'informe personne.</b> Il <b>réclame</b> une déclaration et
/// l'enregistre, comme il le fait de l'identité et de la remise : l'information de la personne est
/// un acte humain, et l'art. 12.3 en met la charge probatoire sur celui qui prolonge. Le service la
/// lui fait tenir, il ne la tient pas à sa place.
/// </para>
/// <para>
/// <b>Le déplacement de l'échéance n'est pas ici.</b> C'est un <b>calcul</b> sur
/// <see cref="DeclaredOn"/>, fait par <see cref="StatutoryDeadline"/> à l'instant où quelqu'un
/// regarde : déclarée dans le mois, elle porte le délai à trois mois ; déclarée après, elle
/// s'inscrit quand même — le fait est gardé — mais le dénominateur ne bouge pas, sans quoi un clic
/// blanchirait un dépassement déjà acquis. Aucune propriété d'ici ne dit si elle a porté ; une telle
/// propriété serait le dénominateur persisté que ce contexte refuse partout.
/// </para>
/// <para>
/// ⚠️ <b>Elle n'est jamais barrée.</b> Une déclaration hors délai est enregistrée telle quelle : on
/// consigne un fait laid plutôt qu'on ne fabrique un faux, et le dépassement acquis se lit à côté.
/// </para>
/// </remarks>
public sealed record ExtensionDeclaration
{
  private ExtensionDeclaration(string motive, DateTimeOffset informedOn, DateTimeOffset declaredOn)
  {
    Motive = motive;
    InformedOn = informedOn;
    DeclaredOn = declaredOn;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private ExtensionDeclaration()
  {
  }

  /// <summary>
  /// Pourquoi le délai est prolongé, dans les mots de l'humain qui prolonge. <b>Prose de preuve</b> :
  /// elle descend au <c>Ledger</c> et y survit au dossier de cinq ans — rien de ce qui nomme la
  /// personne ou un tiers n'a à y entrer.
  /// </summary>
  public string Motive { get; private set; } = null!;

  /// <summary>
  /// Le jour où l'<c>Operator</c> déclare avoir informé la personne de la prolongation <b>et de ses
  /// motifs</b>, comme l'art. 12.3 l'exige de lui.
  /// </summary>
  /// <remarks>
  /// <b>C'est une déclaration, jamais un constat du service.</b> Aucun courriel n'est parti d'ici :
  /// les trois seules communications dues à la personne — la prolongation, le refus, la remise —
  /// sont des actes humains déclarés au service, jamais émis par lui.
  /// </remarks>
  public DateTimeOffset InformedOn { get; private set; }

  /// <summary>
  /// L'instant où la déclaration a été faite au service. <b>C'est de lui, et de lui seul, que se
  /// calcule le déplacement de l'échéance</b> — jamais de la date d'information, qui prouve autre
  /// chose.
  /// </summary>
  public DateTimeOffset DeclaredOn { get; private set; }

  /// <summary>
  /// La déclaration, ou le refus de ce qui ne peut pas en être une.
  /// </summary>
  /// <param name="motive">Pourquoi le délai est prolongé. <b>Exigé</b> : l'art. 12.3 le met à la charge de qui prolonge.</param>
  /// <param name="informedOn">Le jour où l'<c>Operator</c> déclare avoir informé la personne.</param>
  /// <param name="declaredOn">L'instant de la déclaration au service.</param>
  /// <exception cref="ArgumentException">
  /// Le motif manque, est démesuré ou porte un caractère de contrôle ; ou la personne aurait été
  /// informée après la déclaration.
  /// </exception>
  public static ExtensionDeclaration Of(string? motive, DateTimeOffset informedOn, DateTimeOffset declaredOn)
  {
    // Le plafond est celui de la colonne qui la recevra : ce motif est de la prose de preuve, et il
    // n'existe qu'un seul plafond pour elle dans tout le dispositif.
    var declared = DeclaredText.OrThrow(motive, "Le motif de la prolongation", LedgerEntry.MaxEvidenceProseLength, nameof(motive));

    var informed = informedOn.ToUniversalTime();
    var made = declaredOn.ToUniversalTime();

    if (informed > made)
    {
      throw new ArgumentException(
        "La date à laquelle vous avez informé la personne est postérieure à cette déclaration : "
        + "elle dirait que vous l'informerez demain, et c'est la seule preuve que l'art. 12.3 "
        + "réclame de vous.",
        nameof(informedOn));
    }

    return new ExtensionDeclaration(declared, informed, made);
  }
}

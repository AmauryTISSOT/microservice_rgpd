using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Les <b>six</b> règles qui lisent les valeurs d'un <see cref="ColumnPreview"/> — et les
/// <b>quatre</b> qui ont été écartées nommément, écrites juste à côté pour qu'on ne les rajoute pas
/// dans six mois en croyant réparer un oubli.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce sont des formes écrites d'avance, jamais des mots.</b> Aucun lexique ne s'applique aux
/// valeurs. C'est ce qui tient le corollaire « ce n'est donc pas un NER » : <c>+33612345678</c> est
/// un <b>motif</b>, reconnaissable par sa morphologie, sans modèle et sans corpus d'entraînement ;
/// « Madame Dupont a téléphoné » est une entité nommée dans une phrase, et ce contexte n'y touche
/// pas.
/// </para>
/// <para>
/// ⚠️ <b>Une règle de forme se juge à ce qu'elle rapporte, et certaines rapportent négativement.</b>
/// Un format sans clé de contrôle plafonne au taux de faux positifs de sa famille de colonnes.
/// <b>Quatre sont écartées, avec leurs taux</b> :
/// <list type="bullet">
/// <item>
/// le <b>code postal</b> — 81 % de faux positifs : une base réelle est pleine de codes produits, de
/// codes INSEE de communes et d'années × 100, tous à cinq chiffres ;
/// </item>
/// <item>
/// la <b>date seule</b> — 95 à 98 % : une colonne date sur vingt à cinquante est une date de
/// naissance ;
/// </item>
/// <item>les <b>coordonnées GPS seules</b> — 50 à 70 % ;</item>
/// <item>le couple <b>CNI / NEPH</b> — indiscernables l'un de l'autre.</item>
/// </list>
/// Le prix d'une mauvaise règle n'est pas une ligne fausse de plus : c'est un <c>Operator</c> qui se
/// met à survoler, et l'<c>Omission silencieuse</c> rétablie par épuisement, sans qu'aucune ligne de
/// doctrine n'ait bougé.
/// </para>
/// <para>
/// ⚠️ <b>Aucune règle de forme ne mène à <see cref="PersonalDataCategory.FreeTextAboutPerson"/>.</b>
/// Une règle qui lit les valeurs sait d'avance quelle catégorie elle vise — une clé d'IBAN vise
/// <see cref="PersonalDataCategory.FinancialData"/>, et rien d'autre.
/// </para>
/// </remarks>
internal static partial class ValueFormRules
{
  /// <summary>
  /// Les six règles, <b>dans l'ordre où elles s'énoncent</b>. L'ordre ne tranche rien : c'est celui
  /// du lexique, <see cref="LexiconTaxonomy"/>, qui départage.
  /// </summary>
  internal static IReadOnlyList<ValueFormRule> All { get; } =
  [
    new(
      PersonalDataCategory.FinancialData,
      RuleStrength.CheckedValueForm,
      "portent une clé de contrôle d'IBAN",
      IsIban),
    new(
      PersonalDataCategory.NationalIdentifier,
      RuleStrength.CheckedValueForm,
      "portent une clé de contrôle de numéro de sécurité sociale",
      IsSocialSecurityNumber),
    new(
      // ⚠️ Le SIRET va en ProfessionalLife : c'est la seule des options qui dise vrai pour une SARL
      // comme pour un auto-entrepreneur, dont le SIREN EST son identifiant de personne physique.
      PersonalDataCategory.ProfessionalLife,
      RuleStrength.CheckedValueForm,
      "portent une clé de contrôle de SIREN ou de SIRET",
      IsSirenOrSiret),
    new(
      PersonalDataCategory.ContactDetails,
      RuleStrength.ValueForm,
      "ont la forme d'une adresse de courriel",
      IsEmailAddress),
    new(
      PersonalDataCategory.ContactDetails,
      RuleStrength.ValueForm,
      "ont la forme d'un numéro de téléphone français",
      IsFrenchTelephoneNumber),
    new(
      PersonalDataCategory.OnlineIdentifier,
      RuleStrength.ValueForm,
      "ont la forme d'une adresse IP",
      IsIpAddress),
  ];

  /// <summary>
  /// Un IBAN : deux lettres de pays, deux chiffres de contrôle, puis le compte — et le
  /// <b>mod 97</b> qui vaut 1.
  /// </summary>
  private static bool IsIban(string value)
  {
    var normalised = Unspaced(value).ToUpperInvariant();

    return IbanShape().IsMatch(normalised) && Modulo97(normalised[4..] + normalised[..4]) == 1;
  }

  /// <summary>
  /// Un NIR ou un NIA : treize caractères de numéro et deux de clé, la clé valant
  /// <c>97 − (numéro mod 97)</c>. La Corse a ses deux départements en lettres, <c>2A</c> et
  /// <c>2B</c>, qui se lisent 19 et 18 <b>moins un million</b> — c'est la règle officielle, et non
  /// un ajustement.
  /// </summary>
  private static bool IsSocialSecurityNumber(string value)
  {
    var normalised = Unspaced(value).ToUpperInvariant();

    if (!SocialSecurityShape().IsMatch(normalised))
    {
      return false;
    }

    var body = normalised[..13].Replace("2A", "19", StringComparison.Ordinal)
      .Replace("2B", "18", StringComparison.Ordinal);

    if (!long.TryParse(body, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
    {
      return false;
    }

    if (normalised[5] == '2' && normalised[6] is 'A' or 'B')
    {
      number -= 1_000_000;
    }

    return 97 - (number % 97) == int.Parse(normalised[13..], CultureInfo.InvariantCulture);
  }

  /// <summary>
  /// Un SIREN — neuf chiffres — ou un SIRET — quatorze —, tous deux vérifiés par <b>Luhn</b>, et le
  /// SIRET portant en plus un SIREN valide dans ses neuf premiers chiffres.
  /// <para>
  /// ⚠️ <b>Un numéro dont tous les chiffres sont égaux ne compte pas</b>, <c>000000000</c> le
  /// premier : Luhn le déclare valide, et une colonne d'entiers non renseignés se signalerait en
  /// <c>ProfessionalLife</c> pour un remplissage par défaut.
  /// </para>
  /// </summary>
  private static bool IsSirenOrSiret(string value)
  {
    var normalised = Unspaced(value);

    if (!SirenOrSiretShape().IsMatch(normalised) || normalised.All(digit => digit == normalised[0]))
    {
      return false;
    }

    return normalised.Length == 9
      ? PassesLuhn(normalised)
      : PassesLuhn(normalised) && PassesLuhn(normalised[..9]);
  }

  /// <summary>
  /// Une adresse de courriel — <b>sans</b> clé de contrôle, d'où le seuil de <i>toutes</i> les
  /// valeurs comptées. La forme est délibérément large : c'est l'<c>Operator</c> qui juge, l'aperçu
  /// sous les yeux.
  /// </summary>
  private static bool IsEmailAddress(string value)
  {
    var normalised = value.Trim();

    return normalised.Length <= ColumnPreview.MaxValueLength && EmailShape().IsMatch(normalised);
  }

  /// <summary>Un numéro de téléphone français, en indicatif international ou en zéro de tête.</summary>
  private static bool IsFrenchTelephoneNumber(string value)
  {
    return FrenchTelephoneShape().IsMatch(Punctuationless(value));
  }

  /// <summary>
  /// Une adresse IP, v4 ou v6. La v4 est lue à la main plutôt que par <see cref="IPAddress"/> :
  /// quatre groupes décimaux, sans zéro de tête, chacun sous 256.
  /// </summary>
  private static bool IsIpAddress(string value)
  {
    var normalised = value.Trim();

    if (Ipv4Shape().IsMatch(normalised))
    {
      return normalised.Split('.').All(part => byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }

    return normalised.Contains(':', StringComparison.Ordinal)
      && IPAddress.TryParse(normalised, out var parsed)
      && parsed.AddressFamily == AddressFamily.InterNetworkV6;
  }

  /// <summary>
  /// Ôte les <b>espaces</b>, et rien d'autre : c'est ainsi qu'un humain écrit un IBAN, un NIR ou un
  /// SIREN — « 552 100 554 ».
  /// <para>
  /// ⚠️ <b>Le point n'en est pas un, et l'ôter fabriquerait des faux positifs mesurables.</b> Une
  /// adresse IPv4 privée de ses points fait <b>neuf chiffres</b>, très exactement la longueur d'un
  /// SIREN, et une sur dix passerait Luhn : une colonne d'adresses IP se signalerait alors en
  /// <see cref="PersonalDataCategory.ProfessionalLife"/>, qui l'emporterait à l'arbitrage. Un SIREN
  /// ne s'écrit pas avec des points ; la règle n'a donc pas à les lire.
  /// </para>
  /// </summary>
  private static string Unspaced(string value)
  {
    return Spaces().Replace(value, string.Empty);
  }

  /// <summary>
  /// Ôte tout ce qu'un humain intercale pour lire un <b>numéro de téléphone</b> — espaces, points,
  /// tirets —, parce que c'est là, et seulement là, qu'il en met.
  /// </summary>
  private static string Punctuationless(string value)
  {
    return Separators().Replace(value, string.Empty);
  }

  /// <summary>Le reste de la division par 97 d'un nombre trop long pour tenir dans un entier, chiffre à chiffre.</summary>
  private static int Modulo97(string alphanumeric)
  {
    var remainder = 0;

    foreach (var character in alphanumeric)
    {
      var digits = char.IsAsciiDigit(character)
        ? character - '0'
        : character - 'A' + 10;

      remainder = digits > 9
        ? ((remainder * 100) + digits) % 97
        : ((remainder * 10) + digits) % 97;
    }

    return remainder;
  }

  /// <summary>La clé de Luhn, lue de droite à gauche en doublant un chiffre sur deux.</summary>
  private static bool PassesLuhn(string digits)
  {
    var sum = 0;

    for (var offset = 0; offset < digits.Length; offset++)
    {
      var digit = digits[digits.Length - 1 - offset] - '0';

      if (offset % 2 == 1)
      {
        digit *= 2;

        if (digit > 9)
        {
          digit -= 9;
        }
      }

      sum += digit;
    }

    return sum % 10 == 0;
  }

  [GeneratedRegex(@"\s+")]
  private static partial Regex Spaces();

  [GeneratedRegex(@"[\s.-]+")]
  private static partial Regex Separators();

  [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
  private static partial Regex IbanShape();

  [GeneratedRegex(@"^[1-8][0-9]{2}(?:0[1-9]|1[0-2]|[2-9][0-9])(?:[0-9]{2}|2[AB])[0-9]{6}[0-9]{2}$")]
  private static partial Regex SocialSecurityShape();

  [GeneratedRegex("^(?:[0-9]{9}|[0-9]{14})$")]
  private static partial Regex SirenOrSiretShape();

  [GeneratedRegex(@"^[^@\s,;:<>""]+@[^@\s,;:<>""]+\.[A-Za-z]{2,}$")]
  private static partial Regex EmailShape();

  [GeneratedRegex(@"^(?:\+33|0033|0)[1-9][0-9]{8}$")]
  private static partial Regex FrenchTelephoneShape();

  [GeneratedRegex("^(?:0|[1-9][0-9]{0,2})(?:\\.(?:0|[1-9][0-9]{0,2})){3}$")]
  private static partial Regex Ipv4Shape();
}

/// <summary>
/// <b>Une</b> règle de forme : ce qu'elle reconnaît dans une valeur lue, ce qu'elle en conclut, et
/// la phrase qu'elle écrit dans le motif.
/// </summary>
/// <remarks>
/// ⚠️ <b>La phrase ne porte jamais de chiffre, ni une valeur lue.</b> Le motif est <b>enregistré</b>
/// et vit aussi longtemps que le <c>Screening</c> ; une valeur recopiée dedans survivrait à l'aperçu
/// qui l'a montrée. Et « cinq sur cinq » se comparerait d'une ligne à l'autre alors que les deux
/// nombres ne veulent pas dire la même chose.
/// </remarks>
/// <param name="Category">Ce que cette forme désigne, sans hésitation possible.</param>
/// <param name="Strength">Le degré que cette règle porte — et donc le seuil qu'elle doit atteindre.</param>
/// <param name="FormPhrase">
/// Ce que la règle a vu, au pluriel et sans quantificateur : « portent une clé de contrôle d'IBAN ».
/// Le quantificateur — « toutes les valeurs lues », « certaines des valeurs lues » — est ajouté par
/// le moteur, qui seul sait combien de valeurs comptées l'ont portée.
/// </param>
/// <param name="Recognises">La forme, écrite d'avance, appliquée à <b>une</b> valeur comptée.</param>
internal sealed record ValueFormRule(
  PersonalDataCategory Category,
  RuleStrength Strength,
  string FormPhrase,
  Func<string, bool> Recognises);

// LE TABLEAU DES DEMANDES RGPD : la recherche filtre les lignes à chaque frappe, le tri les
// réordonne par date de réception ; le bouton « Créer une demande » ouvre la modale que le serveur a
// rendue, formulaire remis à zéro ; « Créer » juge la saisie avec les règles du service, puis
// l'envoie au handler de la page ; et quatre modes de fermeture la referment — « Annuler », la
// croix, Échap, un clic sur le fond.
//
// ⚠️ L'OPERATOR NE PERD JAMAIS UNE SAISIE PAR MÉGARDE. Les quatre modes passent tous par
// `requestClose` : un formulaire non modifié s'y ferme directement, un formulaire modifié y ouvre la
// confirmation d'abandon. Aucun mode de fermeture ne doit pouvoir la contourner.
//
// La demande créée, le module insère dans le tableau la ligne que le serveur a rendue pour elle, à sa
// place selon le tri sélectionné, et visible seulement si la recherche en cours la retient.
//
// La poubelle de chaque ligne ouvre la confirmation de suppression, avec la phrase de sa ligne ;
// « Supprimer définitivement » l'envoie au handler de la page, et la ligne s'en va sans rechargement —
// sauf échec, que le toast dit.
//
// Le crayon d'une ligne ouvre la MÊME modale, pré-remplie des valeurs que le serveur rend pour sa
// demande : une seule modale sert les deux gestes, et c'est `data-mode` qui dit lequel est en cours.
// Les mots et les routes des deux modes viennent du serveur (Board.cshtml) ; le module n'en écrit
// aucun. À la fermeture, le focus revient au bouton qui a ouvert la modale.
//
// La correction enregistrée, l'ancienne ligne s'en va et la nouvelle passe par la MÊME insertion que
// la création : elle reprend sa place selon le tri en cours, et la recherche se rejoue sur elle. Le
// replacement, la disparition hors recherche, la nouvelle date limite et son signalement n'ont donc
// aucun code à eux.
//
// ⚠️ UN ENREGISTREMENT QUI ÉCHOUE NE PERD JAMAIS LA SAISIE : le bandeau de la modale dit l'échec —
// technique, ou une demande close pendant la correction —, et la saisie reste là, à réessayer. Seule
// une demande qui n'existe plus fait exception : la modale se ferme, sa ligne part, et le toast le
// dit — il n'y a rien à réessayer.
//
// L'œil d'une ligne ouvre la fiche de sa demande : la lecture à l'écran de ce que le service en
// tient, sans aller-retour réseau — la page a déjà, sur la ligne, tout ce que la fiche montrera. Ses
// quatre fermetures — Échap, la croix, le fond, « Fermer » — ferment directement : rien n'est en jeu
// dans une lecture. ⚠️ La fiche s'ouvre encore VIDE : c'est l'US suivante qui y versera les valeurs.

// LA RECHERCHE. Elle filtre les lignes que le serveur a rendues, sans revenir à lui : une demande
// reste affichée si son email, son nom ou son prénom — ceux que la ligne porte en `data-*`, tels
// qu'enregistrés — contient le texte saisi. Les états vides sont rendus par le serveur ; le module
// ne fait que les montrer ou les cacher, et n'en écrit aucun mot.
const search = document.getElementById("requests-search");
const clearButton = document.getElementById("requests-search-clear");
const tableBody = document.getElementById("requests").tBodies[0];
const rows = tableBody.rows;
const noMatch = document.getElementById("requests-no-match");

// LA SAISIE ET LES VALEURS SE COMPARENT NORMALISÉES : rognées, en minuscules, et sans leurs accents
// — décomposées, puis privées de leurs marques combinantes. « helene » trouve « Hélène », et
// « Hélène » trouve « Helene ». ⚠️ Pas `\p{Diacritic}` : il retirerait aussi des caractères qu'un
// email peut porter tels quels, comme `^` ou `` ` ``.
function normalized(text) {
  return text.trim().normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();
}

function matches(row, sought) {
  const { email, lastName, firstName } = row.dataset;

  return [email, lastName, firstName].some((value) => normalized(value ?? "").includes(sought));
}

// ⚠️ « AUCUNE DEMANDE NE CORRESPOND » NE SE MONTRE QUE SI LA RECHERCHE A ÉCARTÉ DES LIGNES : sur un
// tableau vide, « Aucune demande pour le moment » dit déjà qu'il n'y a rien à trouver.
function applySearch() {
  const sought = normalized(search.value);
  let shown = 0;

  for (const row of rows) {
    row.hidden = !matches(row, sought);
    shown += row.hidden ? 0 : 1;
  }

  noMatch.hidden = rows.length === 0 || shown > 0;
}

// Le ✕ vide la recherche, et rend le focus au champ : une nouvelle recherche commence aussitôt.
function emptySearch() {
  search.value = "";
  applySearch();
  search.focus();
}

search.addEventListener("input", applySearch);
clearButton.addEventListener("click", emptySearch);

// LE TRI. Il réordonne les lignes que le serveur a rendues, sans revenir à lui, sur les clés que
// chacune porte en `data-*` : la date de réception, puis l'instant d'enregistrement, qui départage
// deux demandes reçues le même jour — dans le sens choisi, l'un comme l'autre. Les deux s'écrivent
// en ISO, à largeur fixe : comparés comme des textes, ils se rangent comme des dates.
//
// ⚠️ LE TRI NE TOUCHE PAS À CE QUE LA RECHERCHE CACHE : il déplace les lignes, toutes, sans en
// montrer ni en cacher aucune. Les résultats de la recherche en cours se lisent donc dans l'ordre
// choisi, et une recherche vidée retrouve chaque ligne à sa place.
//
// Rien ne se trie au chargement : « la plus récente » est sélectionnée, et c'est l'ordre du serveur.
const sort = document.getElementById("requests-sort");

// Deux textes dans l'ordre croissant : négatif, nul ou positif, comme le veut `sort`.
function ascending(a, b) {
  return a < b ? -1 : a > b ? 1 : 0;
}

function byReceptionThenCreation(first, second) {
  return (
    ascending(first.dataset.receivedOn, second.dataset.receivedOn) ||
    ascending(first.dataset.createdAt, second.dataset.createdAt)
  );
}

// Deux lignes dans l'ordre du tri sélectionné : négatif si la première y précède la seconde.
// Les valeurs des options sont celles que le serveur a rendues (Board.cshtml).
function inTheSelectedOrder(first, second) {
  const direction = sort.value === "oldest" ? 1 : -1;

  return direction * byReceptionThenCreation(first, second);
}

function applySort() {
  tableBody.append(...[...rows].sort(inTheSelectedOrder));
}

sort.addEventListener("change", applySort);

const dialog = document.getElementById("request-dialog");
const dialogTitle = document.getElementById("request-dialog-title");
const confirmation = document.getElementById("abandon-entry");
const opener = document.getElementById("create-request-open");
const form = document.getElementById("request-form");
const receivedOn = form.elements.namedItem("receivedOn");
const identifier = form.elements.namedItem("id");
const submitButton = form.querySelector("[data-submit]");
const failure = document.getElementById("request-failure");
const toast = document.getElementById("requests-toast");

// LES DEUX GESTES QUE LA MODALE SERT, avec les mots et la route de chacun : ceux que le serveur a
// rendus en `data-*` sur la modale et sur le toast. Le module bascule d'un mode à l'autre à
// l'ouverture ; il n'écrit aucun libellé et ne connaît aucune route.
//
// `saved` est le code du succès de ce geste-là — 201 pour une création, 200 pour une modification —,
// `said` le mot du toast quand il a eu lieu, et `place` ce que sa ligne devient dans le tableau :
// une création s'insère, une modification remplace. `worthSending` dit enfin si la saisie vaut d'être
// envoyée : toujours pour une création, seulement si elle corrige quelque chose pour une
// modification. Tout le reste de l'envoi leur est commun.
//
// `vanished` est ce que le geste fait d'un 404 — la demande n'existe plus. Une correction en désigne
// une, et la sienne peut avoir été supprimée depuis un autre onglet ; une création n'en désigne
// aucune, et un 404 n'y est qu'un échec de plus, avec son bandeau.
//
// ⚠️ TOUT CE QUI DÉPEND DU GESTE EST ICI, et nulle part ailleurs : un troisième geste s'ajouterait à
// cette table, sans qu'aucun `if` sur le mode soit à retrouver dans le module.
const modes = {
  create: {
    title: dialog.dataset.createTitle,
    submit: dialog.dataset.createSubmit,
    action: dialog.dataset.createAction,
    saved: 201,
    said: toast.dataset.created,
    place: insertRow,
    worthSending: () => true,
    vanished: null,
  },
  modify: {
    title: dialog.dataset.modifyTitle,
    submit: dialog.dataset.modifySubmit,
    action: dialog.dataset.modifyAction,
    saved: 200,
    said: toast.dataset.modified,
    place: replaceTheModifiedRow,
    worthSending: isCorrected,
    vanished: dropTheVanishedRequest,
  },
};

// Le geste en cours, celui que la dernière ouverture a posé.
function currentMode() {
  return modes[dialog.dataset.mode];
}

// Le mode devient celui de la modale : le titre, le libellé du bouton primaire et l'action du
// formulaire sont ceux du geste en cours, et `data-mode` dit lequel c'est.
function applyMode(mode) {
  dialog.dataset.mode = mode;
  dialogTitle.textContent = modes[mode].title;
  submitButton.textContent = modes[mode].submit;
  form.action = modes[mode].action;
}

// Les dix messages, écrits par le serveur (DataSubjectRequestMessages) : le module n'en écrit aucun.
const messages = JSON.parse(document.getElementById("request-messages").textContent);

// « AUJOURD'HUI » S'ENTEND À PARIS, comme côté serveur (ParisCalendar) : ni en UTC, ni au fuseau du
// poste de l'Operator. Le format `en-CA` n'est qu'un moyen d'obtenir les trois parties en chiffres ;
// elles sont relues une à une, sans dépendre de l'ordre dans lequel une locale les écrit.
const parisCalendar = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Europe/Paris",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

function todayInParis() {
  const parts = Object.fromEntries(
    parisCalendar.formatToParts(new Date()).map(({ type, value }) => [type, value]),
  );

  return `${parts.year}-${parts.month}-${parts.day}`;
}

// « MODIFIÉ » SE LIT SUR LES VALEURS BRUTES, comparées à celles que la dernière ouverture a posées :
// saisir puis effacer ne modifie rien, et des espaces seuls modifient — aucune saisie ne disparaît
// sans que l'Operator l'ait confirmé, même celle que le service tiendrait pour vide. Rien n'est donc
// rogné ici. Une date mal formée se lit vide, ce qui diffère encore d'« aujourd'hui ».
let valuesAtOpening = "";

function rawValues() {
  return JSON.stringify([...new FormData(form)]);
}

function isModified() {
  return rawValues() !== valuesAtOpening;
}

// ⚠️ DEUX COMPARAISONS VOISINES, ET DÉLIBÉRÉMENT DIFFÉRENTES. `isModified`, au-dessus, porte sur les
// valeurs BRUTES, et décide de la confirmation d'abandon : aucune frappe ne doit disparaître sans que
// l'Operator l'ait confirmé, fût-elle de deux espaces. `isCorrected`, ici, porte sur les valeurs
// ROGNÉES, et décide de l'envoi d'une correction : le domaine rogne avant d'enregistrer, et deux
// espaces ajoutés en fin de nom ne changeraient donc rien pour lui. Une saisie peut ainsi être
// modifiée sans être corrigée — jamais l'inverse.
//
// ⚠️ C'EST LE ROGNAGE DE .NET, celui du domaine (voir `dotnetWhiteSpace`), et non celui du `trim` de
// JavaScript : sans lui, deux espaces d'une sorte que l'un rogne et l'autre garde feraient partir
// l'envoi, ne laisseraient rien au serveur, et le toast dirait « Demande modifiée » pour rien.
//
// ⚠️ CÔTÉ SCRIPT, C'EST UN CONFORT — s'épargner un aller-retour et un toast mensonger. La règle, elle,
// est au domaine, qui ne trace rien non plus d'une correction qui ne corrige rien.
let trimmedValuesAtOpening = "";

function trimmedValues() {
  return JSON.stringify([...new FormData(form)].map(([name, value]) => [name, withoutBorderingSpaces(value)]));
}

function isCorrected() {
  return trimmedValues() !== trimmedValuesAtOpening;
}

// LE FORMULAIRE REPART DE ZÉRO À CHAQUE OUVERTURE : l'Operator n'hérite jamais d'une saisie
// précédente. Les valeurs par défaut sont celles que le serveur a rendues, sauf « aujourd'hui », qui
// est recalculé ici — une page restée ouverte au-delà de minuit ne doit ni proposer la veille, ni
// interdire le jour même. Il devient la valeur par défaut du champ, et non seulement sa valeur.
//
// ⚠️ LA REMISE À ZÉRO SE FAIT À L'OUVERTURE, PAS À LA FERMETURE : une modale fermée par erreur doit
// pouvoir être rouverte sur sa saisie — c'est ce que fait `reopenAndConfirm` — et n'a donc pas déjà
// été vidée. L'identifiant caché s'efface ici avec le reste : une création n'en a pas.
function resetToDefaults() {
  form.reset();

  const today = todayInParis();
  receivedOn.max = today;
  receivedOn.defaultValue = today;
  receivedOn.value = today;

  identifier.value = "";

  forgetRefusals();
  failure.hidden = true;
  proposalNote.hidden = true;
}

// LES RÈGLES SONT CELLES DU SERVICE (DataSubjectRequest.Receive), recopiées une à une pour que le
// navigateur refuse exactement ce que le serveur refuserait. C'est un confort : le serveur fait foi.

// ⚠️ LES ESPACES SONT CEUX DE .NET (`char.IsWhiteSpace`, qui fonde `Trim` et `IsNullOrWhiteSpace`),
// et non ceux du `trim` de JavaScript : l'un retire le U+0085 que l'autre garde, et garde le U+FEFF
// que l'autre retire.
const dotnetWhiteSpace = "[\\t\\n\\v\\f\\r \\u0085\\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000]";
const bordersOfWhiteSpace = new RegExp(`^${dotnetWhiteSpace}+|${dotnetWhiteSpace}+$`, "g");

function withoutBorderingSpaces(value) {
  return value.replace(bordersOfWhiteSpace, "");
}

function trimmed(field) {
  return withoutBorderingSpaces(field.value);
}

// La regex WHATWG de `<input type=email>`, la même que celle d'EmailAddress côté serveur.
const whatwgEmail =
  /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$/;

// Les plafonds, en unités UTF-16 : `.length` compte comme le `.Length` de C#.
const nameMaxLength = 100;
const emailMaxLength = 254;
const messageMaxLength = 10_000;

// Le format ISO que le serveur lit. Un sélecteur de date accepte des années à plus de quatre
// chiffres, que le serveur tiendrait pour mal formées : le navigateur aussi.
const isoDate = /^\d{4}-\d{2}-\d{2}$/;

const lastName = form.elements.namedItem("lastName");
const firstName = form.elements.namedItem("firstName");
const email = form.elements.namedItem("email");
const message = form.elements.namedItem("message");
const right = form.elements.namedItem("right");

// Les champs qu'une règle juge, dans l'ordre du formulaire : c'est le premier en erreur qui prend le focus.
const validatedFields = [receivedOn, lastName, firstName, email, message, right];

// Toutes les raisons de refuser la saisie, au plus une par champ, sous le nom du champ.
function refusalsOfTheEntry() {
  const refusals = {};

  // ⚠️ UNE DATE COMMENCÉE MAIS INCOMPLÈTE A UNE VALEUR VIDE, comme une date absente : seul
  // `validity.badInput` les distingue, et l'Operator doit lire laquelle des deux il a sous les yeux.
  if (receivedOn.validity.badInput) {
    refusals.receivedOn = messages.receivedOnMalformed;
  } else if (receivedOn.value === "") {
    refusals.receivedOn = messages.receivedOnMissing;
  } else if (!isoDate.test(receivedOn.value)) {
    refusals.receivedOn = messages.receivedOnMalformed;
  } else if (receivedOn.value > todayInParis()) {
    refusals.receivedOn = messages.receivedOnInTheFuture;
  }

  const givenLastName = trimmed(lastName);
  if (givenLastName.length > nameMaxLength) {
    refusals.lastName = messages.lastNameTooLong;
  }

  const givenFirstName = trimmed(firstName);
  if (givenFirstName.length > nameMaxLength) {
    refusals.firstName = messages.firstNameTooLong;
  }

  const givenEmail = trimmed(email);
  if (givenEmail !== "" && (givenEmail.length > emailMaxLength || !whatwgEmail.test(givenEmail))) {
    refusals.email = messages.emailInvalid;
  }

  // UN EMAIL, OU UN NOM ET UN PRÉNOM. À défaut, le refus va sous l'email et sous chacun des champs
  // nom et prénom qui manquent. Un email renseigné identifie, même mal formé : sa forme est refusée
  // plus haut, et un nom ou un prénom partiel ne bloque plus.
  if (givenEmail === "" && (givenLastName === "" || givenFirstName === "")) {
    refusals.email = messages.identificationMissing;

    if (givenLastName === "") {
      refusals.lastName = messages.identificationMissing;
    }

    if (givenFirstName === "") {
      refusals.firstName = messages.identificationMissing;
    }
  }

  const givenMessage = trimmed(message);
  if (givenMessage === "") {
    refusals.message = messages.messageMissing;
  } else if (givenMessage.length > messageMaxLength) {
    refusals.message = messages.messageTooLong;
  }

  if (right.value === "") {
    refusals.right = messages.rightMissing;
  }

  return refusals;
}

// Le refus s'écrit dans la place que le serveur a rendue sous le champ, celle que désigne son
// `aria-describedby` ; l'absence de refus la vide.
function showRefusal(field, refusal) {
  document.getElementById(field.getAttribute("aria-describedby")).textContent = refusal ?? "";

  if (refusal) {
    field.setAttribute("aria-invalid", "true");
  } else {
    field.removeAttribute("aria-invalid");
  }
}

// LES CHAMPS QUI AFFICHENT UN REFUS. Vide jusqu'au premier clic sur « Créer » : rien ne s'affiche
// pendant la saisie. Ensuite, seuls ceux-là se revalident en direct, et chacun en sort dès qu'il est
// corrigé ; un champ valide au clic, rendu fautif ensuite, ne s'en plaint qu'au clic suivant.
let inError = new Set();

// ⚠️ LES REFUS DU SERVEUR, CHACUN AVEC LA VALEUR QU'IL A REFUSÉE. Les règles du navigateur avaient
// laissé passer la saisie : les rejouer lèverait ces refus à la première frappe, où qu'elle tombe.
// Un refus du serveur tient donc tant que son champ garde la valeur envoyée, et cède dès qu'elle
// change — le navigateur ne sait pas mieux dire qu'il est corrigé.
let refusedByTheServer = new Map();

function refusalOf(field, refusals) {
  const refused = refusedByTheServer.get(field);

  if (refused && field.value === refused.value) {
    return refused.refusal;
  }

  return refusals[field.name];
}

function forgetRefusals() {
  for (const field of validatedFields) {
    showRefusal(field, undefined);
  }

  inError = new Set();
  refusedByTheServer = new Map();
}

// Chaque refus sous son champ, le focus au premier : que les refus viennent du navigateur ou du
// serveur, l'Operator les lit au même endroit, de la même façon.
function showRefusals(refusals) {
  for (const field of validatedFields) {
    showRefusal(field, refusals[field.name]);
  }

  inError = new Set(validatedFields.filter((field) => refusals[field.name]));
  validatedFields.find((field) => inError.has(field))?.focus();
}

// LE BOUTON PRIMAIRE RESTE CLIQUABLE HORS ENVOI, dans les deux modes : chaque clic juge toute la
// saisie, affiche chaque refus sous son champ et donne le focus au premier — les mêmes règles et les
// mêmes mots pour une correction que pour une création. Une saisie sans refus part au serveur.
//
// ⚠️ UNE CORRECTION QUI NE CORRIGE RIEN NE PART PAS : la modale se ferme, sans requête ni toast. Le
// test vient d'abord — une saisie inchangée est celle qui a été enregistrée, elle n'a rien à se voir
// reprocher —, et c'est le geste en cours qui le porte : voir `worthSending`.
function attemptSave() {
  if (!currentMode().worthSending()) {
    dialog.close();
    return;
  }

  refusedByTheServer = new Map();
  showRefusals(refusalsOfTheEntry());

  if (inError.size === 0) {
    send();
  }
}

// L'ENVOI, LE MÊME POUR LES DEUX GESTES. Le formulaire part tel quel au handler qu'il déclare, jeton
// anti-rejeu compris — la date au format ISO du champ, le droit sous son nom canonique, et
// l'identifiant caché quand c'est une correction : ce que le handler lit.
//
// ⚠️ LE BOUTON PRIMAIRE EST DÉSACTIVÉ PENDANT L'ENVOI : un double clic n'enregistre jamais deux fois.
// Il redevient cliquable quelle que soit l'issue. La modale, elle, ne se ferme pas tant que l'envoi
// court : la réponse doit trouver la saisie qu'elle concerne, pas une modale rouverte à zéro.
//
// Quatre issues. Le code de succès du geste — 201 à la création, 200 à la modification — : c'est
// enregistré, et le corps porte la ligne. 404 sur une correction : la demande n'existe plus, et le
// geste dit ce qu'il en fait (voir `vanished`). 400 portant des refus de la saisie : ils vont sous
// leurs champs, comme ceux du navigateur. Tout le reste — un 400 sans refus, qui est un jeton
// anti-rejeu refusé, un 409, une erreur du serveur, une coupure réseau — : le bandeau, et la saisie
// reste là.
//
// ⚠️ UN 409 NE REDESSINE PAS LA LIGNE : la demande a été close pendant la correction, mais le code
// ne dit pas lequel des deux statuts elle a pris — la cellule Statut afficherait « En cours » à côté
// d'un crayon éteint. L'incohérence se résout au prochain chargement.
//
// ⚠️ IL PORTE EN REVANCHE SA PROPRE PHRASE : « réessayez » serait un mensonge sur une demande close,
// qu'aucun second essai ne rouvrira. Le 409 est donc le seul échec à ne pas prendre celle de
// l'échec technique.
let sending = false;

async function send() {
  const mode = currentMode();

  sending = true;
  submitButton.disabled = true;
  failure.hidden = true;

  const entry = new FormData(form);

  try {
    const response = await fetch(form.action, { method: "POST", body: new URLSearchParams(entry) });

    // ⚠️ C'EST ENREGISTRÉ DÈS LE CODE DE SUCCÈS, que le corps se lise ou non : un corps perdu ne doit
    // pas poser le bandeau, qui inviterait à réessayer — et à enregistrer la demande deux fois.
    if (response.status === mode.saved) {
      closeOnSaving(mode, await response.text().catch(() => ""));
      return;
    }

    if (response.status === 404 && mode.vanished) {
      mode.vanished();
      return;
    }

    const refusals = response.status === 400 ? await refusalsFromTheServer(response) : {};
    const refused = validatedFields.filter((field) => refusals[field.name]);

    if (refused.length > 0) {
      refusedByTheServer = new Map(
        refused.map((field) => [field, { value: entry.get(field.name), refusal: refusals[field.name] }]),
      );
      showRefusals(refusals);
    } else {
      showFailure(response.status === 409 ? failure.dataset.closed : failure.dataset.technical);
    }
  } catch {
    showFailure(failure.dataset.technical);
  } finally {
    sending = false;
    submitButton.disabled = false;
  }
}

// LE BANDEAU DIT LAQUELLE DES DEUX PHRASES, et se montre. Les mots viennent du gabarit serveur, comme
// tous les autres ; le module choisit, il ne rédige pas.
function showFailure(words) {
  failure.textContent = words;
  failure.hidden = false;
}

// Les refus d'un `ValidationProblem`, un par champ, sous les clés mêmes du corps. Une réponse qui
// n'en porte pas n'en rend aucun.
async function refusalsFromTheServer(response) {
  try {
    const { errors } = await response.json();

    return Object.fromEntries(Object.entries(errors ?? {}).map(([key, refusals]) => [key, refusals[0]]));
  } catch {
    return {};
  }
}

// LA QUALIFICATION DU DROIT PAR IA. Le bouton envoie le Message — et lui seul, avec le jeton
// anti-rejeu — au handler `Propose` de l'écran de qualification, dont la modale porte l'adresse : le
// module ne connaît aucune route (ADR-0024). La qualification PROPOSE le droit ; seul
// l'enregistrement le choisit.
//
// ⚠️ UN MESSAGE VIDE NE PART PAS : le refus va sous le champ Message, comme les autres refus de la
// modale, et le select reste tel qu'il est. Les mots sont ceux que la vue a rendus.
//
// ⚠️ L'ATTENTE PEUT DÉPASSER DEUX MINUTES, ET AUCUN DÉLAI N'EST IMPOSÉ ICI : la limite est celle du
// serveur. Pendant ce temps, seul le bouton est désactivé et `aria-busy` — l'icône le montre —, et
// le reste de la modale, « Enregistrer » compris, reste utilisable. Toute fin le rend.
const proposer = form.querySelector("[data-propose]");
const antiforgery = form.elements.namedItem("__RequestVerificationToken");
const proposalNote = document.getElementById("request-proposal-note");
const toReview = proposalNote.querySelector("[data-to-review]");
const justificationOfTheNote = proposalNote.querySelector("[data-justification]");

// ⚠️ LE REFUS TIENT TANT QUE LE MESSAGE GARDE LA VALEUR REFUSÉE, comme un refus du serveur, et cède
// dès qu'elle change : les règles de la saisie, rejouées à la frappe, ne disent pas les mêmes mots.
function refuseAnEmptyMessage() {
  const refusal = proposer.dataset.messageMissing;

  refusedByTheServer.set(message, { value: message.value, refusal });
  inError.add(message);
  showRefusal(message, refusal);
  message.focus();
}

async function propose() {
  if (trimmed(message) === "") {
    refuseAnEmptyMessage();
    return;
  }

  proposer.disabled = true;
  proposer.setAttribute("aria-busy", "true");

  try {
    const response = await fetch(proposer.dataset.propose, {
      method: "POST",
      body: new URLSearchParams({ Text: message.value, [antiforgery.name]: antiforgery.value }),
    });

    if (response.ok) {
      showTheProposal(await response.json());
    }
  } catch {
    // ⚠️ UN ÉCHEC NE DIT ENCORE RIEN — ni un 400, qui peut être un jeton anti-rejeu périmé et non un
    // Message refusé, ni un 503, ni une coupure : leur bandeau vient avec son propre ticket.
  } finally {
    proposer.disabled = false;
    proposer.removeAttribute("aria-busy");
  }
}

// UN DROIT UNIQUE PROPOSÉ ENTRE DANS LE SELECT, même si un autre y était déjà : c'est une valeur
// changée comme une autre, que la confirmation d'abandon protège. `OutOfScope` n'y est pas une
// option, et n'y entre donc jamais.
//
// LA NOTE DIT LA JUSTIFICATION, quand il y en a une, et « À relire » quand la proposition n'est pas
// corroborée ou que le service n'était pas entier : un verdict sans contrôle ne se lit jamais comme
// un verdict contrôlé. ⚠️ `textContent` : la justification est un texte du moteur, pas du HTML.
function showTheProposal({ rights, reviewSignal, degraded, justification }) {
  const offered = [...right.options].map((option) => option.value);

  if (rights.length === 1 && offered.includes(rights[0].name)) {
    right.value = rights[0].name;
    revalidateTheFieldsInError();
  }

  justificationOfTheNote.textContent = justification ?? "";
  toReview.hidden = reviewSignal === "Corroborated" && !degraded;
  proposalNote.hidden = !justification && toReview.hidden;
}

proposer.addEventListener("click", propose);

// LE TOAST DIT QUELQUES SECONDES CE QUI VIENT D'AVOIR LIEU. Ses mots sont ceux que la page a rendus.
const toastDuration = 5_000;
let toastTimer;

function say(words) {
  toast.textContent = words;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    toast.textContent = "";
  }, toastDuration);
}

// LE GESTE EST ENREGISTRÉ : sa ligne prend sa place dans le tableau, la modale se ferme — sans
// confirmation, rien n'est perdu —, et le toast dit le mot de ce geste-là. La prochaine ouverture
// repartira des valeurs par défaut, comme toutes les ouvertures.
//
// ⚠️ UN CORPS ILLISIBLE NE TOUCHE PAS AU TABLEAU : le geste a eu lieu — le toast le dit —, mais sans
// ligne à mettre, mieux vaut laisser celle qui est là que retirer l'ancienne sans la remplacer.
function closeOnSaving(mode, rowHtml) {
  const row = rowRenderedBy(rowHtml);

  if (row) {
    mode.place(row);
  }

  confirmation.close();
  dialog.close();

  say(mode.said);
}

// La ligne que le serveur a rendue, telle quelle — ou rien, si le corps n'en portait pas.
function rowRenderedBy(rowHtml) {
  const template = document.createElement("template");
  template.innerHTML = rowHtml;

  return template.content.querySelector("tr");
}

// LA LIGNE DE LA DEMANDE EST CELLE QUE LE SERVEUR A RENDUE, par la vue partielle du tableau : le
// module l'insère telle quelle, sans en écrire un mot. L'état vide s'efface alors.
//
// ELLE PREND SA PLACE SELON LE TRI SÉLECTIONNÉ, sur les mêmes clés que lui et dans le même sens :
// elle entre devant la première ligne qu'elle précède, ou en queue si elle n'en précède aucune. Les
// lignes du tableau sont déjà dans cet ordre — le serveur les a rendues dans l'ordre par défaut, et
// chaque changement de tri les y remet —, la première trouvée est donc la bonne. De deux demandes
// reçues le même jour, c'est l'instant d'enregistrement que le serveur a rendu sur la ligne qui
// départage, et le tri en décide comme pour les autres : une demande créée à l'instant est la plus
// récemment enregistrée, et une demande corrigée garde le sien — la corriger ne la rajeunit pas.
//
// ⚠️ LA RECHERCHE EN COURS SE REJOUE ENSUITE : une ligne qui ne lui correspond pas entre cachée, et
// « Aucune demande ne correspond » reste d'accord avec ce que le tableau montre.
//
// ⚠️ C'EST AUSSI PAR ICI QUE PASSE UNE LIGNE CORRIGÉE, l'ancienne retirée : son replacement au tri,
// sa disparition hors recherche, sa nouvelle date limite et le signalement de celle-ci n'ont donc
// aucun code à eux — ils découlent de cette insertion-là, et de la ligne que le serveur a rendue.
function insertRow(row) {
  const next = [...rows].find((existing) => inTheSelectedOrder(row, existing) < 0);

  tableBody.insertBefore(row, next ?? null);
  none.hidden = true;
  applySearch();
}

// UNE LIGNE S'EN VA SANS RECHARGEMENT — supprimée par l'Operator, ou disparue de la base pendant
// qu'il la corrigeait. La dernière partie, l'état vide que le serveur a rendu reparaît.
//
// ⚠️ LA RECHERCHE EN COURS SE REJOUE : la seule ligne trouvée partie, c'est à elle de dire que plus
// rien ne correspond — et de se taire sur un tableau devenu vide.
function removeRow(row) {
  row?.remove();
  none.hidden = rows.length > 0;
  applySearch();
}

// L'identification lie trois champs : un email saisi lève le refus du nom et du prénom. C'est donc
// toute la saisie qui est rejugée à chaque frappe, mais seuls les champs en erreur en affichent l'issue.
function revalidateTheFieldsInError() {
  if (inError.size === 0) {
    return;
  }

  const refusals = refusalsOfTheEntry();

  for (const field of inError) {
    const refusal = refusalOf(field, refusals);
    showRefusal(field, refusal);

    if (!refusal) {
      inError.delete(field);
    }
  }
}

// Le premier champ du formulaire : celui qui prend le focus à l'ouverture.
const firstField = form.elements.namedItem("origin");

// LE BOUTON QUI A OUVERT LA MODALE — le crayon d'une ligne, ou « Créer une demande » : c'est à lui
// que le focus revient à la fermeture, quelle qu'en soit la façon.
let openedBy = null;

const frame = document.getElementById("requests-frame");

// L'OUVERTURE, LA MÊME POUR LES DEUX GESTES : le formulaire repart de zéro, le mode pose ses mots et
// sa route, puis la modification seule y verse les valeurs de sa demande et son identifiant. L'état
// de départ n'est relevé qu'ensuite : c'est à lui, et non aux valeurs par défaut, que « modifié » se
// comparera — une correction ramenée à ce qu'elle était ne modifie rien.
function open(mode, { values, id, from, row }) {
  resetToDefaults();
  applyMode(mode);

  if (values) {
    fill(values);
    identifier.value = id;
  }

  valuesAtOpening = rawValues();
  trimmedValuesAtOpening = trimmedValues();
  openedBy = from;
  rowBeingModified = row ?? null;

  dialog.showModal();

  // `showModal` donnerait le focus à la croix, premier élément focalisable de la modale : il va au
  // premier champ, pour que la saisie — ou la correction — commence aussitôt.
  firstField.focus();
}

// LES VALEURS ENREGISTRÉES ENTRENT DANS LES CHAMPS, chacune sous la clé du champ qu'elle remplit :
// les noms mêmes du formulaire, des deux côtés (RequestForm). Un champ facultatif laissé vide
// revient nul, et se lit vide.
//
// ⚠️ DES PROPRIÉTÉS, JAMAIS DES ATTRIBUTS. Écrire `value` ou `checked` en attribut ferait de la
// demande chargée la valeur par défaut du formulaire : la remise à zéro la retrouverait, et la
// modale de création ne s'ouvrirait plus jamais vide.
function fill(values) {
  for (const [name, value] of Object.entries(values)) {
    const field = form.elements.namedItem(name);

    if (!field) {
      continue;
    }

    if (field.type === "checkbox") {
      field.checked = value === true;
    } else {
      field.value = value ?? "";
    }
  }
}

// LE FOCUS REVIENT AU BOUTON QUI A OUVERT LA SURFACE, quelle qu'elle soit et quelle que soit la façon
// dont elle s'est fermée : sans souris, la navigation reprend là où elle en était.
//
// ⚠️ SI CE BOUTON A DISPARU entre-temps — sa ligne supprimée depuis un autre onglet —, le focus va au
// cadre du tableau, et non en haut de la page.
function giveTheFocusBackTo(opener) {
  if (opener?.isConnected) {
    opener.focus();
  } else {
    frame.focus();
  }
}

dialog.addEventListener("close", () => giveTheFocusBackTo(openedBy));

function requestClose() {
  if (sending) {
    return;
  }

  if (isModified()) {
    confirmation.showModal();
  } else {
    dialog.close();
  }
}

opener.addEventListener("click", () => open("create", { from: opener }));
submitButton.addEventListener("click", attemptSave);
form.addEventListener("input", revalidateTheFieldsInError);

// LA CONFIRMATION. « Continuer la saisie » ne referme qu'elle : le formulaire est là, intact.
// « Abandonner » referme les deux, sans rien envoyer ; la prochaine ouverture repartira des valeurs
// par défaut. Échap sur la confirmation la referme seule, comme « Continuer la saisie » : ce que la
// touche fait par réflexe ne détruit rien.
confirmation.querySelector("[data-continue]").addEventListener("click", () => {
  confirmation.close();
});

confirmation.querySelector("[data-abandon]").addEventListener("click", () => {
  confirmation.close();
  dialog.close();
});

for (const dismiss of dialog.querySelectorAll("[data-dismiss]")) {
  dismiss.addEventListener("click", requestClose);
}

// Échap : le navigateur fermerait seul, sans passer par `requestClose`. Il est donc retenu, et la
// fermeture repasse par le même chemin que les trois autres modes.
//
// ⚠️ LE NAVIGATEUR NE LAISSE PAS TOUJOURS RETENIR ÉCHAP. Chromium ne rend `cancel` annulable que si
// l'Operator a cliqué ou saisi depuis le dernier Échap retenu, et Échap lui-même ne compte pas : des
// Échap répétés finissent par fermer la modale quoi que fasse le module. Une saisie modifiée n'est
// alors pas perdue — elle est toujours dans le formulaire — : la modale est rouverte telle quelle,
// et la confirmation posée par-dessus.
dialog.addEventListener("cancel", (event) => {
  event.preventDefault();

  if (event.cancelable) {
    requestClose();
  } else if (isModified()) {
    dialog.addEventListener("close", reopenAndConfirm, { once: true });
  }
});

function reopenAndConfirm() {
  dialog.showModal();

  // Pendant l'envoi, aucune fermeture n'est demandée : la modale revient seule, et la réponse suivra.
  if (!sending) {
    confirmation.showModal();
  }
}

// LE FOND N'EST PAS UN ÉLÉMENT : un clic dessus arrive sur le `dialog` lui-même, hors de son cadre.
// ⚠️ L'appui ET le relâchement doivent tomber sur le fond. Une sélection de texte commencée dans la
// modale et relâchée dehors produit elle aussi un clic sur le `dialog` : elle ne doit rien fermer.
function closeOnBackdropClick(modal, close) {
  let pressedOnTheBackdrop = false;

  function isOnTheBackdrop(event) {
    if (event.target !== modal) {
      return false;
    }

    const frame = modal.getBoundingClientRect();

    return (
      event.clientX < frame.left ||
      event.clientX > frame.right ||
      event.clientY < frame.top ||
      event.clientY > frame.bottom
    );
  }

  modal.addEventListener("pointerdown", (event) => {
    pressedOnTheBackdrop = isOnTheBackdrop(event);
  });

  modal.addEventListener("click", (event) => {
    if (pressedOnTheBackdrop && isOnTheBackdrop(event)) {
      close();
    }

    pressedOnTheBackdrop = false;
  });
}

closeOnBackdropClick(dialog, requestClose);

// LA SUPPRESSION D'UNE DEMANDE. La poubelle d'une ligne ouvre la confirmation, où le module recopie
// ce que la ligne porte : la phrase que le serveur a composée pour elle, et l'identifiant de sa
// demande. Il n'en écrit aucun mot. « Annuler », Échap et un clic sur le fond la referment sans rien
// envoyer ; Échap, le navigateur le fait seul.
const deletion = document.getElementById("delete-request");
const deletionForm = document.getElementById("delete-request-form");
const deletionConsequence = document.getElementById("delete-request-consequence");
const deleteButton = deletion.querySelector("[data-delete-for-good]");
const requests = document.getElementById("requests");
const none = document.getElementById("requests-none");

// La ligne dont la poubelle a ouvert la confirmation : c'est elle que la suppression retire.
let rowToDelete = null;

// Les lignes ne sont pas écoutées une à une : le tableau l'est, pour toutes à la fois — la poubelle
// comme le crayon.
requests.addEventListener("click", (event) => {
  const trash = event.target.closest('[data-action="delete"]');

  if (trash) {
    openDeletion(trash.closest("tr"));
    return;
  }

  const pencil = event.target.closest('[data-action="edit"]');

  if (pencil) {
    openModification(pencil);
    return;
  }

  const eye = event.target.closest('[data-action="view"]');

  if (eye) {
    openSheet(eye);
  }
});

function openDeletion(row) {
  rowToDelete = row;
  deletionConsequence.textContent = row.dataset.deletionConfirmation;
  deletionForm.elements.namedItem("id").value = row.dataset.requestId;

  // « Annuler » prend le focus : `showModal` honore son `autofocus`.
  deletion.showModal();
}

// ⚠️ PENDANT L'ENVOI, LA CONFIRMATION NE SE FERME PAS : la réponse doit trouver la ligne qu'elle
// concerne — une autre poubelle ne peut pas la lui ravir —, et c'est elle qui la fermera.
let deleting = false;

function closeDeletion() {
  if (!deleting) {
    deletion.close();
  }
}

deletion.querySelector("[data-dismiss]").addEventListener("click", closeDeletion);
closeOnBackdropClick(deletion, closeDeletion);

deletion.addEventListener("cancel", (event) => {
  if (deleting) {
    event.preventDefault();
  }
});

// L'ENVOI. Le formulaire part tel quel au handler qu'il déclare, jeton anti-rejeu compris.
// « Supprimer définitivement » est désactivé pendant l'envoi : un double clic ne part qu'une fois.
//
// QUELLE QUE SOIT L'ISSUE, LA CONFIRMATION SE FERME, ET LE TOAST LA DIT. 204, ou 404 — la demande a
// déjà été supprimée ailleurs, et ce que voulait l'Operator est acquis (ADR-0022) — : la ligne s'en
// va. Tout le reste — un jeton anti-rejeu refusé, une erreur du serveur, une coupure réseau — est un
// échec : la ligne reste, puisque rien n'a été supprimé, et sa poubelle permet de réessayer.
async function deleteForGood() {
  deleting = true;
  deleteButton.disabled = true;

  const deleted = await sendDeletion();

  deleting = false;
  deleteButton.disabled = false;
  deletion.close();

  if (deleted) {
    removeRow(rowToDelete);
    say(toast.dataset.deleted);
  } else {
    say(toast.dataset.deletionFailed);
  }

  rowToDelete = null;
}

// Envoie la suppression, et dit si la demande n'existe plus — supprimée à l'instant, ou avant.
async function sendDeletion() {
  try {
    const response = await fetch(deletionForm.action, {
      method: "POST",
      body: new URLSearchParams(new FormData(deletionForm)),
    });

    return response.status === 204 || response.status === 404;
  } catch {
    return false;
  }
}

deleteButton.addEventListener("click", deleteForGood);

// LA MODIFICATION D'UNE DEMANDE. Le crayon d'une ligne fait lire au serveur les huit valeurs de sa
// demande, puis ouvre la modale en mode modification, pré-remplie : c'est la même modale, le même
// formulaire, les mêmes règles et le même envoi qu'à la création. Seule diffère la place que prend la
// ligne au retour : une correction remplace la sienne, là où une création n'en avait aucune.
//
// ⚠️ LE CRAYON D'UNE DEMANDE CLOSE PORTE `aria-disabled`, JAMAIS `disabled` — pour que son infobulle
// puisse dire pourquoi (voir _RequestRow.cshtml). Le module ne peut donc pas se fier à `disabled`
// pour savoir si le geste est offert : c'est `aria-disabled` qu'il teste, et lui seul.
const editAction = '[data-action="edit"]';

function isOffered(pencil) {
  return pencil.getAttribute("aria-disabled") !== "true";
}

// ⚠️ LE VERROU EST GLOBAL : pendant le chargement, TOUS les crayons du tableau sont éteints. Un
// second clic sur une autre ligne ne peut donc pas lancer une seconde lecture, et « la modale s'ouvre
// avec les valeurs de la mauvaise demande » devient structurellement impossible — il n'y a jamais
// qu'une lecture en vol. Le verrou tombe à l'ouverture comme à l'échec.
//
// ⚠️ AUCUN TEXTE NE CHANGE : un libellé qui change sous le curseur déplace la mise en page, et fait
// perdre au bouton son nom accessible en pleine action. Seuls `aria-busy` et `aria-disabled` bougent.
//
// ⚠️ UN CRAYON DÉJÀ ÉTEINT LE RESTE : le verrou ne rend `aria-disabled` qu'aux crayons à qui il l'a
// posé. Une demande close ne redevient pas modifiable parce qu'un chargement s'est terminé.
let pencilsDimmedByTheLoading = [];

function lockThePencils() {
  pencilsDimmedByTheLoading = [];

  for (const pencil of requests.querySelectorAll(editAction)) {
    pencil.setAttribute("aria-busy", "true");

    if (isOffered(pencil)) {
      pencil.setAttribute("aria-disabled", "true");
      pencilsDimmedByTheLoading.push(pencil);
    }
  }

  // ⚠️ « CRÉER UNE DEMANDE » EST VERROUILLÉ AUSSI : la modale est unique, et une création ouverte
  // entre-temps serait écrasée par les valeurs qui arrivent. Lui n'a pas d'infobulle à montrer :
  // `disabled` suffit.
  opener.disabled = true;
}

function releaseThePencils() {
  for (const pencil of requests.querySelectorAll(editAction)) {
    pencil.removeAttribute("aria-busy");
  }

  for (const pencil of pencilsDimmedByTheLoading) {
    pencil.removeAttribute("aria-disabled");
  }

  pencilsDimmedByTheLoading = [];
  opener.disabled = false;
}

// LE CRAYON D'UNE LIGNE : les valeurs d'abord, la modale ensuite. Un pré-remplissage en échec
// n'ouvre rien — le toast le dit, et la ligne reste à portée d'un second clic.
async function openModification(pencil) {
  if (!isOffered(pencil)) {
    return;
  }

  const row = pencil.closest("tr");

  lockThePencils();
  const values = await readValues(row.dataset.requestId);
  releaseThePencils();

  if (!values) {
    say(toast.dataset.loadFailed);
    return;
  }

  open("modify", { values, id: row.dataset.requestId, from: pencil, row });
}

// LA LIGNE QUE LA MODALE CORRIGE : c'est elle que la correction enregistrée retire du tableau.
let rowBeingModified = null;

// LA LIGNE CORRIGÉE REMPLACE LA SIENNE, SANS RECHARGEMENT : l'ancienne s'en va, et la nouvelle passe
// par l'insertion de la création. ⚠️ DANS CET ORDRE : une ancienne encore là serait une ligne de plus
// à qui se comparer au tri, et la nouvelle pourrait se ranger du mauvais côté d'elle-même.
//
// ⚠️ LE FOCUS SUIT LA LIGNE : le crayon qui a ouvert la modale part avec l'ancienne, et c'est celui
// de la nouvelle qui prend sa place. Sans cela, toute correction enregistrée renverrait le focus au
// cadre du tableau — le recours prévu pour une ligne disparue —, et l'Operator perdrait sa place à
// chaque fois qu'il corrige.
function replaceTheModifiedRow(row) {
  rowBeingModified?.remove();
  insertRow(row);

  openedBy = row.querySelector(editAction) ?? openedBy;
}

// LA DEMANDE N'EXISTE PLUS : elle a été supprimée depuis un autre onglet pendant que l'Operator la
// corrigeait. Il n'y a rien à réessayer — aucune correction ne la rattrapera —, donc pas de bandeau :
// la modale se ferme, sa ligne part du tableau, et le toast dit ce qui lui est arrivé.
//
// ⚠️ LE FOCUS PART AU CADRE DU TABLEAU, et c'est voulu : le crayon qui avait ouvert la modale s'en
// est allé avec sa ligne, et la fermeture le constate (voir l'écouteur `close`).
function dropTheVanishedRequest() {
  removeRow(rowBeingModified);
  confirmation.close();
  dialog.close();

  say(toast.dataset.vanished);
}

// Les huit valeurs d'une demande, ou rien du tout — 404 d'une demande supprimée ailleurs, erreur du
// serveur, coupure réseau : toutes les issues sans valeurs se valent, et l'Operator lit la même
// phrase. L'adresse est celle que la modale porte ; le module n'écrit aucune route.
async function readValues(id) {
  const address = new URL(dialog.dataset.values, document.baseURI);
  address.searchParams.set("id", id);

  try {
    const response = await fetch(address, { headers: { Accept: "application/json" } });

    return response.ok ? await response.json() : null;
  } catch {
    return null;
  }
}

// LA FICHE D'UNE DEMANDE. L'œil d'une ligne ouvre la fiche que le serveur a rendue : la lecture à
// l'écran de ce que le service tient de cette demande, ancrée au bord droit, en lecture seule.
//
// ⚠️ AUCUN ALLER-RETOUR RÉSEAU : tout ce que la fiche montre, la page l'a déjà sur la ligne. Il n'y
// a donc ni état de chargement, ni état d'échec, ni verrou — l'ouverture est instantanée.
//
// ⚠️ LES QUATRE FERMETURES — Échap, la croix, le fond, « Fermer » — FERMENT DIRECTEMENT : rien n'est
// en jeu dans une lecture, et la confirmation d'abandon n'a rien à y faire. Échap, le navigateur le
// fait seul ; rien n'est retenu.
const sheet = document.getElementById("request-sheet");

// L'ŒIL QUI A OUVERT LA FICHE : c'est à lui que le focus revient à la fermeture, quelle qu'en soit
// la façon. ⚠️ Il se retient à part de celui de la modale de saisie : les deux surfaces s'ouvrent
// depuis des boutons différents, et chacune rend le focus au sien.
let sheetOpenedBy = null;

// LE TITRE DE LA FICHE : il nomme la personne, et jamais l'identifiant technique de la demande.
const sheetTitle = document.getElementById("request-sheet-title");

function openSheet(eye) {
  sheetOpenedBy = eye;

  fillSheet(eye.closest("tr"));
  sheet.showModal();
}

// CE QUE LA FICHE PORTE, EN UNE PASSE UNIQUE : pour chaque cellule nommée de la ligne, le module
// CLONE SES NŒUDS ENFANTS dans la cible de même nom. Le badge du statut et le signalement de la date
// limite empruntent alors le chemin du texte, sans aucun cas particulier — et la règle « le script
// n'écrit aucun mot » devient littérale : il ne fait que déplacer des nœuds rendus par le serveur.
// Le « — » d'une valeur absente en fait partie : il vient de la cellule, comme le reste.
//
// ⚠️ LES CELLULES SE LISENT PAR LEUR NOM, JAMAIS PAR LEUR INDEX : l'ordre des colonnes est un
// compromis de largeur d'écran, pas un contrat — ajouter ou déplacer une colonne ne casse pas la
// fiche en silence. Une cellule sans cible dans la fiche n'est pas une erreur : la fiche montre ce
// qu'elle montre.
//
// TROIS VALEURS NE VIENNENT D'AUCUNE CELLULE, parce qu'aucune colonne ne les montre : le titre — le
// nom replié de la personne —, l'origine et le message, que la ligne porte en `data-sheet-*`. Ce
// sont les trois seules affectations explicites, et le libellé français de l'origine y arrive déjà
// écrit par le serveur. ⚠️ `textContent`, jamais `innerHTML` : le message est un texte que la
// personne a écrit, et il s'affiche tel qu'il est enregistré.
//
// ⚠️ CES TROIS CIBLES-LÀ NE SE GARDENT PAS, quand la boucle se garde : une cellule qui n'intéresse
// pas la fiche est une possibilité, une fiche qui aurait perdu son titre, son origine ou son
// message est un gabarit cassé — et il vaut mieux qu'il se voie.
function fillSheet(row) {
  for (const cell of row.querySelectorAll("td[data-field]")) {
    const clone = cell.cloneNode(true);

    sheet.querySelector(`[data-field="${cell.dataset.field}"]`)?.replaceChildren(...clone.childNodes);
  }

  sheetTitle.textContent = row.dataset.sheetPerson;
  sheet.querySelector('[data-field="origin"]').textContent = row.dataset.sheetOrigin;
  sheet.querySelector('[data-field="message"]').textContent = row.dataset.sheetMessage;
}

sheet.addEventListener("close", () => giveTheFocusBackTo(sheetOpenedBy));

function closeSheet() {
  sheet.close();
}

// La croix de la tête et « Fermer » du pied : deux sorties, la même fermeture.
for (const dismiss of sheet.querySelectorAll("[data-dismiss]")) {
  dismiss.addEventListener("click", closeSheet);
}

closeOnBackdropClick(sheet, closeSheet);

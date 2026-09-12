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
// La demande créée, le module insère dans le tableau la ligne que le serveur a rendue pour elle.
//
// La poubelle de chaque ligne ouvre la confirmation de suppression, avec la phrase de sa ligne ;
// « Supprimer définitivement » l'envoie au handler de la page, et la ligne s'en va sans rechargement —
// sauf échec, que le toast dit.

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

function applySort() {
  // Les valeurs des options sont celles que le serveur a rendues (Board.cshtml).
  const direction = sort.value === "oldest" ? 1 : -1;
  const sorted = [...rows].sort((first, second) => direction * byReceptionThenCreation(first, second));

  tableBody.append(...sorted);
}

sort.addEventListener("change", applySort);

const dialog = document.getElementById("create-request");
const confirmation = document.getElementById("abandon-entry");
const opener = document.getElementById("create-request-open");
const form = document.getElementById("create-request-form");
const receivedOn = form.elements.namedItem("receivedOn");
const createButton = form.querySelector("[data-create]");
const failure = document.getElementById("create-request-failure");
const toast = document.getElementById("requests-toast");

// Les dix messages, écrits par le serveur (DataSubjectRequestMessages) : le module n'en écrit aucun.
const messages = JSON.parse(document.getElementById("create-request-messages").textContent);

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

// LE FORMULAIRE REPART DE ZÉRO À CHAQUE OUVERTURE : l'Operator n'hérite jamais d'une saisie
// précédente. Les valeurs par défaut sont celles que le serveur a rendues, sauf « aujourd'hui », qui
// est recalculé ici — une page restée ouverte au-delà de minuit ne doit ni proposer la veille, ni
// interdire le jour même. Il devient la valeur par défaut du champ, et non seulement sa valeur.
function resetToDefaults() {
  form.reset();

  const today = todayInParis();
  receivedOn.max = today;
  receivedOn.defaultValue = today;
  receivedOn.value = today;

  forgetRefusals();
  failure.hidden = true;

  valuesAtOpening = rawValues();
}

// LES RÈGLES SONT CELLES DU SERVICE (DataSubjectRequest.Receive), recopiées une à une pour que le
// navigateur refuse exactement ce que le serveur refuserait. C'est un confort : le serveur fait foi.

// ⚠️ LES ESPACES SONT CEUX DE .NET (`char.IsWhiteSpace`, qui fonde `Trim` et `IsNullOrWhiteSpace`),
// et non ceux du `trim` de JavaScript : l'un retire le U+0085 que l'autre garde, et garde le U+FEFF
// que l'autre retire.
const dotnetWhiteSpace = "[\\t\\n\\v\\f\\r \\u0085\\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000]";
const bordersOfWhiteSpace = new RegExp(`^${dotnetWhiteSpace}+|${dotnetWhiteSpace}+$`, "g");

function trimmed(field) {
  return field.value.replace(bordersOfWhiteSpace, "");
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

// « CRÉER » RESTE CLIQUABLE HORS ENVOI : chaque clic juge toute la saisie, affiche chaque refus sous
// son champ et donne le focus au premier. Une saisie sans refus part au serveur.
function attemptCreation() {
  refusedByTheServer = new Map();
  showRefusals(refusalsOfTheEntry());

  if (inError.size === 0) {
    send();
  }
}

// L'ENVOI. Le formulaire part tel quel au handler qu'il déclare, jeton anti-rejeu compris — la date
// au format ISO du champ, le droit sous son nom canonique : ce que le handler lit.
//
// ⚠️ « CRÉER » EST DÉSACTIVÉ PENDANT L'ENVOI : un double clic n'enregistre jamais deux demandes. Il
// redevient cliquable quelle que soit l'issue. La modale, elle, ne se ferme pas tant que l'envoi
// court : la réponse doit trouver la saisie qu'elle concerne, pas une modale rouverte à zéro.
//
// Trois issues. 201 : la demande est enregistrée, et le corps porte sa ligne. 400 portant des refus
// de la saisie : ils vont sous leurs champs, comme ceux du navigateur. Tout le reste — un 400 sans
// refus, qui est un jeton anti-rejeu refusé, une erreur du serveur, une coupure réseau — : le
// bandeau, et la saisie reste là.
let sending = false;

async function send() {
  sending = true;
  createButton.disabled = true;
  failure.hidden = true;

  const entry = new FormData(form);

  try {
    const response = await fetch(form.action, { method: "POST", body: new URLSearchParams(entry) });

    // ⚠️ LA DEMANDE EST ENREGISTRÉE DÈS LE 201, que son corps se lise ou non : un corps perdu ne doit
    // pas poser le bandeau, qui inviterait à réessayer — et à enregistrer la demande deux fois.
    if (response.status === 201) {
      closeOnCreation(await response.text().catch(() => ""));
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
      failure.hidden = false;
    }
  } catch {
    failure.hidden = false;
  } finally {
    sending = false;
    createButton.disabled = false;
  }
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

// LA DEMANDE EST CRÉÉE : sa ligne entre dans le tableau, la modale se ferme — sans confirmation, rien
// n'est perdu —, et le toast le dit. La prochaine ouverture repartira des valeurs par défaut, comme
// toutes les ouvertures.
function closeOnCreation(rowHtml) {
  insertRow(rowHtml);
  confirmation.close();
  dialog.close();

  say(toast.dataset.created);
}

// LA LIGNE DE LA NOUVELLE DEMANDE EST CELLE QUE LE SERVEUR A RENDUE, par la vue partielle du
// tableau : le module l'insère telle quelle, sans en écrire un mot. L'état vide s'efface alors.
//
// ELLE PREND SA PLACE DANS L'ORDRE PAR DÉFAUT — la date de réception la plus récente d'abord, puis la
// date de création la plus récente. Elle vient d'être créée : elle passe devant toutes celles reçues
// le même jour, et se place donc devant la première reçue ce jour-là ou avant. Les dates ISO se
// comparent comme des chaînes. ⚠️ Le tri choisi et la recherche en cours ne sont pas encore
// consultés : c'est l'objet du ticket #393.
function insertRow(rowHtml) {
  const template = document.createElement("template");
  template.innerHTML = rowHtml;

  const row = template.content.querySelector("tr");

  if (!row) {
    return;
  }

  const receivedOn = row.dataset.receivedOn;
  const next = [...rows].find((existing) => existing.dataset.receivedOn <= receivedOn);

  tableBody.insertBefore(row, next ?? null);
  none.hidden = true;
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

function open() {
  resetToDefaults();
  dialog.showModal();

  // `showModal` donnerait le focus à la croix, premier élément focalisable de la modale : il va au
  // premier champ, pour que la saisie commence aussitôt.
  form.elements[0].focus();
}

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

opener.addEventListener("click", open);
createButton.addEventListener("click", attemptCreation);
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

// Les lignes ne sont pas écoutées une à une : le tableau l'est, pour toutes à la fois.
requests.addEventListener("click", (event) => {
  const trash = event.target.closest('[data-action="delete"]');

  if (trash) {
    openDeletion(trash.closest("tr"));
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
    removeTheDeletedRow();
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

// LA LIGNE S'EN VA SANS RECHARGEMENT. La dernière partie, l'état vide que le serveur a rendu reparaît.
//
// ⚠️ LA RECHERCHE EN COURS SE REJOUE : la seule ligne trouvée partie, c'est à elle de dire que plus
// rien ne correspond — et de se taire sur un tableau devenu vide.
function removeTheDeletedRow() {
  rowToDelete.remove();
  none.hidden = requests.tBodies[0].rows.length > 0;
  applySearch();
}

deleteButton.addEventListener("click", deleteForGood);

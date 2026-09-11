// LE TABLEAU DES DEMANDES RGPD : le bouton « Créer une demande » ouvre la modale que le serveur a
// rendue, formulaire remis à zéro ; « Créer » juge la saisie avec les règles du service ; et quatre
// modes de fermeture la referment — « Annuler », la croix, Échap, un clic sur le fond.
//
// ⚠️ POUR L'INSTANT, CHACUN DES QUATRE FERME DIRECTEMENT, saisie comprise. Ils passent tous par
// `requestClose`, et c'est là que la confirmation d'abandon se posera, avec son propre ticket :
// aucun mode de fermeture ne doit pouvoir la contourner.

const dialog = document.getElementById("create-request");
const opener = document.getElementById("create-request-open");
const form = document.getElementById("create-request-form");
const receivedOn = form.elements.namedItem("receivedOn");
const createButton = form.querySelector("[data-create]");

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

function forgetRefusals() {
  for (const field of validatedFields) {
    showRefusal(field, undefined);
  }

  inError = new Set();
}

// « CRÉER » RESTE TOUJOURS CLIQUABLE : chaque clic juge toute la saisie, affiche chaque refus sous son
// champ et donne le focus au premier. Tant que l'envoi n'est pas branché, une saisie valide s'arrête là.
function attemptCreation() {
  const refusals = refusalsOfTheEntry();

  for (const field of validatedFields) {
    showRefusal(field, refusals[field.name]);
  }

  inError = new Set(validatedFields.filter((field) => refusals[field.name]));
  validatedFields.find((field) => inError.has(field))?.focus();
}

// L'identification lie trois champs : un email saisi lève le refus du nom et du prénom. C'est donc
// toute la saisie qui est rejugée à chaque frappe, mais seuls les champs en erreur en affichent l'issue.
function revalidateTheFieldsInError() {
  if (inError.size === 0) {
    return;
  }

  const refusals = refusalsOfTheEntry();

  for (const field of inError) {
    showRefusal(field, refusals[field.name]);

    if (!refusals[field.name]) {
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
  dialog.close();
}

opener.addEventListener("click", open);
createButton.addEventListener("click", attemptCreation);
form.addEventListener("input", revalidateTheFieldsInError);

for (const dismiss of dialog.querySelectorAll("[data-dismiss]")) {
  dismiss.addEventListener("click", requestClose);
}

// Échap : le navigateur fermerait seul, sans passer par `requestClose`. Il est donc retenu, et la
// fermeture repasse par le même chemin que les trois autres modes.
dialog.addEventListener("cancel", (event) => {
  event.preventDefault();
  requestClose();
});

// LE FOND N'EST PAS UN ÉLÉMENT : un clic dessus arrive sur le `dialog` lui-même, hors de son cadre.
// ⚠️ L'appui ET le relâchement doivent tomber sur le fond. Une sélection de texte commencée dans la
// modale et relâchée dehors produit elle aussi un clic sur le `dialog` : elle ne doit rien fermer.
let pressedOnTheBackdrop = false;

function isOnTheBackdrop(event) {
  if (event.target !== dialog) {
    return false;
  }

  const frame = dialog.getBoundingClientRect();

  return (
    event.clientX < frame.left ||
    event.clientX > frame.right ||
    event.clientY < frame.top ||
    event.clientY > frame.bottom
  );
}

dialog.addEventListener("pointerdown", (event) => {
  pressedOnTheBackdrop = isOnTheBackdrop(event);
});

dialog.addEventListener("click", (event) => {
  if (pressedOnTheBackdrop && isOnTheBackdrop(event)) {
    requestClose();
  }

  pressedOnTheBackdrop = false;
});

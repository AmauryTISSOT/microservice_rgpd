// LE TABLEAU DES DEMANDES RGPD : le bouton « Créer une demande » ouvre la modale que le serveur a
// rendue, formulaire remis à zéro ; « Créer » juge la saisie avec les règles du service, puis
// l'envoie au handler de la page ; et quatre modes de fermeture la referment — « Annuler », la
// croix, Échap, un clic sur le fond.
//
// ⚠️ L'OPERATOR NE PERD JAMAIS UNE SAISIE PAR MÉGARDE. Les quatre modes passent tous par
// `requestClose` : un formulaire non modifié s'y ferme directement, un formulaire modifié y ouvre la
// confirmation d'abandon. Aucun mode de fermeture ne doit pouvoir la contourner.

const dialog = document.getElementById("create-request");
const confirmation = document.getElementById("abandon-entry");
const opener = document.getElementById("create-request-open");
const form = document.getElementById("create-request-form");
const receivedOn = form.elements.namedItem("receivedOn");
const createButton = form.querySelector("[data-create]");
const failure = document.getElementById("create-request-failure");
const toast = document.getElementById("create-request-toast");

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

function forgetRefusals() {
  for (const field of validatedFields) {
    showRefusal(field, undefined);
  }

  inError = new Set();
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
  showRefusals(refusalsOfTheEntry());

  if (inError.size === 0) {
    send();
  }
}

// L'ENVOI. Le formulaire part tel quel au handler qu'il déclare, jeton anti-rejeu compris — la date
// au format ISO du champ, le droit sous son nom canonique : ce que le handler lit.
//
// ⚠️ « CRÉER » EST DÉSACTIVÉ PENDANT L'ENVOI : un double clic n'enregistre jamais deux demandes. Il
// redevient cliquable quelle que soit l'issue.
//
// Trois issues. 201 : la demande est enregistrée. 400 portant des refus de la saisie : ils vont sous
// leurs champs, comme ceux du navigateur. Tout le reste — un 400 sans refus, qui est un jeton
// anti-rejeu refusé, une erreur du serveur, une coupure réseau — : le bandeau, et la saisie reste là.
async function send() {
  createButton.disabled = true;
  failure.hidden = true;

  try {
    const response = await fetch(form.action, {
      method: "POST",
      body: new URLSearchParams(new FormData(form)),
    });

    if (response.status === 201) {
      created();
      return;
    }

    const refusals = response.status === 400 ? await refusalsFromTheServer(response) : {};

    if (validatedFields.some((field) => refusals[field.name])) {
      showRefusals(refusals);
    } else {
      failure.hidden = false;
    }
  } catch {
    failure.hidden = false;
  } finally {
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

// LA DEMANDE EST CRÉÉE : la modale se ferme — sans confirmation, rien n'est perdu —, et le toast le
// dit quelques secondes. Ses mots sont ceux que la page a rendus. La prochaine ouverture repartira
// des valeurs par défaut, comme toutes les ouvertures.
const toastDuration = 5_000;
let toastTimer;

function created() {
  confirmation.close();
  dialog.close();

  toast.textContent = toast.dataset.created;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    toast.textContent = "";
  }, toastDuration);
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
  confirmation.showModal();
}

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

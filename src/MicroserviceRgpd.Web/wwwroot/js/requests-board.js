// LE TABLEAU DES DEMANDES RGPD : le bouton « Créer une demande » ouvre la modale que le serveur a
// rendue, formulaire remis à zéro, et quatre modes de fermeture la referment — « Annuler », la croix,
// Échap, un clic sur le fond.
//
// ⚠️ L'OPERATOR NE PERD JAMAIS UNE SAISIE PAR MÉGARDE. Les quatre modes passent tous par
// `requestClose` : un formulaire non modifié s'y ferme directement, un formulaire modifié y ouvre la
// confirmation d'abandon. Aucun mode de fermeture ne doit pouvoir la contourner.

const dialog = document.getElementById("create-request");
const confirmation = document.getElementById("abandon-entry");
const opener = document.getElementById("create-request-open");
const form = document.getElementById("create-request-form");
const receivedOn = form.elements.namedItem("receivedOn");

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

  valuesAtOpening = rawValues();
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

// LE DÉPÔT D'UN RELEVÉ : le bouton « Copier la requête » met dans le presse-papiers le texte exact
// que l'écran affiche — la requête embarquée, jamais une copie.
//
// ⚠️ LE BOUTON NAÎT `hidden`, et c'est ce module qui le montre : sans script, ou sans presse-papiers
// (contexte non sécurisé), l'écran reste entier et la requête se sélectionne à la main.

const button = document.querySelector("button.copy-query");
const query = document.getElementById("listing-query");

if (button && query && navigator.clipboard) {
  const label = button.querySelector("span");
  const idle = label.textContent;

  button.hidden = false;

  button.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(query.textContent);
      label.textContent = button.dataset.copied;
    } catch {
      // Le presse-papiers a refusé : on le dit, et la sélection à la main reste possible.
      label.textContent = "Copie impossible — sélectionnez la requête";
    }

    setTimeout(() => {
      label.textContent = idle;
    }, 2000);
  });
}

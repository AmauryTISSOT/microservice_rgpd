// LA QUALIFICATION : le compteur dit combien de caractères la demande porte, contre le plafond que
// le serveur a écrit. Il n'empêche rien — le refus au-delà du plafond reste celui du service.
//
// ⚠️ SANS CE MODULE, l'écran annonce « Au plus N caractères » et reste entier.

const area = document.getElementById("texte");
const count = document.querySelector(".composer-count");

if (area && count) {
  const ceiling = Number(count.dataset.ceiling);
  const format = new Intl.NumberFormat("fr-FR");

  const render = () => {
    const length = area.value.length;
    count.textContent = `${format.format(length)} / ${format.format(ceiling)} caractères`;
    count.classList.toggle("over", length > ceiling);
  };

  area.addEventListener("input", render);
  render();
}

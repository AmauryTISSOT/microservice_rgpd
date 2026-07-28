"""L'amont du moteur LLM, en faux — le seul du dépôt, et le seul dont la suite ait besoin.

**Aucun test n'appelle Ollama** : un test qui exige un GPU est un test qui ne tourne jamais, et un
test qui ne tourne jamais ment. Ce faux tient en quelques lignes parce que le contrat que le moteur
exige de son amont est étroit — une consigne, un texte, une réponse. C'est cette étroitesse, et non
une astuce de test, qui rend la suite exécutable sur la machine du développeur pressé.

Importable seulement une fois `conftest` passé : `llm` lit sa configuration à l'import.
"""

import json

from qualification_sidecar import llm

#: Un avis complet et bien formé, tel que le modèle est censé le rendre. Les tests le dérivent par
#: `VALID_VERDICT | {…}` pour n'abîmer qu'un champ à la fois, et ne jamais confondre deux refus.
VALID_VERDICT = {
    "droits": ["acces"],
    "justification": "Le texte réclame une copie des données détenues.",
    "confiance": "moyenne",
}


class FakeModel:
    """Un amont docile : il rend ce qu'on lui a confié, lève ce qu'on lui a confié, et note ce qu'on
    lui a demandé."""

    def __init__(self, content=None, served_model="modele-servi:7b", raises=None):
        # Une chaîne passe telle quelle : c'est ce qui permet de lui faire rendre autre chose que
        # du JSON, ce qu'un dictionnaire ne saurait pas exprimer.
        self._content = content if isinstance(content, str) else json.dumps(content)
        self._served_model = served_model
        self._raises = raises
        self.system = None
        self.user = None

    async def answer(self, *, system, user):
        self.system, self.user = system, user

        if self._raises is not None:
            raise self._raises

        return llm.ModelAnswer(content=self._content, served_model=self._served_model)

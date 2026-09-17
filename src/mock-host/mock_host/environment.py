"""Les réglages que le mock lit dans son environnement, et la façon dont il les refuse.

Un réglage illisible **arrête le démarrage** plutôt que de passer pour une valeur par défaut : une
faute de frappe passerait sinon pour un réglage, et le mock répondrait — ou écouterait — là où
personne ne l'attend. Le message nomme la variable fautive et recopie ce qu'elle valait.
"""

from __future__ import annotations

import os
import re


def int_setting(name: str, *, default: int, minimum: int, maximum: int, what: str) -> int:
    """La variable `name` lue comme un entier de `minimum` à `maximum` — ou `default` si elle se tait.

    ⚠️ **Ce qui est lu est exactement ce qui est écrit** : `int()` accepterait « +5 », « 5_03 », une
    espace ou des chiffres non latins. Ils sont illisibles ici, comme côté service.
    """
    raw = os.environ.get(name, "").strip()

    if not raw:
        return default

    if not re.fullmatch(r"-?[0-9]+", raw) or not minimum <= int(raw) <= maximum:
        raise ValueError(f"{name} vaut « {raw} », qui n'est pas {what}.")

    return int(raw)

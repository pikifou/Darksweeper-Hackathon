# Dark Sweeper – Mines / Cases interactives

*(Hackathon Scope – Right Click / Open Logic)*

## 1. Règle fondamentale

> Chaque case “mine” est un **point d’intérêt**.
> Quand le joueur **interagit (clic droit)**, le jeu déclenche un **événement** appartenant à un type déterminé (ou tiré au sort selon tes règles).

Chaque événement :

* peut modifier les PV,
* peut donner un gain,
* doit être loggé dans le `PlayerProfile` (RunLog),
* et peut influencer les systèmes narratifs (plus tard).

---

## 2. Types d’événements (liste officielle)

Pour le hackathon, on limite à **4 types** :

1. **Combat**
2. **Chest** (coffre)
3. **Dialogue** (rencontre / choix moral)
4. **Shrine** (autel / sacrifice)

---

## 3. Comportement commun à tous les types

### États

* `Hidden` : non révélé
* `Revealed` : révélé
* `Resolved` : interaction terminée (ne se rejoue pas)

### Interaction

* **Clic droit** sur une case révélée → ouvre l’interaction (ou déclenche l’action associée)
* Une case ne peut pas être “résolue” deux fois.

### Logging minimal (commun)

Chaque événement doit produire un `RunEvent` avec :

* `event_type` (COMBAT / CHEST / DIALOGUE / SHRINE)
* `tile_id` (ou coord)
* `level_id`
* `hp_before`, `hp_after`
* `timestamp` ou `event_index`

---

## 4. Type 1 – COMBAT (remplace Shadow)

### Intention

Le combat est le **contenu principal**, répétable, qui met de la pression sur les PV et pousse à l’arbitrage.

### Déclenchement (clic droit)

* Lancer un combat associé à la case.

### Variantes hackathon (simples)

Tu peux garder 2 sous-types max :

* **Combat normal**
* **Combat élite** (plus dangereux, plus rentable)

### Résolution

* Le combat se résout (mini-système existant chez toi).
* À la fin :

  * la case passe `Resolved`
  * le joueur a un `hp_delta`
  * éventuellement une petite récompense (optionnel)

### Logging

```
event_type: COMBAT
combat_tier: NORMAL | ELITE
hp_delta: -X
reward: NONE | <reward_type>
```

### Données utiles à logger (pour narration future)

* `combat_tier`
* `damage_taken`
* `hp_before/hp_after`
* `did_player_engage` (si tu as des cas où il peut éviter / fuir)

---

## 5. Type 2 – CHEST (coffre, tentation non triviale)

### Intention

Un coffre n’est jamais un no-brainer : il doit être **risqué** ou **coûteux**.

### Choix joueur

* **Open**
* **Ignore**

### États internes (invisibles)

* `REWARD` : gain net
* `TRAPPED` : gain + perte de PV

*(Hackathon : uniquement ces deux)*

### Résolution

* Open → applique état, puis `Resolved`
* Ignore → `Resolved` (ou laissé “non résolu” si tu veux qu’il puisse revenir, mais pour hackathon je recommande : résolu)

### Logging

**Open**

```
event_type: CHEST
player_choice: OPEN
chest_state: REWARD | TRAPPED
hp_delta: -X / 0
reward: <reward_type>
```

**Ignore**

```
event_type: CHEST
player_choice: IGNORE
chest_state: UNKNOWN
```

---

## 6. Type 3 – DIALOGUE (rencontre / choix moral)

### Intention

Créer du sens et des contradictions avec très peu de texte.

### Choix possibles (max 3)

* **Help**
* **Harm**
* **Ignore**

*(Tous les dialogues n’ont pas forcément les 3)*

### Effets gameplay (simples)

* Help : coûte souvent des PV / temps / risque
* Harm : peut donner PV / reward
* Ignore : neutre ou léger coût (dette future, optionnel)

### Résolution

* 1 choix → effets appliqués → `Resolved`

### Logging

```
event_type: DIALOGUE
dialogue_id: <id>
choice: HELP | HARM | IGNORE
hp_delta: -X / +X / 0
reward: NONE | <reward_type>
```

---

## 7. Type 4 – SHRINE (autel / sacrifice)

### Intention

Un sacrifice explicite : “tes valeurs coûtent quelque chose”.

### Choix joueur

* **Sacrifice**
* **Refuse**

### Effets gameplay (simples)

* Sacrifice :

  * perte PV fixe
  * gain (buff / info / protection boss / etc.)
* Refuse :

  * rien immédiat

### Résolution

* choix → effets → `Resolved`

### Logging

```
event_type: SHRINE
shrine_id: <id>
accepted: true | false
hp_delta: -X / 0
reward: NONE | <reward_type>
```

---

## 8. Catalogue des “Rewards” (communs aux objets)

Pour rester limité au hackathon, choisis **2–3 reward types max** :

* **HP_GAIN** : +PV immédiat
* **VISION_GAIN** : améliore visibilité / falloff / rayon
* **BUFF** : effet court (ex: réduire dégâts prochain combat)

Chaque event peut avoir `reward_type` + `reward_value`.

---

## 9. Règles d’assemblage (distribution des cases)

Tu as deux options simples :

### Option A – Distribution fixe par niveau

Ex:

* Niveau 1 : 70% Combat, 20% Chest, 10% Shrine
* Niveau 2 : 60% Combat, 20% Dialogue, 20% Chest
* Niveau 3 : Boss + quelques combats élite

### Option B – Tirage au sort pondéré

Chaque case a un tirage à la révélation ou à l’interaction.

*(Hackathon : je recommande A, plus contrôlable et démo-friendly.)*

---

## 10. Helper: “What happens on right click?”

Spécification fonctionnelle :

* Entrée : `tile_id`
* Sortie : un objet “InteractionDescriptor” qui décrit :

  * `event_type`
  * `available_choices` (si applicable)
  * `risk_hint` (optionnel : ex “uncertain”)
  * `is_resolved`

But : l’UI sait quoi afficher sans logique cachée.

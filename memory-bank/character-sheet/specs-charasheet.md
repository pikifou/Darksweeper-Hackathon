# Cahier des charges – Système de questions (Player Profiling Intro)

## 1. Objectif du système

Le système de questions a pour objectif de :

* établir un **profil moral et comportemental du joueur** avant toute phase de gameplay,
* positionner ce profil sur deux axes fondamentaux :

  * **Action ↔ Inaction**
  * **Empathie ↔ Détachement**
* en déduire :

  * un **dieu majeur** (quadrant dominant),
  * un **dieu mineur** (tension secondaire),
* générer ensuite :

  * une **intro narrative personnalisée**,
  * une **orientation du contenu du jeu** (épreuves, coûts, boss, outro).

Ce système **n’est pas un questionnaire abstrait** :
il est **diégétique**, incarné visuellement, et intégré à la mise en scène.

---

## 2. Principes de design fondamentaux

### 2.1. Pas de test psychologique explicite

* Le joueur ne doit jamais avoir l’impression de remplir un test.
* Les questions doivent être :

  * concrètes,
  * situées,
  * formulées comme des réactions à des situations plausibles.
* Aucune réponse n’est présentée comme « meilleure ».

---

### 2.2. Diégèse totale

* Le questionnaire **fait partie de l’expérience narrative**.
* Il n’existe pas de rupture entre :

  * interface,
  * narration,
  * progression visuelle.
* Le joueur **avance physiquement** dans l’espace fictionnel en répondant.

---

### 2.3. Progression incarnée

* Chaque réponse est un **pas en avant**.
* Répondre = s’engager = avancer.
* Le joueur ne « valide pas un écran »,
  il **passe à un état narratif suivant**.

---

## 3. Structure générale du système

### 3.1. Nombre de questions

* **6 questions fixes**, toujours posées dans le même ordre.
* Chaque question contribue au score final sur les deux axes.

---

### 3.2. Format d’une question

Chaque question est composée de :

* un **texte de question** (court, direct),
* **4 réponses possibles**, correspondant implicitement aux 4 quadrants :

  * Action + Empathie
  * Action + Détachement
  * Inaction + Empathie
  * Inaction + Détachement

Le mapping score n’est **jamais visible** par le joueur.

---

## 4. Mise en scène visuelle et temporelle

### 4.1. États visuels (images clés)

Le système repose sur **6 images fixes clés**, numérotées :

* Image 1 : état initial du personnage
* Image 2
* Image 3
* Image 4
* Image 5
* Image 6 : état final du questionnaire

Chaque image représente :

* le personnage dans une posture différente,
* une progression spatiale ou symbolique,
* un état narratif plus avancé.

---

### 4.2. Transitions vidéo

Entre chaque image clé :

* une **vidéo de transition** est jouée :

  * Vidéo 1→2
  * Vidéo 2→3
  * Vidéo 3→4
  * Vidéo 4→5
  * Vidéo 5→6

Ces vidéos représentent :

* un déplacement,
* une descente,
* une avancée,
* ou une transformation subtile du personnage.

---

### 4.3. Règle de déclenchement

* Le joueur sélectionne une réponse.
* Il clique sur **“Next”**.
* La vidéo correspondant à la transition vers l’étape suivante est immédiatement lancée.
* La question suivante apparaît **uniquement à la fin de la vidéo**.

👉 Il n’y a **aucun écran de chargement**,
👉 aucun retour en arrière,
👉 aucun saut.

---

## 5. Interface utilisateur (design, pas technique)

### 5.1. Question Box

À chaque étape :

* une **box de question** est affichée par-dessus l’image ou la scène,
* elle contient :

  * le texte de la question,
  * les 4 réponses cliquables,
  * un bouton “Next” (inactif tant qu’une réponse n’est pas choisie).

La box :

* est sobre,
* ne détourne pas l’attention de la scène,
* peut disparaître pendant la vidéo.

---

### 5.2. Interaction utilisateur

* Une seule action possible : **choisir une réponse**.
* Pas de survol explicatif.
* Pas d’indicateur de progression chiffré (ex. “Question 3/6”).
* La progression est **perçue visuellement**, pas numériquement.

---

## 6. Logique de progression narrative

### 6.1. Sens de la progression

La succession des questions doit donner l’impression que :

* le joueur s’enfonce,
* se dévoile,
* se met progressivement en contradiction ou en cohérence avec lui-même.

Les premières questions sont plus générales,
les dernières touchent à :

* la limite personnelle,
* le renoncement,
* le coût de l’engagement.

---

### 6.2. Finalisation

À la fin de la 6e question :

* l’image finale (Image 6) reste affichée,
* aucune question supplémentaire n’apparaît,
* le système dispose alors de :

  * ActionScore
  * EmpathyScore
* ces scores servent à :

  * déterminer le dieu majeur,
  * déterminer le dieu mineur,
  * préparer l’intro narrative suivante.

---

## 7. Contraintes de design importantes

* Le joueur ne peut pas :

  * revenir en arrière,
  * modifier ses réponses,
  * consulter un résumé.
* Les réponses doivent être :

  * moralement défendables,
  * jamais caricaturales,
  * jamais ironiques.
* Le système doit rester :

  * court (quelques minutes),
  * fluide,
  * solennel mais non pesant.

---

## 8. Résumé d’intention (à destination de l’IA suivante)

> Ce système de questions est une **séquence rituelle d’engagement**.
> Chaque réponse est un pas en avant, chaque pas est irréversible.
> Le joueur déclare qui il pense être —
> le jeu s’en souviendra pour le confronter plus tard.

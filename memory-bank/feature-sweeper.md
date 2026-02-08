# Cahier des Charges : Prototype DarkSweeper (Version Hackathon)

## 1. Vision et Objectif du Prototype

### 1.1. Intention Générale

Ce document définit le cahier des charges pour la création d'un prototype fonctionnel de **DarkSweeper**, un jeu de puzzle roguelite inspiré du Démineur. Conformément à la vision globale du projet, ce prototype doit se concentrer sur l'expérience de **perception, de danger et d'asymétrie de l'information**.

L'objectif n'est pas de livrer une version complète, mais une boucle de jeu minimale et robuste (MVP) pour un hackathon. Le prototype doit incarner la règle fondamentale du monde de DarkSweeper : le joueur n'est pas un héros, mais une **anomalie perceptive**, le seul être capable de sonder un territoire mortel et d'en interpréter les signes au prix d'un effort constant.

### 1.2. Objectifs du Prototype

Le prototype doit livrer une boucle de jeu où le joueur peut :

1.  **Éclairer progressivement** un plateau de jeu plongé dans l'obscurité (Fog of War) par ses clics.
2.  **Payer un coût** pour chaque action d'illumination, matérialisé par une perte de Points de Vie (PV).
3.  **Interagir avec une grille de Démineur classique** sous-jacente en posant des drapeaux et en révélant des cases.
4.  **Gagner ou perdre** en fonction des règles du Démineur et de la gestion de ses PV.

### 1.3. Principes de Design du Prototype

- **L'information a un coût** : Chaque action pour voir le plateau (clic gauche) diminue les PV du joueur. Savoir n'est pas gratuit.
- **Voir n'est pas résoudre** : L'illumination du plateau est un système distinct de la révélation des cases du Démineur. Une case peut être éclairée mais non résolue.
- **La tension avant tout** : Le design doit empêcher toute stratégie consistant à "tout lire puis jouer". Le joueur doit constamment arbitrer entre le besoin de voir et le risque d'épuiser ses ressources.
- **Clarté et lisibilité** : Les mécaniques, bien que punitives, doivent être déterministes, lisibles et sans ambiguïté pour le joueur.

---

## 2. Mécaniques de Jeu Détaillées

### 2.1. Le Plateau de Jeu

Le jeu se déroule sur une grille 2D dont les dimensions (`largeur` et `hauteur`) sont paramétrables.
Chaque case du plateau possède deux couches d'états qui coexistent :

1.  **État Logique (Démineur)** : La structure cachée du puzzle.
2.  **État Visuel (Lumière)** : La perception qu'en a le joueur.

### 2.2. Logique du Démineur

Cette couche gère les règles fondamentales du puzzle. Chaque case possède les propriétés suivantes :

| Propriété | Type | Description |
| :--- | :--- | :--- |
| `hasMine` | booléen | `true` si la case contient une mine, sinon `false`. |
| `adjacentMines` | entier (0-8) | Le nombre de mines dans les 8 cases voisines. |
| `isRevealed` | booléen | `true` si la case a été révélée (au sens Démineur), sinon `false`. Initialement `false`. |
| `isFlagged` | booléen | `true` si le joueur a placé un drapeau sur la case, sinon `false`. Initialement `false`. |

**Règles d'interaction (Démineur) :**

-   **Clic gauche sur une case non flaguée** :
    -   Si `hasMine` est `true` : **Défaite immédiate**.
    -   Sinon : `isRevealed` passe à `true`. Le chiffre du nombre de mines adjacentes devient visible.
    -   Si `adjacentMines` est `0` : une propagation classique (flood fill) révèle automatiquement toutes les cases adjacentes à `0` et leur bordure chiffrée.
-   **Clic droit sur une case non révélée (`isRevealed: false`)** :
    -   Bascule l'état `isFlagged` (`true`/`false`).

**Conditions de fin de partie :**

-   **Victoire** : Toutes les cases non minées (`hasMine: false`) sont révélées (`isRevealed: true`).
-   **Défaite** : Le joueur clique sur une case contenant une mine.

### 2.3. Mécanique de Lumière et Fog of War

Cette couche représente la perception limitée et coûteuse du joueur. Elle est superposée à la logique du Démineur.

Chaque case possède une propriété de lumière :

| Propriété | Type | Description |
| :--- | :--- | :--- |
| `light` | flottant [0.0, 1.0] | Niveau d'illumination de la case. `0.0` pour noir total, `1.0` pour pleine lumière. Initialement `0.0` pour toutes les cases. |

**Coût de la Perception :**

-   Le joueur dispose d'un total de **Points de Vie (PV)** (ex: 100).
-   **Chaque clic gauche**, qu'il révèle une case ou non, **coûte 1 PV**.
-   Si les PV du joueur atteignent 0, il ne peut plus effectuer d'action de clic gauche (l'input est ignoré).

**Modèle d'Illumination :**

-   Un clic gauche sur une case `(cx, cy)` applique un "pinceau" de lumière circulaire sur le plateau.
-   La lumière est **persistante** : elle ne diminue pas avec le temps. Les nouvelles sources de lumière s'ajoutent à la lumière existante en utilisant la fonction `max(ancienne_lumiere, nouvelle_lumiere)` pour chaque case, afin d'éviter une sur-illumination.
-   Le pinceau est défini par deux paramètres :
    -   `radiusFull` (ex: 1) : Le rayon dans lequel la lumière est à `1.0`.
    -   `radiusFalloff` (ex: 3) : La distance supplémentaire sur laquelle la lumière décroît linéairement de `1.0` à `0.0`.

**Lisibilité et Affichage :**

L'affichage des informations de la couche Démineur dépend du niveau de lumière (`light`) de la case :

-   **`light < 0.15`** : Noir quasi total. La grille est à peine visible.
-   **`0.15 <= light < 0.6`** : Pénombre. On peut distinguer les formes, mais les chiffres ou les drapeaux sont flous ou invisibles.
-   **`light >= 0.6`** : Pleine lumière. Les chiffres, les drapeaux et l'état de la case sont parfaitement lisibles.

---

## 3. Interface Utilisateur (UI) et Entrées

### 3.1. Entrées (Inputs)

-   **Clic Gauche** :
    1.  Vérifie si les PV > 0. Si non, ignorer.
    2.  Diminue les PV de 1.
    3.  Applique le pinceau de lumière centré sur la case cliquée.
    4.  Si la case n'est pas flaguée, exécute l'action logique du Démineur (révélation ou défaite).
-   **Clic Droit** :
    -   Si la case n'est pas révélée, bascule l'état du drapeau (`isFlagged`).
    -   Cette action ne coûte pas de PV et n'affecte pas la lumière.
-   **Survol (Hover)** :
    -   Un léger contour doit indiquer la case survolée par la souris, même en faible lumière, pour guider le joueur.

### 3.2. Affichage Tête Haute (HUD)

L'interface doit être minimale et afficher les informations critiques :

-   **Points de Vie actuels** (ex: "PV: 87/100").
-   **Nombre de mines restantes** (calculé par `Mines totales - Nombre de drapeaux posés`).
-   **Message d'état** : Un texte simple pour indiquer la victoire ou la défaite.

---

## 4. Spécifications Techniques et Visuelles

### 4.1. Rendu Visuel

-   L'approche doit être performante. Éviter l'utilisation de multiples lumières dynamiques (point lights).
-   **Approche recommandée** : Utiliser un *overlay* par case ou une texture de rendu unique pour l'ensemble de la grille qui simule le Fog of War. La valeur `light` de chaque case pilote l'opacité ou la couleur de cet overlay.
-   Le style visuel doit être sombre et sobre, en accord avec la direction artistique globale, mais la lisibilité prime.
-   **Accessibilité** : L'information critique (drapeaux, chiffres) ne doit pas être codée uniquement par la couleur (ex: rouge/vert). Utiliser des icônes, des contrastes de valeur et des formes distinctes.

### 4.2. Paramètres de Configuration

Pour faciliter l'itération et l'équilibrage, les variables suivantes doivent être facilement modifiables dans l'éditeur :

-   `gridWidth`, `gridHeight` : Dimensions de la grille.
-   `mineCount` : Nombre total de mines.
-   `HPStart` : PV de départ du joueur.
-   `radiusFull`, `radiusFalloff` : Paramètres du pinceau de lumière.

---

## 5. Livrables Attendus

Le livrable final pour le hackathon est une scène unique et jouable contenant :

1.  La génération d'un plateau de Démineur avec les mécaniques de lumière et de Fog of War fonctionnelles.
2.  Les interactions de base (clic gauche pour illuminer/révéler, clic droit pour flaguer).
3.  L'interface utilisateur (HUD) affichant les PV et les mines restantes.
4.  Les conditions de victoire et de défaite.
5.  Un bouton pour relancer une nouvelle partie.
6.  Un panneau de débogage ou des paramètres exposés pour ajuster les variables de jeu clés.

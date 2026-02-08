# Architecture Unity 6 — Intégration LLM (ChatGPT) avec sortie JSON stricte et GameState persisté

## 1. Contexte et objectif

Ce document décrit l’architecture technique retenue pour intégrer un appel à l’API ChatGPT dans un projet **Unity 6**, avec les objectifs suivants :

* Forcer une **sortie JSON strictement conforme** à un schéma défini.
* Garantir une **séparation claire des responsabilités** entre :

  * la couche technique (API, parsing, validation),
  * la logique métier (GameState, règles),
  * l’intégration Unity (ScriptableObjects, loading).
* Permettre de **transformer une réponse LLM en données Unity chargeables** dans le jeu.
* Rester **testable, maintenable et évolutif** (LLM interchangeable, schémas versionnés).

L’architecture vise un **test technique propre**, mais directement réutilisable en production.

---

## 2. Principes de conception clés

### 2.1 Unity reste l’autorité

Le LLM :

* ne modifie jamais directement le jeu,
* ne fait que **proposer** une mise à jour structurée.

Unity :

* valide,
* applique,
* persiste.

Cela garantit un gameplay **déterministe** et sécurisé.

---

### 2.2 Séparation stricte des couches

Le système est découpé en **4 couches indépendantes** :

| Couche       | Rôle                           | Dépendances     |
| ------------ | ------------------------------ | --------------- |
| Core LLM     | Appels API, JSON strict        | .NET uniquement |
| Domain       | Logique métier, règles         | C# pur          |
| Unity Data   | ScriptableObjects, persistence | UnityEngine     |
| Presentation | Orchestration, UI              | MonoBehaviour   |

➡️ Objectif :

* tester la logique sans Unity,
* faire évoluer le jeu sans toucher au LLM,
* changer de modèle ou d’API sans impacter le gameplay.

---

### 2.3 DTO ≠ Modèle ≠ ScriptableObject

Trois représentations distinctes sont volontairement utilisées :

1. **DTO (Data Transfer Object)**
   → correspond **exactement** au JSON retourné par le LLM.

2. **GameStateModel (runtime)**
   → état du jeu **cohérent, validé**, utilisé par la logique.

3. **GameState ScriptableObject**
   → format Unity **sérialisable et chargeable** dans le jeu.

Ce découplage évite :

* les effets de bord Unity,
* les dépendances LLM dans le gameplay,
* les problèmes de versioning.

---

## 3. Vue d’ensemble de l’architecture

```
[ Player Action / Game Event ]
            ↓
[ Orchestrator (MonoBehaviour) ]
            ↓
[ Core LLM ]
  - Request Builder
  - OpenAI Client
  - JSON Parser
            ↓
[ DTO (JSON strict) ]
            ↓
[ Domain Layer ]
  - GameStateReducer
  - Rules Validator
            ↓
[ GameStateModel ]
            ↓
[ Unity Data Layer ]
  - Mapper
  - ScriptableObject
  - Persistence
            ↓
[ Jeu / UI / Systems ]
```

---

## 4. Core LLM Layer (agnostique Unity)

### 4.1 Rôle

Cette couche est responsable **uniquement** de :

* construire la requête (prompt + schéma),
* appeler l’API OpenAI,
* récupérer une réponse JSON brute,
* parser cette réponse en DTO typé.

Elle **ne connaît pas le jeu**.

---

### 4.2 Choix techniques

* **HTTP** : `UnityWebRequest` (coroutine-based, compatible toutes plateformes Unity).
* **Async** : Coroutines Unity (pas de Task dans la couche transport).
* **JSON** : `Newtonsoft.Json` (package Unity par défaut `com.unity.nuget.newtonsoft-json`).
* **Modèle** : GPT-5 avec **JSON mode** (`response_format: { "type": "json_object" }`).
* **Température basse** (0.2) pour stabilité.

---

### 4.3 JSON strict & schéma

Le LLM reçoit :

* un **system prompt impératif** :

  * sortie JSON uniquement,
  * aucun texte libre,
  * schéma obligatoire.
* un **schéma versionné** embarqué dans le prompt.

En cas d’erreur :

* le système **refuse** la réponse (reject, état inchangé).
* Pas de repair pass — JSON mode de GPT-5 garantit la conformité structurelle.

---

## 5. Domain Layer (logique métier)

### 5.1 Rôle

Cette couche :

* reçoit un DTO validé syntaxiquement,
* applique les règles métier,
* produit un **nouvel état cohérent**.

Elle est **100 % indépendante de Unity**.

---

### 5.2 GameStateModel

Le GameState runtime :

* utilise des types forts (enums, structs),
* garantit les invariants (HP ≥ 0, flags cohérents),
* est la seule source de vérité logique.

---

### 5.3 Approche simplifiée (pas de reducer)

Le LLM renvoie l'**état complet mis à jour** (pas de delta/patch).

Le pipeline :

* parse le JSON en DTO,
* mappe le DTO vers un `GameStateModel`,
* applique le modèle au ScriptableObject.

Pas de reducer fonctionnel. Simplicité prioritaire pour le tech demo.

---

## 6. Unity Data Layer (bridge vers le moteur)

### 6.1 Rôle

Cette couche fait le lien entre :

* un modèle runtime “pur”,
* et des **objets Unity persistables**.

Elle gère :

* ScriptableObjects,
* sérialisation,
* loading.

---

### 6.2 Pourquoi passer par des ScriptableObjects

Les ScriptableObjects permettent :

* d’exposer l’état dans l’Inspector,
* de packager des états dans le build,
* de référencer d’autres assets Unity.

Ils sont adaptés pour :

* GameState initial,
* états générés offline (Editor),
* état courant en mémoire.

---

### 6.3 Mapper explicite

Un **mapper dédié** est utilisé :

* `GameStateModel → GameStateSO`
* (optionnel) `GameStateSO → GameStateModel`

Ce choix évite :

* la sérialisation implicite fragile,
* les dépendances cachées Unity ↔ logique.

---

## 7. Persistence : runtime vs editor

### 7.1 Mode Runtime (jeu lancé)

* Sauvegarde dans `Application.persistentDataPath`.
* Format : JSON (ou binaire si nécessaire).
* Rechargement au démarrage → mapping vers SO en mémoire.

Utilisé pour :

* sauvegardes joueur,
* continuité de session.

---

### 7.2 Mode Editor (génération d’assets)

* Création / mise à jour de ScriptableObjects sur disque.
* Utilisation de `AssetDatabase` (Editor only).
* Versionnable via Git.

Utilisé pour :

* prototypage,
* génération de contenu,
* validation design.

---

## 8. Orchestration (MonoBehaviour)

### 8.1 Rôle

L’orchestrator :

* reçoit une intention (action joueur, trigger),
* lance la requête LLM,
* gère le pipeline complet,
* notifie le jeu (events, UI).

Il ne contient **aucune logique métier**.

---

### 8.2 Pourquoi un orchestrator unique

* Centralise les appels LLM.
* Facilite le debug.
* Permet de throttler / queue / monitorer.

---

## 9. Gestion des erreurs

### 9.1 Types d’erreurs gérées

* Réseau / timeout.
* JSON invalide.
* Schéma invalide.
* Règles métier refusées.

### 9.2 Stratégie

* État du jeu **jamais corrompu**.
* Logs explicites.
* Retry contrôlé.
* Option “repair JSON” possible.

---

## 10. Versioning & évolutivité

* Tous les JSON contiennent `schema_version`.
* Les reducers peuvent gérer plusieurs versions.
* Le LLM peut être remplacé sans toucher au jeu.
* Le GameState peut évoluer sans casser les prompts existants.

---

## 11. Pourquoi cette architecture est adaptée à Unity 6

* Compatible async/await moderne.
* Séparation claire Editor / Runtime.
* Assemblies propres.
* Testable hors moteur.
* Prête pour :

  * Addressables,
  * multi-agents,
  * backend distant,
  * génération offline.

---

## 12. Conclusion

Cette architecture :

* sécurise l’usage d’un LLM en gameplay,
* évite le couplage fragile JSON ↔ Unity,
* permet une montée en complexité progressive,
* est adaptée aussi bien à un **test technique** qu’à une **feature long terme**.

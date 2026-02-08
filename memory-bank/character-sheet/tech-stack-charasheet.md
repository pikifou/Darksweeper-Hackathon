# Tech Stack & Architecture — Questionnaire System (Character Sheet)

*(Unity 6 – Hackathon Scope)*

---

## 1. Technical Objective

The system must:

1. Display **6 fixed questions** with **4 answer buttons** each (A, B, C, D)
2. On click: immediately lock the answer, update scores, trigger a video transition with background crossfade
3. Store:
   * the **exact text** of each question
   * the **exact text** of the chosen answer
   * the **Action / Empathy scores** (cumulative)
4. On completion: fire a **C# event** carrying structured data, ready for ChatGPT prompting
5. No back-office, no dynamic content editing, no "Next" button

Priority: **simplicity, readability, zero magic**

---

## 2. Architecture (3 Layers)

```
[Data] → [Flow] → [Presentation]
```

---

### 2.1 Data Layer (static, declarative)

**Choice: JSON file** (not ScriptableObjects)

All question content lives in a single JSON file (`questions.json`), loaded at runtime.

#### Why JSON instead of ScriptableObjects

* All 6 questions and 24 answers in one readable text file
* Easy to review, diff, and edit
* No Unity Editor dependency for content authoring
* Simpler for a hackathon — one file, one loader

#### JSON Structure

```json
{
  "questions": [
    {
      "id": "Q1",
      "text": "When you are confronted with a clear injustice:",
      "answers": [
        { "id": "A", "text": "You step in immediately, even if you don't fully understand the situation." },
        { "id": "B", "text": "You act if you believe your intervention can genuinely change the outcome." },
        { "id": "C", "text": "You are deeply affected, but you know you cannot always intervene." },
        { "id": "D", "text": "You believe the world is full of injustice, and not all of it is your responsibility." }
      ]
    }
  ]
}
```

#### Global Scoring Rule

Scoring is **not** stored per-answer. It is a global rule applied by the flow controller:

| Answer ID | Action | Empathy |
| --------- | ------ | ------- |
| A         | +1     | +1      |
| B         | +1     | −1      |
| C         | −1     | +1      |
| D         | −1     | −1      |

#### JSON Loader

A static C# class that:
* reads the JSON file from `StreamingAssets` or `Resources`
* deserializes it via `Newtonsoft.Json` into typed C# objects
* returns a list of question data (pure C#, no Unity dependencies)

---

### 2.2 Flow Layer (sequential logic)

**One controller, one state enum.**

#### Flow Controller (MonoBehaviour)

Responsibilities:
* load question data at Start
* track current question index (0 → 5)
* receive answer clicks from the UI
* apply the global scoring rule
* update the runtime data model
* trigger video playback + background crossfade
* fire completion event after the outro video

#### State Enum

```csharp
ShowingQuestion → PlayingTransition → ShowingQuestion → ... → PlayingOutro → Completed
```

No FSM framework, no coroutine chains, no complex state machine.

#### Runtime Data Model

A pure C# class (no Unity dependencies), JSON-serializable:

**`QuestionnaireResult`**

* `List<AnswerRecord>` — ordered list of player answers
* `int actionScore` — cumulative
* `int empathyScore` — cumulative

**`AnswerRecord`**

* `string questionId`
* `string questionText`
* `string answerId`
* `string answerText`
* `int actionDelta`
* `int empathyDelta`

This object is:
* carried by the completion event
* serializable to JSON for the ChatGPT prompt
* usable for debug logging

#### Completion Event

```csharp
public event System.Action<QuestionnaireResult> OnQuestionnaireCompleted;
```

Fired after the outro video ends. Any external system (ChatGPT module, scene loader, etc.) subscribes to this event.

---

### 2.3 Presentation Layer (UI + Video + Background)

#### Canvas Setup (UGUI)

Three layers, back to front:

| Layer | Component | Content |
| ----- | --------- | ------- |
| 1 (back) | `Image` (fullscreen) | Background world image — crossfades between states |
| 2 (mid) | `RawImage` (centered, fixed size) | Character — static image or video via RenderTexture |
| 3 (front) | Panel with Buttons + Text | Question text + 4 answer buttons (A, B, C, D) |

#### Background Images

* 7 sprites: BG1–BG6 (one per question) + BG_Black (final)
* Crossfade implemented with **two overlapping `Image` components** — fade alpha of outgoing, fade in incoming
* Crossfade happens **during** the transition video

#### Character Display (VideoPlayer → RenderTexture → RawImage)

* `VideoPlayer` component renders to a `RenderTexture`
* A centered `RawImage` displays either:
  * a static character sprite (during question), OR
  * the RenderTexture (during video playback)
* 6 static character images: Char1–Char6
* 6 video clips: Trans_1to2 through Trans_5to6 + Trans_Outro
* Video end detected via `VideoPlayer.loopPointReached`

#### Question UI

* Question text: `TextMeshProUGUI`
* 4 buttons: standard UGUI `Button` with `TextMeshProUGUI` labels
* Click any button → immediately notify the flow controller → UI hides
* Buttons are **disabled** while a transition is playing (prevent double-clicks)
* No "Next" button. No selection + confirmation. One click = one commitment.

---

## 3. Data Pipeline (for AI prompting)

### Output JSON Shape

```json
{
  "questions": [
    {
      "questionId": "Q1",
      "questionText": "When you are confronted with a clear injustice:",
      "answerId": "A",
      "answerText": "You step in immediately, even if you don't fully understand the situation.",
      "actionDelta": 1,
      "empathyDelta": 1
    }
  ],
  "finalScores": {
    "action": 2,
    "empathy": -1
  }
}
```

* No computation needed on the receiving end
* All texts are exact (same as displayed to the player)
* Directly injectable into a ChatGPT prompt

---

## 4. Asset Organization

```
Assets/
  Scenes/
    Questionnaire.unity              ← new scene (separate from LLM_TechDemo)
  StreamingAssets/
    questions.json                   ← all question content
  Scripts/
    Questionnaire/
      Data/
        QuestionData.cs              ← typed data classes for JSON deserialization
        QuestionLoader.cs            ← reads and parses questions.json
      Flow/
        QuestionnaireFlowController.cs
        QuestionnaireResult.cs       ← runtime data model (pure C#)
        ScoringRule.cs               ← global A/B/C/D → delta mapping
      Presentation/
        QuestionUIController.cs      ← manages question panel and buttons
        BackgroundController.cs      ← manages background crossfade
        CharacterDisplayController.cs ← manages RawImage + VideoPlayer
  Art/
    Questionnaire/
      Backgrounds/
        BG1.png ... BG6.png, BG_Black.png
      Characters/
        Char1.png ... Char6.png
      Videos/
        Trans_1to2.mp4 ... Trans_5to6.mp4, Trans_Outro.mp4
```

---

## 5. Tech Choices Summary

| Component        | Choice                                              |
| ---------------- | --------------------------------------------------- |
| Engine           | Unity 6                                             |
| UI framework     | UGUI (Canvas) + TextMeshPro                         |
| Question data    | JSON file (`questions.json`) + Newtonsoft.Json       |
| Video playback   | Unity VideoPlayer → RenderTexture → RawImage        |
| Background fade  | Two overlapping UGUI `Image` components, alpha lerp  |
| Scoring          | Global rule in code (not per-answer in data)        |
| Runtime data     | Pure C# class, JSON-serializable                    |
| Completion signal| C# event (`System.Action<QuestionnaireResult>`)     |
| Scene            | `Questionnaire.unity` (separate from LLM_TechDemo)  |
| Testing          | Manual only                                         |

---

## 6. Why This Works for a Hackathon

* Few classes, clear responsibilities
* No heavy systems (no Addressables, no FSM, no ScriptableObject chains)
* One JSON file = all content in one place
* Debug-friendly (log scores, serialize result, inspect canvas)
* Modifiable without breaking the flow
* Compatible with AI prompting pipeline
* **Explainable in 5 minutes to a jury or a dev**

# Questionnaire Base System – Implementation Plan

*(Unity 6 – Hackathon Scope)*

## Scope

This plan covers **only the base questionnaire system**, used at the start of the game to collect player profile data.

Included:

* 6 fixed questions (content defined in `docs/questions.md`)
* 4 answer buttons per question (A, B, C, D) — click = immediate advance
* Layered canvas: background images + character video + question UI
* Background image crossfade during video transitions
* 6 transition videos (5 inter-question + 1 outro)
* Player answer tracking with cumulative Action / Empathy scoring
* Completion event with structured data for ChatGPT prompting

Not included:

* God assignment logic
* Narrative generation
* Gameplay consequences
* Loading screen design (placeholder only)
* Backend or API calls

---

## Visual Architecture

### Canvas Layering (back to front)

1. **Background Image** — fullscreen, represents the world. Crossfades from image N to image N+1 during each transition video.
2. **Character RawImage** — centered, fixed position. Displays either a static character image (during question) or the transition video (via RenderTexture from VideoPlayer).
3. **Question UI** — 4 answer buttons (A, B, C, D) + question text, overlaid on top.

### Assets Required

* **7 background images**: BG1–BG6 (one per question) + BG_Black (final state after outro)
* **6 character images**: Char1–Char6 (one static frame per question)
* **6 transition videos**: Trans_1to2, Trans_2to3, Trans_3to4, Trans_4to5, Trans_5to6, Trans_Outro

### Flow Per Question

1. Background N visible, Character image N visible, Question N buttons appear.
2. Player clicks A/B/C/D → answer locked, score updated, buttons disappear.
3. Transition video N plays on the character RawImage **while** background crossfades from N to N+1.
4. Video ends → character image N+1 shown → Question N+1 buttons appear.
5. After Q6: outro video plays, background fades to black, completion event fires.

---

## Scoring Rule (Global)

The mapping is **uniform across all questions** (defined in `docs/questions.md`):

| Answer | Action | Empathy |
| ------ | ------ | ------- |
| A      | +1     | +1      |
| B      | +1     | −1      |
| C      | −1     | +1      |
| D      | −1     | −1      |

Score range after 6 questions: Action ∈ [−6, +6], Empathy ∈ [−6, +6].

---

## Step 1 – Load Question Data from JSON

### Goal

Create a **single JSON file** containing all 6 questions and their answers, and a loader to parse it at runtime.

### Instructions

* Create a JSON file (`questions.json`) in `Assets/StreamingAssets/` or `Assets/Resources/` containing the 6 questions.
* Each question has:
  * `id` (string, e.g. "Q1")
  * `text` (the question text)
  * `answers` — array of 4 objects, each with:
    * `id` (string: "A", "B", "C", "D")
    * `text` (the answer text)
* The scoring rule (A/B/C/D → deltas) is **not** stored per-answer in the JSON — it is applied globally by the flow controller using the answer ID.
* Create a C# loader class that reads and deserializes this JSON into a typed data structure at runtime.
* Use `Newtonsoft.Json` for deserialization.

### Test

* Enter Play Mode.
* Verify via `Debug.Log`:
  * 6 questions loaded
  * each has exactly 4 answers
  * all texts match `docs/questions.md`

---

## Step 2 – Create Runtime Data Model (Player Answers)

### Goal

Create a **runtime container** that records what the player actually chose.

### Instructions

* Define a plain C# class (no Unity dependencies) to store questionnaire results.
* For each answered question, store:
  * question ID
  * question text
  * selected answer ID (A/B/C/D)
  * selected answer text
  * Action delta applied
  * Empathy delta applied
* Also store:
  * cumulative Action score
  * cumulative Empathy score

This structure must be **JSON-serializable** (via Newtonsoft.Json).

### Test

* Manually populate the structure with mock data.
* Serialize to JSON and log to console.
* Confirm output is clean, readable, and contains no Unity object references.

---

## Step 3 – Build Question Flow Controller

### Goal

Control the **linear progression** of the questionnaire.

### Instructions

* Create a single controller (MonoBehaviour) responsible for:
  * loading question data (Step 1)
  * tracking current question index (0 → 5)
  * receiving answer selections from the UI
  * applying the global scoring rule based on answer ID
  * updating the runtime data model
  * triggering visual transitions (video + background fade)
  * firing a completion event after the outro video
* Use a simple state enum:

```
ShowingQuestion → PlayingTransition → ShowingQuestion → ... → PlayingOutro → Completed
```

* Enforce strict rules:
  * no going back
  * no changing an answer once clicked
  * answer buttons are disabled during transitions

### Test

* Simulate answering questions in sequence via button clicks.
* Confirm:
  * question index increments correctly
  * answers are recorded in order
  * scores accumulate correctly
  * state transitions are clean (no double-triggers)

---

## Step 4 – Implement Question UI (Answer Buttons)

### Goal

Display questions and capture answers via **direct button clicks** — no "Next" button.

### Instructions

* Create a Question UI panel (UGUI Canvas) containing:
  * a TextMeshPro text field for the question
  * 4 Buttons (A, B, C, D), each showing the answer text
* When the player clicks any button:
  * the answer is immediately locked
  * the UI panel hides
  * the flow controller is notified with the selected answer ID
* The UI must **not** calculate scores or manage progression.
* Buttons are disabled while a transition is playing.

### Test

* Display Question 1.
* Click button B → UI disappears, flow controller receives "B".
* Confirm UI can be reused for all 6 questions (populated dynamically).

---

## Step 5 – Set Up Background Images (World Layer)

### Goal

Create the **background layer** that reveals the world progressively.

### Instructions

* Add a fullscreen `Image` component (back of canvas) for the background.
* Prepare 7 background sprites: BG1–BG6 + BG_Black.
* BG1 is shown at start.
* During each transition video, crossfade the background from BG_N to BG_N+1.
* After the outro video, crossfade to BG_Black.
* Crossfade can be done with two overlapping `Image` components, fading alpha.

### Test

* Start the questionnaire.
* Answer Q1 → during transition video, background fades from BG1 to BG2.
* Answer Q5 → background fades from BG5 to BG6.
* Answer Q6 → outro plays, background fades to black.

---

## Step 6 – Set Up Character Display (VideoPlayer + RenderTexture)

### Goal

Display the **character** as a centered element that alternates between static images and transition videos.

### Instructions

* Add a `RawImage` component (centered, fixed size) on the canvas, in front of the background.
* Create a `RenderTexture` and assign it to a `VideoPlayer` component.
* The `RawImage` displays either:
  * a **static character image** (Char_N) when showing a question, OR
  * the **RenderTexture** output when a video is playing.
* Prepare 6 character images (Char1–Char6) and 6 video clips.
* When transitioning:
  1. Switch `RawImage` source to the RenderTexture.
  2. Play the transition video.
  3. On `VideoPlayer.loopPointReached`, switch back to static image Char_N+1.
* For the outro (after Q6): play the outro video, then leave the RawImage blank or hidden.

### Test

* Answer Q1 → static Char1 replaced by transition video → video ends → static Char2 appears.
* Repeat through Q6.
* After Q6 → outro video plays → character area goes blank.

---

## Step 7 – Wire Scoring Into the Flow

### Goal

Accumulate Action / Empathy scores on each answer click.

### Instructions

* When the flow controller receives an answer ID:
  * look up the delta using the global rule (A=+1/+1, B=+1/−1, C=−1/+1, D=−1/−1)
  * add Action delta to cumulative Action score
  * add Empathy delta to cumulative Empathy score
  * store the record in the runtime data model
* Scores are updated **immediately** on click, before the transition starts.

### Test

* Answer all 6 questions with known choices.
* After each answer, log current scores.
* Verify totals match expected values.
* Example: all A's → Action=6, Empathy=6. All D's → Action=−6, Empathy=−6.

---

## Step 8 – Implement Outro and Completion Event

### Goal

End the questionnaire cleanly and **hand off to the next system** via an event.

### Instructions

* After Q6 is answered:
  * hide the question UI (same as any other answer click)
  * play the outro transition video
  * crossfade background to BG_Black
  * when the outro video ends:
    * the flow controller enters `Completed` state
    * fire a **C# event** (e.g. `System.Action<PlayerQuestionnaireResult>`) carrying the full runtime data
  * no further input is accepted
* Any external system (ChatGPT module, scene loader, etc.) can subscribe to this event.

### Test

* Complete all 6 questions.
* Verify:
  * outro video plays
  * background fades to black
  * completion event fires with correct data
  * no additional UI appears, no further interaction possible
  * subscribe a test listener that logs the event payload

---

## Step 9 – Expose Structured Data for AI Prompting

### Goal

Ensure the completion event payload is **directly usable** for ChatGPT prompting.

### Instructions

* The runtime data model (carried by the completion event) must serialize to this JSON shape:

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

* The data must:
  * preserve question order
  * contain exact texts (no truncation)
  * include both per-answer deltas and cumulative totals
  * be serializable via `Newtonsoft.Json` with no extra transformation

### Test

* Complete the questionnaire.
* Serialize the event payload to JSON.
* Log it and verify it matches the expected shape.
* Paste the output into a mock ChatGPT prompt and confirm it is usable as-is.

---

## Step 10 – End-to-End Validation

### Goal

Validate the system as a whole.

### Instructions

* Create the new scene (`Questionnaire.unity`), separate from `LLM_TechDemo`.
* Run the scene from start.
* Complete the questionnaire naturally.
* Observe:
  * background crossfades
  * character video transitions
  * button responsiveness
  * data capture
* Verify the completion event fires with correct, serialized data.

### Test

* Confirm:
  * zero hard-coded question texts in code (all from JSON)
  * no missing answers
  * no desync between visuals and data
  * background and character visuals match the current question
  * data is ready for AI usage without transformation
  * buttons are disabled during transitions (no double-click exploits)

---

## Final Acceptance Criteria

The system is considered complete when:

* The questionnaire feels like a **ritual, not a form**
* All answers are captured verbatim
* Scores are deterministic and reproducible
* Data can be injected directly into a ChatGPT request
* The completion event fires reliably with the full payload
* The system can be explained to a dev in under 10 minutes

---

## Design Intent Reminder

> The player does not fill a questionnaire.
> The player commits, step by step, to a declared identity.
> The system records this commitment to confront it later.

# Dictation Content Mode cho câu ví dụ

## Problem Statement

Người học hiện chỉ có thể nghe nội dung từ vựng trong Dictation. Họ không thể chọn nghe và chép lại câu ví dụ, dù mỗi flashcard đã có câu ví dụ tiếng Anh và nghĩa tiếng Việt. Điều này giới hạn việc luyện nghe trong ngữ cảnh.

Khi bổ sung mode câu ví dụ, nội dung phát âm, đáp án được chấm, feedback và kết quả phiên phải luôn cùng một Dictation Content Mode. Nếu mode câu phát nhầm từ vựng hoặc kết quả dùng cài đặt hiện tại thay vì mode của phiên, trải nghiệm học sẽ không đáng tin cậy.

## Solution

Thêm Dictation Content Mode vào cài đặt nghe chép với hai lựa chọn:

- **Từ vựng**: giữ nguyên hành vi hiện tại và là mặc định.
- **Câu ví dụ**: phát câu ví dụ tiếng Anh và yêu cầu người học nhập lại câu đó.

Mode câu ví dụ chỉ dùng các thẻ có câu ví dụ. Voice, đáp án đúng, feedback và màn hình kết quả đều lấy nội dung từ mode đã được lưu vào phiên. Khi trả lời sai, người học được xem so sánh theo từng từ giữa câu đã nhập và câu đúng, nghĩa tiếng Việt, cùng thao tác nghe lại câu đúng.

## User Stories

1. As a learner, I want to choose vocabulary or example-sentence dictation, so that I can practise listening at the level appropriate to my goal.
2. As an existing learner, I want vocabulary dictation to remain the default, so that my current workflow does not unexpectedly change.
3. As a learner, I want the selected Dictation Content Mode to persist in my study settings, so that I do not need to select it every session.
4. As a learner, I want example-sentence mode to read the English example sentence, so that the audio matches the exercise shown by the selected mode.
5. As a learner, I want every replay and automatic playback to use the same prompt text, so that vocabulary audio can never be played during a sentence exercise.
6. As a learner, I want example-sentence mode to require the English sentence as the answer, so that the exercise is genuine sentence transcription.
7. As a learner, I want answer-mode and synonym settings hidden in example-sentence mode, so that irrelevant options do not create ambiguity.
8. As a learner, I want those vocabulary-only settings restored when I return to vocabulary mode, so that existing Dictation behaviour remains available.
9. As a learner, I want changing the content mode to start a fresh session, so that one session cannot mix vocabulary and sentence prompts.
10. As a learner, I want cards without example sentences excluded from sentence mode, so that every exercise has playable content and a valid answer.
11. As a learner, I want a clear message when a set has no usable example sentences, so that I understand why the session cannot start.
12. As a learner, I want answer checking to ignore letter case, so that capitalization does not create a false failure.
13. As a learner, I want answer checking to ignore leading, trailing, and repeated whitespace, so that harmless spacing does not create a false failure.
14. As a learner, I want answer checking to ignore common punctuation marks, so that punctuation does not distract from listening comprehension.
15. As a learner, I want missing, extra, or incorrect words to remain incorrect, so that sentence transcription still measures the words I heard.
16. As a learner, I want my submitted answer locked after checking, so that feedback always reflects what was actually graded.
17. As a learner, I want to see my submitted sentence beside the correct sentence, so that I can understand the correction in context.
18. As a learner, I want correct, incorrect, missing, and extra words distinguished in the feedback, so that I can locate the exact error.
19. As a learner, I want misspelled words highlighted at word level, so that feedback stays readable on desktop and mobile.
20. As a learner, I want feedback to use labels and visual treatment in addition to color, so that errors remain understandable without relying on color perception.
21. As a learner, I want to see the Vietnamese meaning after a wrong sentence answer, so that I can connect the sentence with its meaning.
22. As a learner, I want to replay the correct sentence from the feedback, so that I can compare the audio with the correction.
23. As a learner, I want “Tôi không biết” to produce the same complete correction feedback, so that skipping still teaches me the answer.
24. As a learner, I want auto-advance to keep working after a correct answer, so that my existing pacing preference is preserved.
25. As a learner, I want sentence answers to update the same flashcard progress as vocabulary answers, so that my study progress remains unified.
26. As a learner, I want a correct sentence to mark the card learned and an incorrect sentence to mark it learning, so that progress reflects Dictation performance.
27. As a learner, I want the result screen to show the correct example sentence, its Vietnamese meaning, and the source vocabulary term for missed sentence prompts, so that review has enough context.
28. As a learner, I want old results to retain the mode used when the session was created, so that changing settings later cannot rewrite session history.
29. As a maintainer, I want existing users, settings, and historical Dictation sessions to default safely to vocabulary mode, so that the migration is backward compatible.
30. As a maintainer, I want one authoritative prompt value to drive playback and grading, so that voice and answer logic cannot drift apart.

## Implementation Decisions

- Add a persistent Dictation Content Mode setting with vocabulary and example-sentence values. Vocabulary is the default for new and existing users.
- Snapshot the selected Dictation Content Mode on each Dictation session. Historical and newly migrated sessions without an explicit value are treated as vocabulary sessions.
- A session uses its stored content mode as the authoritative source for grading and result rendering. Later setting changes do not alter the session.
- Starting Dictation in example-sentence mode filters out cards whose example sentence is blank. When no eligible cards remain, do not create a session and show a sentence-specific message.
- The study payload exposes the vocabulary term, definition, example sentence, example meaning, and one authoritative prompt text appropriate to the session mode.
- All speech actions—initial playback, next-card playback, keyboard replay, and feedback replay—use that authoritative prompt text.
- In vocabulary mode, existing answer-mode and synonym behaviour remains unchanged.
- In example-sentence mode, the expected answer is always the English example sentence; answer-mode and synonym settings are not applied and are hidden in the settings UI.
- Changing Dictation Content Mode saves the setting, reloads Dictation, and starts a new session from the first eligible card. In-progress state from the abandoned session is not counted as completed.
- Sentence normalization is case-insensitive, collapses irrelevant whitespace, and ignores the common punctuation marks period, comma, exclamation mark, question mark, and semicolon.
- Missing, extra, reordered, or misspelled words remain incorrect after normalization.
- The answer-check response provides the submitted answer, correct answer, example meaning where applicable, and a structured word comparison sufficient to render correct, incorrect, missing, and extra tokens.
- Word comparison is word-level rather than character-level. It aligns surrounding correct words so missing and extra words appear at meaningful positions.
- After submission, the input becomes read-only until the learner advances.
- Wrong-answer feedback shows “Bạn đã nhập” and “Câu đúng”, token-level differences, the Vietnamese meaning, “Nghe lại câu đúng”, and “Tiếp tục”.
- Feedback does not rely on color alone; token status must also have accessible text or non-color visual treatment.
- “Tôi không biết” records an incorrect answer and uses the same feedback presentation as any other wrong answer.
- Existing auto-advance behaviour remains unchanged for correct answers.
- Sentence-mode correctness updates the existing per-flashcard progress model; no separate sentence-progress model is introduced.
- Sentence-mode results expose the example sentence, example meaning, and source vocabulary term. Vocabulary-mode results retain the current term, definition, and pronunciation presentation.
- Playback speed and selected browser voice remain supported. This feature changes the spoken text source, not the speech provider.
- No new client dependency is required for word comparison or prompt selection.

## Testing Decisions

- Good tests assert observable Dictation behaviour rather than private helper implementation or exact internal algorithms.
- The primary seam is the existing controller-level Dictation workflow test module using the in-memory database and real study services.
- Controller workflow tests cover mode persistence, eligible-card selection, session mode snapshotting, authoritative prompt content, answer-check responses, progress updates, and mode-aware result data.
- The existing Dictation service test module covers the normalization matrix and word-comparison edge cases where focused examples are clearer than full workflow setup.
- Prior art is the current Dictation controller tests that create a session, submit an answer, complete it, and read the result, plus the existing service tests for filters, synonyms, normalization, authorization, and progress.
- Required sentence-mode cases include case differences, repeated whitespace, ignored punctuation, missing words, extra words, reordered words, misspellings, empty example sentences, “Tôi không biết”, and changing settings after a session was created.
- A browser-level manual verification covers every speech trigger using the sentence prompt, mode-dependent settings visibility, input locking, responsive token feedback, keyboard behaviour, feedback replay, and non-color accessibility cues.
- A browser automation framework is not introduced solely for this feature because the repository has no existing browser-test seam.

## Out of Scope

- Generating, translating, or correcting example sentences.
- Supporting cards with multiple example sentences or paragraph dictation.
- Replacing browser Speech Synthesis with recorded audio or a server-side text-to-speech provider.
- Semantic, fuzzy, pronunciation-based, or AI-assisted grading.
- Character-level spelling diffs.
- A separate progress model for sentence mastery.
- Redesigning the general study history experience.
- Adding a browser-test framework.
- Changing vocabulary-mode answer semantics beyond preserving current behaviour.
- Recovering progress from an abandoned session after switching content mode.

## Further Notes

- Existing flashcards may have blank example sentences because the vocabulary-field migration supplied an empty default; sentence mode must handle those records safely.
- The project glossary names this concept Dictation Content Mode and distinguishes it from the existing Dictation Answer Mode.
- No ADR is required: the decisions are feature-local, visible in the interface, and reasonably reversible.

using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Study;

public class QuizQuestionFactory
{
    public const string InsufficientPoolReason =
        "Cần ít nhất 4 thuật ngữ và 4 định nghĩa khác nhau để tạo câu hỏi trắc nghiệm.";

    private readonly AppDbContext _context;
    private readonly Random _random;

    public QuizQuestionFactory(AppDbContext context)
        : this(context, Random.Shared)
    {
    }

    public QuizQuestionFactory(AppDbContext context, Random random)
    {
        _context = context;
        _random = random;
    }

    public async Task<QuizPoolAvailability> GetAvailabilityAsync(int setId, string userId)
    {
        (List<Flashcard> sameSetCards, List<Flashcard> ownedOtherCards) =
            await LoadCandidatePoolsAsync(setId, userId);
        List<Flashcard> candidateCards = sameSetCards.Concat(ownedOtherCards).ToList();

        int distinctTermCount = CountDistinctValues(candidateCards.Select(card => card.FrontText));
        int distinctDefinitionCount = CountDistinctValues(candidateCards.Select(card => card.BackText));
        bool isAvailable = distinctTermCount >= 4 && distinctDefinitionCount >= 4;

        return new QuizPoolAvailability(
            isAvailable,
            distinctTermCount,
            distinctDefinitionCount,
            isAvailable ? null : InsufficientPoolReason);
    }

    public async Task<List<QuizSessionQuestion>> BuildQuestionsAsync(
        int setId,
        string userId,
        IReadOnlyList<Flashcard> sourceCards,
        IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections = null)
    {
        (List<Flashcard> sameSetCards, List<Flashcard> ownedOtherCards) =
            await LoadCandidatePoolsAsync(setId, userId);
        List<QuizQuestionDirection> directions = BuildDirections(sourceCards, fixedDirections);
        CandidatePools sameSetPools = ProjectCandidatePools(sameSetCards);
        CandidatePools ownedOtherPools = ProjectCandidatePools(ownedOtherCards);
        var cardDirections = sourceCards
            .Select((card, index) => (Card: card, Direction: directions[index]))
            .ToList();
        Shuffle(cardDirections);
        var questions = new List<QuizSessionQuestion>(sourceCards.Count);

        for (int index = 0; index < cardDirections.Count; index++)
        {
            (Flashcard sourceCard, QuizQuestionDirection direction) = cardDirections[index];

            string prompt = GetPrompt(sourceCard, direction);
            string correctAnswer = GetAnswer(sourceCard, direction);
            List<string> choices = BuildChoices(
                correctAnswer,
                direction,
                sameSetPools,
                ownedOtherPools);

            Shuffle(choices);
            int correctChoiceIndex = choices.FindIndex(choice =>
                NormalizeChoice(choice) == NormalizeChoice(correctAnswer));

            questions.Add(new QuizSessionQuestion
            {
                FlashcardId = sourceCard.Id,
                OrderIndex = index,
                Direction = direction,
                PromptText = prompt,
                Choice1Text = choices[0],
                Choice2Text = choices[1],
                Choice3Text = choices[2],
                Choice4Text = choices[3],
                CorrectChoiceIndex = correctChoiceIndex
            });
        }

        return questions;
    }

    private async Task<(List<Flashcard> SameSet, List<Flashcard> OwnedOther)>
        LoadCandidatePoolsAsync(int setId, string userId)
    {
        List<Flashcard> sameSetCards = await _context.Flashcards
            .AsNoTracking()
            .Where(card => card.FlashcardSetId == setId)
            .ToListAsync();

        List<Flashcard> ownedOtherCards = await _context.Flashcards
            .AsNoTracking()
            .Where(card => card.FlashcardSetId != setId)
            .Where(card => _context.FlashcardSets.Any(set =>
                set.Id == card.FlashcardSetId && set.UserId == userId))
            .ToListAsync();

        return (sameSetCards, ownedOtherCards);
    }

    private List<QuizQuestionDirection> BuildDirections(
        IReadOnlyList<Flashcard> sourceCards,
        IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections)
    {
        int fixedTermToDefinitionCount = 0;
        int fixedDefinitionToTermCount = 0;
        var directions = new QuizQuestionDirection?[sourceCards.Count];

        for (int index = 0; index < sourceCards.Count; index++)
        {
            if (fixedDirections == null
                || !fixedDirections.TryGetValue(
                    sourceCards[index].Id,
                    out QuizQuestionDirection fixedDirection))
            {
                continue;
            }

            directions[index] = fixedDirection;
            if (fixedDirection == QuizQuestionDirection.TermToDefinition)
            {
                fixedTermToDefinitionCount++;
            }
            else
            {
                fixedDefinitionToTermCount++;
            }
        }

        int minimumTermToDefinitionCount = fixedTermToDefinitionCount;
        int maximumTermToDefinitionCount =
            sourceCards.Count - fixedDefinitionToTermCount;
        int[] balancedTargets =
        {
            sourceCards.Count / 2,
            (sourceCards.Count + 1) / 2
        };
        int[] feasibleTargets = balancedTargets
            .Distinct()
            .Where(target => target >= minimumTermToDefinitionCount
                && target <= maximumTermToDefinitionCount)
            .ToArray();

        int targetTermToDefinitionCount = feasibleTargets.Length > 0
            ? feasibleTargets[_random.Next(feasibleTargets.Length)]
            : Math.Clamp(
                sourceCards.Count / 2,
                minimumTermToDefinitionCount,
                maximumTermToDefinitionCount);

        int unfixedTermToDefinitionCount =
            targetTermToDefinitionCount - fixedTermToDefinitionCount;
        int unfixedCount = sourceCards.Count
            - fixedTermToDefinitionCount
            - fixedDefinitionToTermCount;
        var unfixedDirections = new List<QuizQuestionDirection>(unfixedCount);
        unfixedDirections.AddRange(Enumerable.Repeat(
            QuizQuestionDirection.TermToDefinition,
            unfixedTermToDefinitionCount));
        unfixedDirections.AddRange(Enumerable.Repeat(
            QuizQuestionDirection.DefinitionToTerm,
            unfixedCount - unfixedTermToDefinitionCount));
        Shuffle(unfixedDirections);

        int unfixedIndex = 0;
        for (int index = 0; index < directions.Length; index++)
        {
            if (!directions[index].HasValue)
            {
                directions[index] = unfixedDirections[unfixedIndex++];
            }
        }

        return directions.Select(direction => direction!.Value).ToList();
    }

    private List<string> BuildChoices(
        string correctAnswer,
        QuizQuestionDirection direction,
        CandidatePools sameSetPools,
        CandidatePools ownedOtherPools)
    {
        string normalizedCorrectAnswer = NormalizeChoice(correctAnswer);
        if (normalizedCorrectAnswer.Length == 0)
        {
            throw new QuizUnavailableException(InsufficientPoolReason);
        }

        var choices = new List<string> { correctAnswer };
        var usedValues = new HashSet<string>(StringComparer.Ordinal)
        {
            normalizedCorrectAnswer
        };

        AddDistinctDistractors(choices, usedValues, sameSetPools.For(direction));
        if (choices.Count < 4)
        {
            AddDistinctDistractors(choices, usedValues, ownedOtherPools.For(direction));
        }

        if (choices.Count < 4)
        {
            throw new QuizUnavailableException(InsufficientPoolReason);
        }

        return choices;
    }

    private void AddDistinctDistractors(
        List<string> choices,
        HashSet<string> usedValues,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        int needed = 4 - choices.Count;
        if (needed == 0 || candidates.Count == 0)
        {
            return;
        }

        var visitedIndices = new HashSet<int>();
        int maxRandomAttempts = Math.Min(candidates.Count, Math.Max(8, needed * 4));
        for (int attempt = 0; attempt < maxRandomAttempts && choices.Count < 4; attempt++)
        {
            int candidateIndex = _random.Next(candidates.Count);
            if (visitedIndices.Add(candidateIndex))
            {
                ChoiceCandidate candidate = candidates[candidateIndex];
                if (usedValues.Add(candidate.Normalized))
                {
                    choices.Add(candidate.Value);
                }
            }
        }

        if (choices.Count == 4)
        {
            return;
        }

        // Rare fallback for pathological Random output or a pool dominated by exclusions.
        foreach (ChoiceCandidate candidate in candidates)
        {
            if (usedValues.Add(candidate.Normalized))
            {
                choices.Add(candidate.Value);
            }

            if (choices.Count == 4)
            {
                return;
            }
        }
    }

    private static CandidatePools ProjectCandidatePools(IReadOnlyList<Flashcard> cards)
    {
        return new CandidatePools(
            ProjectCandidates(cards.Select(card => card.BackText)),
            ProjectCandidates(cards.Select(card => card.FrontText)));
    }

    private static IReadOnlyList<ChoiceCandidate> ProjectCandidates(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<ChoiceCandidate>();
        foreach (string value in values)
        {
            string trimmed = value.Trim();
            string normalized = NormalizeChoice(trimmed);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                candidates.Add(new ChoiceCandidate(trimmed, normalized));
            }
        }

        return candidates;
    }

    private static int CountDistinctValues(IEnumerable<string> values)
    {
        return values
            .Select(NormalizeChoice)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static string GetPrompt(Flashcard card, QuizQuestionDirection direction)
    {
        return direction == QuizQuestionDirection.TermToDefinition
            ? card.FrontText
            : card.BackText;
    }

    private static string GetAnswer(Flashcard card, QuizQuestionDirection direction)
    {
        return direction == QuizQuestionDirection.TermToDefinition
            ? card.BackText
            : card.FrontText;
    }

    private static string NormalizeChoice(string value) => value.Trim().ToUpperInvariant();

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private sealed record ChoiceCandidate(string Value, string Normalized);

    private sealed record CandidatePools(
        IReadOnlyList<ChoiceCandidate> Definitions,
        IReadOnlyList<ChoiceCandidate> Terms)
    {
        public IReadOnlyList<ChoiceCandidate> For(QuizQuestionDirection direction) =>
            direction == QuizQuestionDirection.TermToDefinition ? Definitions : Terms;
    }
}

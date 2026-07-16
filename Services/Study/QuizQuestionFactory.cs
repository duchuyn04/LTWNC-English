using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Study;

public class QuizQuestionFactory
{
    public const string InsufficientPoolReason =
        "Cần ít nhất 4 thuật ngữ và 4 định nghĩa khác nhau để tạo câu hỏi trắc nghiệm.";

    private readonly AppDbContext _context;

    public QuizQuestionFactory(AppDbContext context)
    {
        _context = context;
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
        var questions = new List<QuizSessionQuestion>(sourceCards.Count);

        for (int index = 0; index < sourceCards.Count; index++)
        {
            Flashcard sourceCard = sourceCards[index];
            QuizQuestionDirection direction = directions[index];

            string prompt = GetPrompt(sourceCard, direction);
            string correctAnswer = GetAnswer(sourceCard, direction);
            List<string> choices = BuildChoices(
                correctAnswer,
                direction,
                sameSetCards,
                ownedOtherCards);

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

    private static List<QuizQuestionDirection> BuildDirections(
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
            ? feasibleTargets[Random.Shared.Next(feasibleTargets.Length)]
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

    private static List<string> BuildChoices(
        string correctAnswer,
        QuizQuestionDirection direction,
        IReadOnlyList<Flashcard> sameSetCards,
        IReadOnlyList<Flashcard> ownedOtherCards)
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

        AddDistinctDistractors(choices, usedValues, sameSetCards, direction);
        if (choices.Count < 4)
        {
            AddDistinctDistractors(choices, usedValues, ownedOtherCards, direction);
        }

        if (choices.Count < 4)
        {
            throw new QuizUnavailableException(InsufficientPoolReason);
        }

        return choices;
    }

    private static void AddDistinctDistractors(
        List<string> choices,
        HashSet<string> usedValues,
        IReadOnlyList<Flashcard> cards,
        QuizQuestionDirection direction)
    {
        var candidates = cards
            .Select(card => GetAnswer(card, direction))
            .ToList();
        Shuffle(candidates);

        foreach (string candidate in candidates)
        {
            string normalizedCandidate = NormalizeChoice(candidate);
            if (normalizedCandidate.Length > 0 && usedValues.Add(normalizedCandidate))
            {
                choices.Add(candidate);
            }

            if (choices.Count == 4)
            {
                return;
            }
        }
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

    private static void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}

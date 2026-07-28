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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_random` để các phương thức khác sử dụng.
        _random = random;
    }

    public async Task<QuizPoolAvailability> GetAvailabilityAsync(int setId, string userId)
    {
        // 1. Cập nhật `(List<Flashcard> sameSetCards, List<Flashcard> ownedOtherCards)` bằng giá trị mới.
        (List<Flashcard> sameSetCards, List<Flashcard> ownedOtherCards) =
            await LoadCandidatePoolsAsync(setId, userId);
        // 2. Gọi `ToList` và lưu kết quả vào `candidateCards`.
        List<Flashcard> candidateCards = sameSetCards.Concat(ownedOtherCards).ToList();

        // 3. Gọi `CountDistinctValues` và lưu kết quả vào `distinctTermCount`.
        int distinctTermCount = CountDistinctValues(candidateCards.Select(card => card.FrontText));
        // 4. Gọi `CountDistinctValues` và lưu kết quả vào `distinctDefinitionCount`.
        int distinctDefinitionCount = CountDistinctValues(candidateCards.Select(card => card.BackText));
        // 5. Tính giá trị và lưu vào `isAvailable` để dùng ở bước tiếp theo.
        bool isAvailable = distinctTermCount >= 4 && distinctDefinitionCount >= 4;

        // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Cập nhật `(List<Flashcard> sameSetCards, List<Flashcard> ownedOtherCards)` bằng giá trị mới.
        (List<Flashcard> sameSetCards, List<Flashcard> ownedOtherCards) =
            await LoadCandidatePoolsAsync(setId, userId);
        // 2. Gọi `BuildDirections` và lưu kết quả vào `directions`.
        List<QuizQuestionDirection> directions = BuildDirections(sourceCards, fixedDirections);
        // 3. Gọi `ProjectCandidatePools` và lưu kết quả vào `sameSetPools`.
        CandidatePools sameSetPools = ProjectCandidatePools(sameSetCards);
        // 4. Gọi `ProjectCandidatePools` và lưu kết quả vào `ownedOtherPools`.
        CandidatePools ownedOtherPools = ProjectCandidatePools(ownedOtherCards);
        // 5. Gọi `ToList` và lưu kết quả vào `cardDirections`.
        var cardDirections = sourceCards
            .Select((card, index) => (Card: card, Direction: directions[index]))
            .ToList();
        // 6. Gọi `Shuffle` để thực hiện bước nghiệp vụ này.
        Shuffle(cardDirections);
        // 7. Khởi tạo `questions` với dữ liệu ban đầu cần thiết.
        var questions = new List<QuizSessionQuestion>(sourceCards.Count);

        // 8. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int index = 0; index < cardDirections.Count; index++)
        {
            // 9. Cập nhật `(Flashcard sourceCard, QuizQuestionDirection direction)` bằng giá trị mới.
            (Flashcard sourceCard, QuizQuestionDirection direction) = cardDirections[index];

            // 10. Gọi `GetPrompt` và lưu kết quả vào `prompt`.
            string prompt = GetPrompt(sourceCard, direction);
            // 11. Gọi `GetAnswer` và lưu kết quả vào `correctAnswer`.
            string correctAnswer = GetAnswer(sourceCard, direction);
            // 12. Gọi `BuildChoices` và lưu kết quả vào `choices`.
            List<string> choices = BuildChoices(
                correctAnswer,
                direction,
                sameSetPools,
                ownedOtherPools);

            // 13. Gọi `Shuffle` để thực hiện bước nghiệp vụ này.
            Shuffle(choices);
            // 14. Gọi `FindIndex` và lưu kết quả vào `correctChoiceIndex`.
            int correctChoiceIndex = choices.FindIndex(choice =>
                NormalizeChoice(choice) == NormalizeChoice(correctAnswer));

            // 15. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 16. Trả `questions` cho nơi gọi.
        return questions;
    }

    private async Task<(List<Flashcard> SameSet, List<Flashcard> OwnedOther)>
        LoadCandidatePoolsAsync(int setId, string userId)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `sameSetCards`.
        List<Flashcard> sameSetCards = await _context.Flashcards
            .AsNoTracking()
            .Where(card => card.FlashcardSetId == setId)
            .ToListAsync();

        // 2. Gọi `ToListAsync` và lưu kết quả vào `ownedOtherCards`.
        List<Flashcard> ownedOtherCards = await _context.Flashcards
            .AsNoTracking()
            .Where(card => card.FlashcardSetId != setId)
            .Where(card => _context.FlashcardSets.Any(set =>
                set.Id == card.FlashcardSetId && set.UserId == userId))
            .ToListAsync();

        // 3. Trả `(sameSetCards, ownedOtherCards)` cho nơi gọi.
        return (sameSetCards, ownedOtherCards);
    }

    private List<QuizQuestionDirection> BuildDirections(
        IReadOnlyList<Flashcard> sourceCards,
        IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections)
    {
        // 1. Tính giá trị và lưu vào `fixedTermToDefinitionCount` để dùng ở bước tiếp theo.
        int fixedTermToDefinitionCount = 0;
        // 2. Tính giá trị và lưu vào `fixedDefinitionToTermCount` để dùng ở bước tiếp theo.
        int fixedDefinitionToTermCount = 0;
        // 3. Khởi tạo `directions` với dữ liệu ban đầu cần thiết.
        var directions = new QuizQuestionDirection?[sourceCards.Count];

        // 4. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int index = 0; index < sourceCards.Count; index++)
        {
            // 5. Kiểm tra `fixedDirections == null || !fixedDirections.TryGetValue( sourceCard...` để chọn nhánh xử lý phù hợp.
            if (fixedDirections == null
                || !fixedDirections.TryGetValue(
                    sourceCards[index].Id,
                    out QuizQuestionDirection fixedDirection))
            {
                // 6. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 7. Cập nhật `directions[index]` bằng giá trị mới.
            directions[index] = fixedDirection;
            // 8. Kiểm tra `fixedDirection == QuizQuestionDirection.TermToDefinition` để chọn nhánh xử lý phù hợp.
            if (fixedDirection == QuizQuestionDirection.TermToDefinition)
            {
                // 9. Cập nhật bộ đếm hoặc trạng thái `fixedTermToDefinitionCount`.
                fixedTermToDefinitionCount++;
            }
            else
            {
                // 10. Cập nhật bộ đếm hoặc trạng thái `fixedDefinitionToTermCount`.
                fixedDefinitionToTermCount++;
            }
        }

        // 11. Tính giá trị và lưu vào `minimumTermToDefinitionCount` để dùng ở bước tiếp theo.
        int minimumTermToDefinitionCount = fixedTermToDefinitionCount;
        // 12. Tính giá trị và lưu vào `maximumTermToDefinitionCount` để dùng ở bước tiếp theo.
        int maximumTermToDefinitionCount =
            sourceCards.Count - fixedDefinitionToTermCount;
        // 13. Tính giá trị và lưu vào `balancedTargets` để dùng ở bước tiếp theo.
        int[] balancedTargets =
        {
            sourceCards.Count / 2,
            (sourceCards.Count + 1) / 2
        };
        // 14. Gọi `ToArray` và lưu kết quả vào `feasibleTargets`.
        int[] feasibleTargets = balancedTargets
            .Distinct()
            .Where(target => target >= minimumTermToDefinitionCount
                && target <= maximumTermToDefinitionCount)
            .ToArray();

        // 15. Tính giá trị và lưu vào `targetTermToDefinitionCount` để dùng ở bước tiếp theo.
        int targetTermToDefinitionCount = feasibleTargets.Length > 0
            ? feasibleTargets[_random.Next(feasibleTargets.Length)]
            : Math.Clamp(
                sourceCards.Count / 2,
                minimumTermToDefinitionCount,
                maximumTermToDefinitionCount);

        // 16. Tính giá trị và lưu vào `unfixedTermToDefinitionCount` để dùng ở bước tiếp theo.
        int unfixedTermToDefinitionCount =
            targetTermToDefinitionCount - fixedTermToDefinitionCount;
        // 17. Tính giá trị và lưu vào `unfixedCount` để dùng ở bước tiếp theo.
        int unfixedCount = sourceCards.Count
            - fixedTermToDefinitionCount
            - fixedDefinitionToTermCount;
        // 18. Khởi tạo `unfixedDirections` với dữ liệu ban đầu cần thiết.
        var unfixedDirections = new List<QuizQuestionDirection>(unfixedCount);
        // 19. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        unfixedDirections.AddRange(Enumerable.Repeat(
            QuizQuestionDirection.TermToDefinition,
            unfixedTermToDefinitionCount));
        // 20. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        unfixedDirections.AddRange(Enumerable.Repeat(
            QuizQuestionDirection.DefinitionToTerm,
            unfixedCount - unfixedTermToDefinitionCount));
        // 21. Gọi `Shuffle` để thực hiện bước nghiệp vụ này.
        Shuffle(unfixedDirections);

        // 22. Tính giá trị và lưu vào `unfixedIndex` để dùng ở bước tiếp theo.
        int unfixedIndex = 0;
        // 23. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int index = 0; index < directions.Length; index++)
        {
            // 24. Kiểm tra `!directions[index].HasValue` để chọn nhánh xử lý phù hợp.
            if (!directions[index].HasValue)
            {
                // 25. Cập nhật `directions[index]` bằng giá trị mới.
                directions[index] = unfixedDirections[unfixedIndex++];
            }
        }

        // 26. Trả kết quả từ `ToList` cho nơi gọi.
        return directions.Select(direction => direction!.Value).ToList();
    }

    private List<string> BuildChoices(
        string correctAnswer,
        QuizQuestionDirection direction,
        CandidatePools sameSetPools,
        CandidatePools ownedOtherPools)
    {
        // 1. Gọi `NormalizeChoice` và lưu kết quả vào `normalizedCorrectAnswer`.
        string normalizedCorrectAnswer = NormalizeChoice(correctAnswer);
        // 2. Kiểm tra `normalizedCorrectAnswer.Length == 0` để chọn nhánh xử lý phù hợp.
        if (normalizedCorrectAnswer.Length == 0)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new QuizUnavailableException(InsufficientPoolReason)`.
            throw new QuizUnavailableException(InsufficientPoolReason);
        }

        // 4. Khởi tạo `choices` với dữ liệu ban đầu cần thiết.
        var choices = new List<string> { correctAnswer };
        // 5. Khởi tạo `usedValues` với dữ liệu ban đầu cần thiết.
        var usedValues = new HashSet<string>(StringComparer.Ordinal)
        {
            normalizedCorrectAnswer
        };

        // 6. Gọi `AddDistinctDistractors` để thực hiện bước nghiệp vụ này.
        AddDistinctDistractors(choices, usedValues, sameSetPools.For(direction));
        // 7. Kiểm tra `choices.Count < 4` để chọn nhánh xử lý phù hợp.
        if (choices.Count < 4)
        {
            // 8. Gọi `AddDistinctDistractors` để thực hiện bước nghiệp vụ này.
            AddDistinctDistractors(choices, usedValues, ownedOtherPools.For(direction));
        }

        // 9. Kiểm tra `choices.Count < 4` để chọn nhánh xử lý phù hợp.
        if (choices.Count < 4)
        {
            // 10. Dừng xử lý và phát sinh lỗi `new QuizUnavailableException(InsufficientPoolReason)`.
            throw new QuizUnavailableException(InsufficientPoolReason);
        }

        // 11. Trả `choices` cho nơi gọi.
        return choices;
    }

    private void AddDistinctDistractors(
        List<string> choices,
        HashSet<string> usedValues,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        // 1. Tính giá trị và lưu vào `needed` để dùng ở bước tiếp theo.
        int needed = 4 - choices.Count;
        // 2. Kiểm tra `needed == 0 || candidates.Count == 0` để chọn nhánh xử lý phù hợp.
        if (needed == 0 || candidates.Count == 0)
        {
            // 3. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 4. Khởi tạo `visitedIndices` với dữ liệu ban đầu cần thiết.
        var visitedIndices = new HashSet<int>();
        // 5. Gọi `Min` và lưu kết quả vào `maxRandomAttempts`.
        int maxRandomAttempts = Math.Min(candidates.Count, Math.Max(8, needed * 4));
        // 6. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int attempt = 0; attempt < maxRandomAttempts && choices.Count < 4; attempt++)
        {
            // 7. Gọi `Next` và lưu kết quả vào `candidateIndex`.
            int candidateIndex = _random.Next(candidates.Count);
            // 8. Kiểm tra `visitedIndices.Add(candidateIndex)` để chọn nhánh xử lý phù hợp.
            if (visitedIndices.Add(candidateIndex))
            {
                // 9. Tính giá trị và lưu vào `candidate` để dùng ở bước tiếp theo.
                ChoiceCandidate candidate = candidates[candidateIndex];
                // 10. Kiểm tra `usedValues.Add(candidate.Normalized)` để chọn nhánh xử lý phù hợp.
                if (usedValues.Add(candidate.Normalized))
                {
                    // 11. Gọi `Add` để thực hiện bước nghiệp vụ này.
                    choices.Add(candidate.Value);
                }
            }
        }

        // 12. Kiểm tra `choices.Count == 4` để chọn nhánh xử lý phù hợp.
        if (choices.Count == 4)
        {
            // 13. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // Rare fallback for pathological Random output or a pool dominated by exclusions.
        // 14. Duyệt từng `candidate` trong `candidates` để xử lý lần lượt.
        foreach (ChoiceCandidate candidate in candidates)
        {
            // 15. Kiểm tra `usedValues.Add(candidate.Normalized)` để chọn nhánh xử lý phù hợp.
            if (usedValues.Add(candidate.Normalized))
            {
                // 16. Gọi `Add` để thực hiện bước nghiệp vụ này.
                choices.Add(candidate.Value);
            }

            // 17. Kiểm tra `choices.Count == 4` để chọn nhánh xử lý phù hợp.
            if (choices.Count == 4)
            {
                // 18. Kết thúc phương thức sau khi hoàn tất xử lý.
                return;
            }
        }
    }

    private static CandidatePools ProjectCandidatePools(IReadOnlyList<Flashcard> cards)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new CandidatePools(
            ProjectCandidates(cards.Select(card => card.BackText)),
            ProjectCandidates(cards.Select(card => card.FrontText)));
    }

    private static IReadOnlyList<ChoiceCandidate> ProjectCandidates(IEnumerable<string> values)
    {
        // 1. Khởi tạo `seen` với dữ liệu ban đầu cần thiết.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // 2. Khởi tạo `candidates` với dữ liệu ban đầu cần thiết.
        var candidates = new List<ChoiceCandidate>();
        // 3. Duyệt từng `value` trong `values` để xử lý lần lượt.
        foreach (string value in values)
        {
            // 4. Gọi `Trim` và lưu kết quả vào `trimmed`.
            string trimmed = value.Trim();
            // 5. Gọi `NormalizeChoice` và lưu kết quả vào `normalized`.
            string normalized = NormalizeChoice(trimmed);
            // 6. Kiểm tra `normalized.Length > 0 && seen.Add(normalized)` để chọn nhánh xử lý phù hợp.
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                // 7. Gọi `Add` để thực hiện bước nghiệp vụ này.
                candidates.Add(new ChoiceCandidate(trimmed, normalized));
            }
        }

        // 8. Trả `candidates` cho nơi gọi.
        return candidates;
    }

    private static int CountDistinctValues(IEnumerable<string> values)
    {
        // 1. Trả kết quả từ `Count` cho nơi gọi.
        return values
            .Select(NormalizeChoice)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static string GetPrompt(Flashcard card, QuizQuestionDirection direction)
    {
        // 1. Trả `direction == QuizQuestionDirection.TermToDefinition ? card.FrontTex...` cho nơi gọi.
        return direction == QuizQuestionDirection.TermToDefinition
            ? card.FrontText
            : card.BackText;
    }

    private static string GetAnswer(Flashcard card, QuizQuestionDirection direction)
    {
        // 1. Trả `direction == QuizQuestionDirection.TermToDefinition ? card.BackText...` cho nơi gọi.
        return direction == QuizQuestionDirection.TermToDefinition
            ? card.BackText
            : card.FrontText;
    }

    private static string NormalizeChoice(string value)
    {
        // 1. Trả kết quả từ `ToUpperInvariant` cho nơi gọi.
        return value.Trim().ToUpperInvariant();
    }

    private void Shuffle<T>(IList<T> values)
    {
        // 1. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int index = values.Count - 1; index > 0; index--)
        {
            // 2. Gọi `Next` và lưu kết quả vào `swapIndex`.
            int swapIndex = _random.Next(index + 1);
            // 3. Cập nhật `(values[index], values[swapIndex])` bằng giá trị mới.
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private sealed record ChoiceCandidate(string Value, string Normalized);

    private sealed record CandidatePools(
        IReadOnlyList<ChoiceCandidate> Definitions,
        IReadOnlyList<ChoiceCandidate> Terms)
    {
        public IReadOnlyList<ChoiceCandidate> For(QuizQuestionDirection direction)
        {
            // 1. Trả `direction == QuizQuestionDirection.TermToDefinition ? Definitions :...` cho nơi gọi.
            return direction == QuizQuestionDirection.TermToDefinition ? Definitions : Terms;
        }
    }
}

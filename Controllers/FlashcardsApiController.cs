using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

[Authorize]
// Route cố định: [controller] sẽ resolve thành "FlashcardsApi" (sai so với JS gọi /api/flashcards).
[Route("api/flashcards")]
[ApiController]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class FlashcardsApiController : ControllerBase
{
    private readonly IFlashcardSetService _setService;
    private readonly ICurrentUser _currentUser;

    public FlashcardsApiController(IFlashcardSetService setService, ICurrentUser currentUser)
    {
        _setService = setService;
        _currentUser = currentUser;
    }

    private string? UserId => _currentUser.UserId;

    [HttpPost("flashcard-sets")]
    public async Task<IActionResult> CreateSet(CreateSetRequest request)
    {
        if (UserId == null) return Challenge();

        var set = await _setService.CreateSetAsync(
            request.Title,
            request.Description,
            request.IsPublic,
            UserId);

        return CreatedAtAction(nameof(GetSet), new { id = set.Id }, MapToResponse(set));
    }

    [HttpGet("flashcard-sets/{id}")]
    public async Task<IActionResult> GetSet(int id)
    {
        if (UserId == null) return Challenge();

        var set = await _setService.GetOwnedSetAsync(id, UserId);
        if (set == null) return NotFound();

        return Ok(MapToResponse(set));
    }

    [HttpPut("flashcard-sets/{id}")]
    public async Task<IActionResult> UpdateSet(int id, UpdateSetRequest request)
    {
        if (UserId == null) return Challenge();

        await _setService.UpdateSetAsync(
            id,
            request.Title,
            request.Description,
            request.IsPublic,
            UserId);

        return NoContent();
    }

    [HttpPost("flashcards")]
    public async Task<IActionResult> CreateCard(CreateCardRequest request)
    {
        if (UserId == null) return Challenge();

        var card = await _setService.AddCardAsync(
            request.SetId,
            request.FrontText,
            request.BackText,
            request.Pronunciation,
            request.PartOfSpeech,
            request.ExampleSentence,
            request.ExampleMeaning,
            request.Synonyms,
            request.ImageUrl,
            null,
            request.IsStarred,
            UserId);

        return CreatedAtAction(nameof(GetCard), new { id = card.Id }, MapToResponse(card));
    }

    [HttpGet("flashcards/{id}")]
    public async Task<IActionResult> GetCard(int id)
    {
        if (UserId == null) return Challenge();

        var card = await _setService.GetCardAsync(id, UserId);
        if (card == null) return NotFound();

        return Ok(MapToResponse(card));
    }

    [HttpPut("flashcards/{id}")]
    public async Task<IActionResult> UpdateCard(int id, UpdateCardRequest request)
    {
        if (UserId == null) return Challenge();

        await _setService.UpdateCardAsync(
            id,
            request.FrontText,
            request.BackText,
            request.Pronunciation,
            request.PartOfSpeech,
            request.ExampleSentence,
            request.ExampleMeaning,
            request.Synonyms,
            request.ImageUrl,
            null,
            request.RemoveUploadedImage,
            request.IsStarred,
            UserId);

        return NoContent();
    }

    [HttpDelete("flashcards/{id}")]
    public async Task<IActionResult> DeleteCard(int id)
    {
        if (UserId == null) return Challenge();

        await _setService.DeleteCardAsync(id, UserId);
        return NoContent();
    }

    [HttpPost("flashcards/{id}/star")]
    public async Task<IActionResult> ToggleStar(int id)
    {
        if (UserId == null) return Challenge();

        bool isStarred = await _setService.ToggleStarAsync(id, UserId);
        return Ok(new { isStarred });
    }

    [HttpPost("flashcards/batch")]
    public async Task<IActionResult> BatchImport(BatchImportRequest request)
    {
        if (UserId == null) return Challenge();

        var items = request.Cards.Select(card => new BatchImportCardItem
        {
            FrontText = card.FrontText,
            BackText = card.BackText,
            Pronunciation = card.Pronunciation,
            PartOfSpeech = card.PartOfSpeech,
            ExampleSentence = card.ExampleSentence,
            ExampleMeaning = card.ExampleMeaning,
            Synonyms = card.Synonyms,
            ImageUrl = card.ImageUrl,
            IsStarred = card.IsStarred
        }).ToList();

        var created = await _setService.BatchImportCardsAsync(
            request.SetId,
            items,
            request.ReplaceAll,
            UserId);

        return Ok(created.Select(MapToResponse).ToList());
    }

    [HttpPost("flashcards/reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request)
    {
        if (UserId == null) return Challenge();

        await _setService.ReorderCardsAsync(request.SetId, request.OrderedCardIds, UserId);
        return NoContent();
    }

    private static SetResponse MapToResponse(FlashcardSet set)
    {
        return new SetResponse
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            CreatedAt = set.CreatedAt,
            UpdatedAt = set.UpdatedAt
        };
    }

    private static CardResponse MapToResponse(Flashcard card)
    {
        return new CardResponse
        {
            Id = card.Id,
            SetId = card.FlashcardSetId,
            FrontText = card.FrontText,
            BackText = card.BackText,
            Pronunciation = card.Pronunciation,
            PartOfSpeech = card.PartOfSpeech,
            ExampleSentence = card.ExampleSentence,
            ExampleMeaning = card.ExampleMeaning,
            Synonyms = card.Synonyms,
            ImageUrl = card.ImageUrl,
            UploadedImagePath = card.UploadedImagePath,
            IsStarred = card.IsStarred,
            OrderIndex = card.OrderIndex
        };
    }
}

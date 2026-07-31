using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// API dành cho trình chỉnh sửa bộ thẻ; mọi thao tác yêu cầu đăng nhập và antiforgery token.
[Authorize]
// Dùng route cố định vì giao diện JavaScript gọi trực tiếp đường dẫn /api/flashcards.
[Route("api/flashcards")]
[ApiController]
[AutoValidateAntiforgeryToken]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class FlashcardsApiController : ControllerBase
{
    // Service xử lý bộ thẻ và thông tin người dùng hiện tại.
    private readonly IFlashcardSetService _setService;
    private readonly ICurrentUser _currentUser;

    // Nhận các service cần dùng qua dependency injection.
    public FlashcardsApiController(IFlashcardSetService setService, ICurrentUser currentUser)
    {
        // 1. Lưu service bộ thẻ và thông tin người dùng cho các API action.
        _setService = setService;
        _currentUser = currentUser;
    }

    // Lấy nhanh mã người dùng từ cookie đăng nhập cho các action API.
    private string? UserId => _currentUser.UserId;

    // Tạo bộ thẻ mới và trả về HTTP 201 cùng dữ liệu vừa tạo.
    [HttpPost("flashcard-sets")]
    public async Task<IActionResult> CreateSet(CreateSetRequest request)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Tạo bộ thẻ từ dữ liệu request.
        // 3. Trả HTTP 201 cùng đường dẫn và dữ liệu bộ thẻ vừa tạo.
        if (UserId == null) return Challenge();

        var set = await _setService.CreateSetAsync(
            request.Title,
            request.Description,
            request.IsPublic,
            UserId,
            request.NewCardQuota,
            request.ReviewPaused);

        return CreatedAtAction(nameof(GetSet), new { id = set.Id }, MapToResponse(set));
    }

    // Lấy một bộ thẻ nếu nó thuộc sở hữu của người dùng hiện tại.
    [HttpGet("flashcard-sets/{id}")]
    public async Task<IActionResult> GetSet(int id)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Tải bộ thẻ thuộc sở hữu của người dùng.
        // 3. Trả HTTP 404 hoặc dữ liệu bộ thẻ.
        if (UserId == null) return Challenge();

        var set = await _setService.GetOwnedSetAsync(id, UserId);
        if (set == null) return NotFound();

        return Ok(MapToResponse(set));
    }

    // Cập nhật thông tin bộ thẻ và trả về HTTP 204 khi thành công.
    [HttpPut("flashcard-sets/{id}")]
    public async Task<IActionResult> UpdateSet(int id, UpdateSetRequest request)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Cập nhật tiêu đề, mô tả và trạng thái công khai.
        // 3. Trả HTTP 204 khi cập nhật thành công.
        if (UserId == null) return Challenge();

        await _setService.UpdateSetAsync(
            id,
            request.Title,
            request.Description,
            request.IsPublic,
            UserId,
            request.NewCardQuota,
            request.ReviewPaused);

        return NoContent();
    }

    // Thêm một thẻ vào bộ thẻ và trả về HTTP 201 cùng dữ liệu vừa tạo.
    [HttpPost("flashcards")]
    public async Task<IActionResult> CreateCard(CreateCardRequest request)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Thêm thẻ mới vào bộ thẻ được chỉ định.
        // 3. Trả HTTP 201 cùng đường dẫn và dữ liệu thẻ vừa tạo.
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

    // Lấy chi tiết một thẻ thuộc bộ thẻ của người dùng hiện tại.
    [HttpGet("flashcards/{id}")]
    public async Task<IActionResult> GetCard(int id)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Tải thẻ theo quyền sở hữu.
        // 3. Trả HTTP 404 hoặc dữ liệu thẻ.
        if (UserId == null) return Challenge();

        var card = await _setService.GetCardAsync(id, UserId);
        if (card == null) return NotFound();

        return Ok(MapToResponse(card));
    }

    // Cập nhật nội dung, ảnh và trạng thái đánh sao của một thẻ.
    [HttpPut("flashcards/{id}")]
    public async Task<IActionResult> UpdateCard(int id, UpdateCardRequest request)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Cập nhật nội dung, ảnh và trạng thái đánh sao của thẻ.
        // 3. Trả HTTP 204 khi cập nhật thành công.
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

    // Xóa một thẻ và trả về HTTP 204 khi thành công.
    [HttpDelete("flashcards/{id}")]
    public async Task<IActionResult> DeleteCard(int id)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Xóa thẻ theo quyền sở hữu.
        // 3. Trả HTTP 204 khi xóa thành công.
        if (UserId == null) return Challenge();

        await _setService.DeleteCardAsync(id, UserId);
        return NoContent();
    }

    // Đổi trạng thái đánh sao và trả trạng thái mới cho giao diện.
    [HttpPost("flashcards/{id}/star")]
    public async Task<IActionResult> ToggleStar(int id)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Đảo trạng thái đánh sao của thẻ.
        // 3. Trả trạng thái mới cho giao diện.
        if (UserId == null) return Challenge();

        bool isStarred = await _setService.ToggleStarAsync(id, UserId);
        return Ok(new { isStarred });
    }

    // Thêm nhiều thẻ trong một yêu cầu, có thể thay thế toàn bộ thẻ cũ.
    [HttpPost("flashcards/batch")]
    public async Task<IActionResult> BatchImport(BatchImportRequest request)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Chuyển từng thẻ trong request thành input của service.
        // 3. Lưu hàng loạt và trả danh sách thẻ vừa tạo.
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

    // Lưu lại thứ tự thẻ theo danh sách mã thẻ từ giao diện.
    [HttpPost("flashcards/reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request)
    {
        // 1. Kiểm tra người gọi đã đăng nhập.
        // 2. Lưu thứ tự thẻ theo danh sách id từ request.
        // 3. Trả HTTP 204 khi hoàn tất.
        if (UserId == null) return Challenge();

        await _setService.ReorderCardsAsync(request.SetId, request.OrderedCardIds, UserId);
        return NoContent();
    }

    // Chuyển entity bộ thẻ thành dữ liệu an toàn để trả qua API.
    private static SetResponse MapToResponse(FlashcardSet set)
    {
        // 1. Chọn các trường bộ thẻ được phép công khai qua API.
        // 2. Tạo đối tượng phản hồi độc lập với entity cơ sở dữ liệu.
        return new SetResponse
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            NewCardQuota = set.NewCardQuota,
            ReviewPaused = set.ReviewPaused,
            CreatedAt = set.CreatedAt,
            UpdatedAt = set.UpdatedAt
        };
    }

    // Chuyển entity thẻ thành dữ liệu phản hồi cho giao diện.
    private static CardResponse MapToResponse(Flashcard card)
    {
        // 1. Chọn nội dung và trạng thái cần trả cho trình chỉnh sửa.
        // 2. Tạo đối tượng phản hồi độc lập với entity cơ sở dữ liệu.
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

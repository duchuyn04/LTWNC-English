using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace ltwnc.Controllers;

// CRUD bộ thẻ / thẻ, copy public set. Hầu hết action cần login; Details cho khách.
[Authorize]
public class FlashcardSetController : Controller
{
    // Nghiệp vụ set + card + copy
    private readonly IFlashcardSetService _setService;

    // User hiện tại từ cookie claims
    private readonly ICurrentUser _currentUser;
    private readonly IFlashcardImportService _importService;

    public FlashcardSetController(
        IFlashcardSetService setService,
        ICurrentUser currentUser,
        IFlashcardImportService importService)
    {
        _setService = setService;
        _currentUser = currentUser;
        _importService = importService;
    }

    // GET /Set: thư viện cá nhân kèm progress
    [Route("/Set")]
    public async Task<IActionResult> Index()
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        List<FlashcardSetListItemViewModel> sets =
            await _setService.GetMySetsWithProgressAsync(userId);
        return View(sets);
    }

    // GET form tạo bộ thẻ
    [Route("/Set/Create")]
    public IActionResult Create()
    {
        return View();
    }

    // POST tạo set, redirect Edit để thêm thẻ
    [HttpPost]
    [Route("/Set/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSetViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        FlashcardSet set = await _setService.CreateSetAsync(
            model.Title,
            model.Description,
            model.IsPublic,
            userId);

        TempData["Success"] = "Đã tạo bộ thẻ. Hãy thêm từ đầu tiên.";
        return RedirectToAction("Edit", new { id = set.Id });
    }

    // GET /Set/{id}: public hoặc owner; map SetDetailViewModel + ExistingCopyId
    [Route("/Set/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        string? userId = _currentUser.UserId;

        FlashcardSet? set = await _setService.GetAccessibleSetWithCardsAsync(id, userId);
        if (set == null)
        {
            return NotFound();
        }

        // User khác owner: xem đã copy set này chưa (nút "Vào bản sao")
        int? existingCopyId = null;
        if (userId != null && userId != set.UserId)
        {
            FlashcardSet? copy = await _setService.GetExistingCopyAsync(set.Id, userId);
            if (copy != null)
            {
                existingCopyId = copy.Id;
            }
        }

        SetDetailViewModel model = new SetDetailViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            UserId = set.UserId,
            Flashcards = set.Flashcards.ToList(),
            IsOwner = userId == set.UserId,
            ExistingCopyId = existingCopyId
        };

        return View(model);
    }

    // POST copy public set vào thư viện; về Study hub của bản sao
    [HttpPost]
    [Route("/Set/{id}/Copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Copy(int id)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            FlashcardSet copy = await _setService.CopyPublicSetAsync(id, userId);
            TempData["Success"] = "Đã sao chép bộ thẻ vào thư viện của bạn.";
            return RedirectToAction("Index", "Study", new { setId = copy.Id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // GET Edit: form meta set + ViewBag.Cards (chỉ owner)
    [Route("/Set/{id}/Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        FlashcardSet? set = await _setService.GetSetWithCardsAsync(id, userId);
        if (set == null)
        {
            return NotFound();
        }

        EditSetViewModel model = new EditSetViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic
        };

        // Danh sách thẻ hiển thị trên form (batch actions cũng ở đây)
        ViewBag.Cards = set.Flashcards.ToList();
        return View(model);
    }

    // POST cập nhật title/description/public
    [HttpPost]
    [Route("/Set/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSetViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            await _setService.UpdateSetAsync(
                id,
                model.Title,
                model.Description,
                model.IsPublic,
                userId);
            TempData["Success"] = "Đã lưu thay đổi bộ thẻ.";
            return RedirectToAction("Edit", new { id });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST nhập nhiều thẻ từ CSV/XLSX, luôn redirect về Edit để tránh gửi lại form.
    [HttpPost]
    [Route("/Set/{id}/Import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(int id, IFormFile? file)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            FlashcardImportResult result = await _importService.ImportAsync(
                id,
                userId,
                file!,
                HttpContext.RequestAborted);

            TempData["ImportImportedCount"] = result.ImportedCount;
            TempData["ImportSkippedCount"] = result.SkippedCount;
            if (result.Errors.Count > 0)
            {
                TempData["ImportErrors"] = JsonSerializer.Serialize(result.Errors);
            }

            TempData["Success"] = result.ImportedCount > 0
                ? $"Đã nhập {result.ImportedCount} thẻ thành công."
                : "Không có thẻ hợp lệ nào được nhập.";
            return RedirectToAction("Edit", new { id });
        }
        catch (FlashcardImportException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction("Edit", new { id });
        }
    }

    // POST xóa cả bộ thẻ, về /Set
    [HttpPost]
    [Route("/Set/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            await _setService.DeleteSetAsync(id, userId);
            return RedirectToAction("Index");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST thêm thẻ vào set; validation lỗi -> TempData Error
    [HttpPost]
    [Route("/Set/{setId}/Cards/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCard(
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool isStarred = false)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            await _setService.AddCardAsync(
                setId,
                frontText,
                backText,
                pronunciation,
                partOfSpeech,
                exampleSentence,
                exampleMeaning,
                synonyms,
                imageUrl,
                imageFile,
                isStarred,
                userId);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST AJAX toggle sao thẻ trong trình chỉnh sửa (owner)
    [HttpPost]
    [Route("/Set/{setId}/Cards/{cardId}/ToggleStar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStar(int setId, int cardId)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            bool isStarred = await _setService.ToggleStarAsync(cardId, userId);
            return Json(new { success = true, isStarred });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST sửa thẻ; removeUploadedImage xóa path ảnh upload nếu user bật
    [HttpPost]
    [Route("/Cards/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCard(
        int id,
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool removeUploadedImage = false,
        bool isStarred = false)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            int updatedSetId = await _setService.UpdateCardAsync(
                id,
                frontText,
                backText,
                pronunciation,
                partOfSpeech,
                exampleSentence,
                exampleMeaning,
                synonyms,
                imageUrl,
                imageFile,
                removeUploadedImage,
                isStarred,
                userId);
            return RedirectToAction("Edit", new { id = updatedSetId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST xóa một thẻ, redirect Edit set
    [HttpPost]
    [Route("/Cards/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            int setId = await _setService.DeleteCardAsync(id, userId);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

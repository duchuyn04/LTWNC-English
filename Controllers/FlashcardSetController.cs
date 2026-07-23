using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.ContentReports;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Flashcards;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace ltwnc.Controllers;

// CRUD bộ thẻ / thẻ, copy public set. Hầu hết action cần login; Details cho khách.
[Authorize]
public class FlashcardSetController : Controller
{
    private const int MaxDisplayedImportErrors = 100;

    // Nghiệp vụ set + card + copy
    private readonly IFlashcardSetService _setService;

    // User hiện tại từ cookie claims
    private readonly ICurrentUser _currentUser;
    private readonly IFlashcardImportService _importService;
    private readonly IContentReportService _contentReportService;

    public FlashcardSetController(
        IFlashcardSetService setService,
        ICurrentUser currentUser,
        IFlashcardImportService importService,
        IContentReportService contentReportService)
    {
        _setService = setService;
        _currentUser = currentUser;
        _importService = importService;
        _contentReportService = contentReportService;
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

    // GET form tạo bộ thẻ (redirect sang unified editor)
    [Route("/Set/Create")]
    public IActionResult Create()
    {
        return RedirectToAction("Editor");
    }

    // GET unified editor: tạo mới hoặc chỉnh sửa bộ thẻ
    [Route("/flashcardset/editor/{id?}")]
    public async Task<IActionResult> Editor(int? id)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        EditorViewModel model = new EditorViewModel();

        if (id.HasValue)
        {
            FlashcardSet? set = await _setService.GetSetWithCardsAsync(id.Value, userId);
            if (set == null)
            {
                return NotFound();
            }

            model.Id = set.Id;
            model.Title = set.Title;
            model.Description = set.Description;
            model.IsPublic = set.IsPublic;
            model.IsQuarantined = set.ModerationStatus == FlashcardSetModerationStatus.Quarantined;
            model.ModerationPublicReason = set.ModerationPublicReason;
            model.ModeratedAtUtc = set.ModeratedAtUtc;
            model.Cards = set.Flashcards
                .OrderBy(c => c.OrderIndex)
                .Select(c => new CardViewModel
                {
                    Id = c.Id,
                    FrontText = c.FrontText,
                    BackText = c.BackText,
                    Pronunciation = c.Pronunciation,
                    PartOfSpeech = c.PartOfSpeech,
                    ExampleSentence = c.ExampleSentence,
                    ExampleMeaning = c.ExampleMeaning,
                    Synonyms = c.Synonyms,
                    ImageUrl = c.ImageUrl,
                    UploadedImagePath = c.UploadedImagePath,
                    IsStarred = c.IsStarred,
                    OrderIndex = c.OrderIndex
                })
                .ToList();
        }
        else
        {
            // New set starts with one empty card
            model.Cards.Add(new CardViewModel());
        }

        return View(model);
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

        try
        {
            FlashcardSet set = await _setService.CreateSetAsync(
                model.Title,
                model.Description,
                model.IsPublic,
                userId);

            TempData["Success"] = "Đã tạo bộ thẻ. Hãy thêm từ đầu tiên.";
            return RedirectToAction("Edit", new { id = set.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(model.Title), ex.Message);
            return View(model);
        }
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

        bool hasOpenReport = false;
        if (userId != null && userId != set.UserId)
        {
            hasOpenReport = await _contentReportService.HasOpenReportAsync(set.Id, userId);
        }

        SetDetailViewModel model = new SetDetailViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            IsQuarantined = set.ModerationStatus == FlashcardSetModerationStatus.Quarantined,
            ModerationPublicReason = set.ModerationPublicReason,
            ModeratedAtUtc = set.ModeratedAtUtc,
            Flashcards = FlashcardViewModelMapper.FromEntities(set.Flashcards),
            IsOwner = userId == set.UserId,
            ExistingCopyId = existingCopyId,
            ReportReasonOptions = _contentReportService.GetReasonOptions(),
            CanReport = userId != null && userId != set.UserId && set.IsPublic && !hasOpenReport && set.ModerationStatus == FlashcardSetModerationStatus.Active,
            HasOpenReport = hasOpenReport
        };

        return View(model);
    }

    // POST gửi báo cáo nội dung cho bộ công khai, dùng antiforgery và chỉ nhận lý do cố định.
    [HttpPost]
    [Route("/Set/{id}/Report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(
        int id,
        ContentReportInputModel input,
        CancellationToken cancellationToken = default)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["ContentReportError"] = FirstModelStateError();
            return RedirectToAction(nameof(Details), new { id });
        }

        ContentReportSubmitResult result = await _contentReportService.SubmitAsync(
            new SubmitContentReportCommand(
                FlashcardSetId: id,
                ReporterUserId: userId,
                Reason: input.Reason,
                Description: input.Description),
            cancellationToken);

        if (result.Succeeded)
        {
            TempData["ContentReportSuccess"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Failure == ContentReportSubmitFailure.NotFoundOrPrivate)
        {
            return NotFound("Không tìm thấy bộ flashcard công khai có thể báo cáo.");
        }

        TempData["ContentReportError"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
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
            return NotFound("Không tìm thấy bộ flashcard công khai có thể sao chép.");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // GET Edit: redirect sang unified editor
    [Route("/Set/{id}/Edit")]
    public IActionResult Edit(int id)
    {
        return RedirectToAction("Editor", new { id });
    }

    // POST cập nhật title/description/public
    [HttpPost]
    [Route("/Set/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSetPageViewModel model)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            EditSetPageViewModel? invalidPage = await BuildEditPageViewModelAsync(id, userId, model);
            if (invalidPage == null)
            {
                return NotFound();
            }

            return View(invalidPage);
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
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(model.Title), ex.Message);
            EditSetPageViewModel? errorPage = await BuildEditPageViewModelAsync(id, userId, model);
            if (errorPage == null)
            {
                return NotFound();
            }

            return View(errorPage);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST nhập nhiều thẻ từ CSV/XLSX, luôn redirect về Edit để tránh gửi lại form.
    [HttpPost]
    [Route("/Set/{id}/Import/Preview")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> PreviewImport(int id, IFormFile? file)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            FlashcardImportPreview preview = await _importService.PreviewAsync(
                id,
                userId,
                file!,
                HttpContext.RequestAborted);

            return Json(new
            {
                validCount = preview.ValidCount,
                skippedCount = preview.SkippedCount,
                rows = preview.Rows.Take(5),
                errors = preview.Errors.Take(MaxDisplayedImportErrors),
                errorsOmittedCount = Math.Max(
                    0,
                    preview.Errors.Count - MaxDisplayedImportErrors)
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (FlashcardImportException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost]
    [Route("/Set/{id}/Import")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> Import(
        int id,
        IFormFile? file,
        bool replaceAll = false)
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
                replaceAll,
                HttpContext.RequestAborted);

            TempData["ImportImportedCount"] = result.ImportedCount;
            TempData["ImportSkippedCount"] = result.SkippedCount;
            FlashcardImportError[] displayedErrors = result.Errors
                .Take(MaxDisplayedImportErrors)
                .ToArray();
            TempData["ImportErrorsOmittedCount"] = result.Errors.Count - displayedErrors.Length;
            if (displayedErrors.Length > 0)
            {
                TempData["ImportErrors"] = JsonSerializer.Serialize(displayedErrors);
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
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> AddCard(int setId, AddCardInputModel input)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstModelStateError();
            return RedirectToAction("Edit", new { id = setId });
        }

        try
        {
            await _setService.AddCardAsync(
                setId,
                input.FrontText,
                input.BackText,
                input.Pronunciation,
                input.PartOfSpeech,
                input.ExampleSentence,
                input.ExampleMeaning,
                input.Synonyms,
                input.ImageUrl,
                input.ImageFile,
                input.IsStarred,
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
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> EditCard(int id, EditCardInputModel input)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstModelStateError();
            return RedirectToAction("Edit", new { id = input.SetId });
        }

        try
        {
            int updatedSetId = await _setService.UpdateCardAsync(
                id,
                input.FrontText,
                input.BackText,
                input.Pronunciation,
                input.PartOfSpeech,
                input.ExampleSentence,
                input.ExampleMeaning,
                input.Synonyms,
                input.ImageUrl,
                input.ImageFile,
                input.RemoveUploadedImage,
                input.IsStarred,
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
            return RedirectToAction("Edit", new { id = input.SetId });
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

    private async Task<EditSetPageViewModel?> BuildEditPageViewModelAsync(
        int setId,
        string userId,
        EditSetViewModel? postedSet = null)
    {
        FlashcardSet? set = await _setService.GetSetWithCardsAsync(setId, userId);
        if (set == null)
        {
            return null;
        }

        EditSetPageViewModel pageModel = new EditSetPageViewModel
        {
            Id = set.Id,
            Title = postedSet?.Title ?? set.Title,
            Description = postedSet?.Description ?? set.Description,
            IsPublic = postedSet?.IsPublic ?? set.IsPublic,
            Cards = FlashcardViewModelMapper.FromEntities(set.Flashcards)
        };
        return pageModel;
    }

    private string FirstModelStateError()
    {
        return ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Dữ liệu thẻ không hợp lệ.";
    }
}

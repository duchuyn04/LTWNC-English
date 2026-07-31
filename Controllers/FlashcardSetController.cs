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

// Quản lý bộ thẻ và từng thẻ: tạo, xem, sửa, xóa, nhập tệp, sao chép và báo cáo nội dung.
[Authorize]
public class FlashcardSetController : Controller
{
    // Chỉ đưa tối đa 100 lỗi nhập tệp ra giao diện để tránh phản hồi quá lớn.
    private const int MaxDisplayedImportErrors = 100;

    // Service xử lý bộ thẻ, từng thẻ và thao tác sao chép.
    private readonly IFlashcardSetService _setService;

    // Đọc người dùng hiện tại, xử lý tệp nhập và tiếp nhận báo cáo nội dung.
    private readonly ICurrentUser _currentUser;
    private readonly IFlashcardImportService _importService;
    private readonly IContentReportService _contentReportService;

    // Nhận các service cần dùng qua dependency injection.
    public FlashcardSetController(
        IFlashcardSetService setService,
        ICurrentUser currentUser,
        IFlashcardImportService importService,
        IContentReportService contentReportService)
    {
        // 1. Lưu các service để những action quản lý bộ thẻ sử dụng.
        _setService = setService;
        _currentUser = currentUser;
        _importService = importService;
        _contentReportService = contentReportService;
    }

    // Hiển thị thư viện bộ thẻ cá nhân kèm tiến độ học.
    [Route("/Set")]
    public async Task<IActionResult> Index()
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Lấy các bộ thẻ cá nhân kèm tiến độ học.
        // 3. Hiển thị danh sách trong thư viện cá nhân.
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        List<FlashcardSetListItemViewModel> sets =
            await _setService.GetMySetsWithProgressAsync(userId);
        return View(sets);
    }

    // Chuyển đường dẫn tạo cũ sang trình chỉnh sửa thống nhất.
    [Route("/Set/Create")]
    public IActionResult Create()
    {
        // 1. Chuyển route tạo cũ sang trình chỉnh sửa thống nhất.
        return RedirectToAction("Editor");
    }

    // Mở trình chỉnh sửa để tạo mới hoặc cập nhật một bộ thẻ đã có.
    [Route("/flashcardset/editor/{id?}")]
    public async Task<IActionResult> Editor(int? id)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Nếu có id, tải bộ thẻ và ánh xạ từng thẻ vào ViewModel.
        // 3. Nếu tạo mới, thêm sẵn một thẻ trống rồi hiển thị trình chỉnh sửa.
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
            model.ReviewPaused = set.ReviewPaused;
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
            // Bộ thẻ mới bắt đầu bằng một thẻ trống để người dùng nhập ngay.
            model.Cards.Add(new CardViewModel());
        }

        return View(model);
    }

    // Tạo bộ thẻ từ form cũ rồi chuyển sang trang sửa để thêm nội dung.
    [HttpPost]
    [Route("/Set/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSetViewModel model)
    {
        // 1. Kiểm tra dữ liệu form và phiên đăng nhập.
        // 2. Yêu cầu service tạo bộ thẻ mới.
        // 3. Chuyển sang trang sửa hoặc hiển thị lỗi tiêu đề.
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

    // Hiển thị bộ thẻ công khai hoặc bộ thẻ của chính người dùng, kèm trạng thái bản sao.
    [Route("/Set/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        // 1. Lấy bộ thẻ mà khách hoặc người dùng hiện tại được phép xem.
        // 2. Kiểm tra trạng thái bản sao và báo cáo nội dung của người đang xem.
        // 3. Tạo ViewModel chi tiết rồi hiển thị trang.
        string? userId = _currentUser.UserId;

        FlashcardSet? set = await _setService.GetAccessibleSetWithCardsAsync(id, userId);
        if (set == null)
        {
            return NotFound();
        }

        // Nếu đang xem bộ thẻ của người khác, kiểm tra người dùng đã sao chép bộ này chưa.
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

    // Gửi báo cáo cho bộ thẻ công khai; form được bảo vệ bằng antiforgery và lý do hợp lệ.
    [HttpPost]
    [Route("/Set/{id}/Report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(
        int id,
        ContentReportInputModel input,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra đăng nhập và dữ liệu báo cáo.
        // 2. Gửi báo cáo tới service cùng lý do, mô tả.
        // 3. Trả 404 hoặc lưu thông báo thành công/thất bại trước khi chuyển hướng.
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

    // Sao chép bộ thẻ công khai vào thư viện cá nhân rồi mở trang học của bản sao.
    [HttpPost]
    [Route("/Set/{id}/Copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Copy(int id)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Sao chép bộ thẻ công khai vào thư viện cá nhân.
        // 3. Mở trang học của bản sao hoặc trả lỗi quyền phù hợp.
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

    // Nhân bản bộ thẻ của chính người dùng thành một bộ private độc lập.
    [HttpPost]
    [Route("/Set/{id:int}/Duplicate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(
        int id,
        CancellationToken cancellationToken = default)
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            FlashcardSet duplicate = await _setService.DuplicateOwnedSetAsync(
                id,
                userId,
                cancellationToken);
            TempData["Success"] = $"Đã nhân bản bộ thẻ thành “{duplicate.Title}”.";
            return RedirectToAction(nameof(Details), new { id = duplicate.Id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Không tìm thấy bộ thẻ có thể nhân bản.");
        }
        catch (InvalidOperationException exception)
        {
            TempData["DuplicateError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // Chuyển đường dẫn chỉnh sửa cũ sang trình chỉnh sửa thống nhất.
    [Route("/Set/{id}/Edit")]
    public IActionResult Edit(int id)
    {
        // 1. Giữ route cũ nhưng chuyển sang trình chỉnh sửa thống nhất với đúng id.
        return RedirectToAction("Editor", new { id });
    }

    // Cập nhật tiêu đề, mô tả và trạng thái công khai của bộ thẻ.
    [HttpPost]
    [Route("/Set/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSetPageViewModel model)
    {
        // 1. Kiểm tra đăng nhập và dữ liệu form.
        // 2. Cập nhật thông tin bộ thẻ qua service.
        // 3. Dựng lại trang khi có lỗi hoặc chuyển hướng khi lưu thành công.
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
                userId,
                null,
                model.ReviewPaused);
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

    // Nhập nhiều thẻ từ CSV/XLSX rồi chuyển hướng để tránh gửi lại form khi tải lại trang.
    [HttpPost]
    [Route("/Set/{id}/Import")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> Import(int id, IFormFile? file)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Nhập các dòng hợp lệ từ tệp vào bộ thẻ.
        // 3. Lưu số lượng và tối đa 100 lỗi vào TempData rồi chuyển về trang sửa.
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

    // Đọc CSV/XLSX từ trình chỉnh sửa và trả JSON để cập nhật danh sách thẻ tại chỗ.
    [HttpPost]
    [Route("/Set/{id}/ImportFile")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> ImportFile(int id, IFormFile? file, bool replaceAll = false)
    {
        // 1. Kiểm tra đăng nhập rồi đọc dữ liệu CSV/XLSX.
        // 2. Kiểm tra lỗi tệp, giới hạn lỗi hiển thị và chuyển các dòng hợp lệ thành input.
        // 3. Lưu thẻ hàng loạt và trả JSON cho trình chỉnh sửa.
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        try
        {
            FlashcardFileParseResult parsed = await _importService.ParseAsync(
                file!,
                HttpContext.RequestAborted);

            FlashcardImportError[] displayedErrors = parsed.Errors
                .Take(MaxDisplayedImportErrors)
                .ToArray();
            int omittedErrorCount = Math.Max(0, parsed.Errors.Count - displayedErrors.Length);

            if (!string.IsNullOrWhiteSpace(parsed.FileError))
            {
                return BadRequest(new
                {
                    message = parsed.FileError,
                    skippedCount = parsed.Errors.Count,
                    errors = displayedErrors,
                    omittedErrorCount
                });
            }

            if (parsed.Rows.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Không có thẻ hợp lệ nào trong tệp.",
                    skippedCount = parsed.Errors.Count,
                    errors = displayedErrors,
                    omittedErrorCount
                });
            }

            List<BatchImportCardItem> items = parsed.Rows
                .Select(row => new BatchImportCardItem
                {
                    FrontText = row.FrontText,
                    BackText = row.BackText,
                    Pronunciation = row.Pronunciation,
                    PartOfSpeech = row.PartOfSpeech,
                    ExampleSentence = row.ExampleSentence,
                    ExampleMeaning = row.ExampleMeaning,
                    Synonyms = row.Synonyms,
                    ImageUrl = row.ImageUrl,
                    IsStarred = false
                })
                .ToList();

            List<Flashcard> created = await _setService.BatchImportCardsAsync(
                id,
                items,
                replaceAll,
                userId);

            return Ok(new
            {
                importedCount = created.Count,
                skippedCount = parsed.Errors.Count,
                errors = displayedErrors,
                omittedErrorCount,
                cards = created.Select(card => new CardResponse
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
                }).ToList()
            });
        }
        catch (FlashcardImportException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Xóa toàn bộ bộ thẻ rồi quay về thư viện cá nhân.
    [HttpPost]
    [Route("/Set/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Yêu cầu service xóa bộ thẻ thuộc sở hữu của người dùng.
        // 3. Quay về thư viện hoặc trả lỗi cấm truy cập.
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

    // Thêm một thẻ; lỗi dữ liệu được lưu vào TempData để hiển thị sau chuyển hướng.
    [HttpPost]
    [Route("/Set/{setId}/Cards/Create")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> AddCard(int setId, AddCardInputModel input)
    {
        // 1. Kiểm tra đăng nhập và dữ liệu thẻ.
        // 2. Gửi nội dung cùng tệp ảnh tới service để tạo thẻ.
        // 3. Quay về trang sửa và hiển thị lỗi nếu thao tác thất bại.
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

    // Đổi trạng thái đánh sao của thẻ và trả JSON cho trình chỉnh sửa.
    [HttpPost]
    [Route("/Set/{setId}/Cards/{cardId}/ToggleStar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStar(int setId, int cardId)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Đổi trạng thái đánh sao của thẻ qua service.
        // 3. Trả trạng thái mới dưới dạng JSON hoặc mã lỗi phù hợp.
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

    // Cập nhật thẻ; có thể xóa ảnh đã tải lên nếu người dùng chọn tùy chọn tương ứng.
    [HttpPost]
    [Route("/Cards/{id}/Edit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("uploads")]
    public async Task<IActionResult> EditCard(int id, EditCardInputModel input)
    {
        // 1. Kiểm tra đăng nhập và dữ liệu chỉnh sửa.
        // 2. Cập nhật nội dung, ảnh và trạng thái đánh sao qua service.
        // 3. Quay về đúng bộ thẻ hoặc trả lỗi tương ứng.
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

    // Xóa một thẻ rồi quay về trang sửa bộ thẻ chứa nó.
    [HttpPost]
    [Route("/Cards/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        // 1. Kiểm tra người dùng đã đăng nhập.
        // 2. Xóa thẻ và nhận lại id bộ thẻ chứa nó.
        // 3. Quay về trang sửa hoặc trả lỗi tương ứng.
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

    // Dựng lại dữ liệu trang sửa khi form không hợp lệ hoặc service trả lỗi.
    private async Task<EditSetPageViewModel?> BuildEditPageViewModelAsync(
        int setId,
        string userId,
        EditSetViewModel? postedSet = null)
    {
        // 1. Tải bộ thẻ cùng danh sách thẻ theo quyền sở hữu.
        // 2. Ưu tiên dữ liệu người dùng vừa nhập để không làm mất nội dung form.
        // 3. Trả ViewModel hoàn chỉnh hoặc null nếu không tìm thấy bộ thẻ.
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
            ReviewPaused = postedSet?.ReviewPaused ?? set.ReviewPaused,
            Cards = FlashcardViewModelMapper.FromEntities(set.Flashcards)
        };
        return pageModel;
    }

    // Lấy lỗi kiểm tra dữ liệu đầu tiên để hiển thị thông báo ngắn gọn.
    private string FirstModelStateError()
    {
        // 1. Gom lỗi từ mọi trường trên form.
        // 2. Lấy thông báo có nội dung đầu tiên.
        // 3. Dùng thông báo mặc định nếu không tìm thấy lỗi cụ thể.
        return ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Dữ liệu thẻ không hợp lệ.";
    }
}

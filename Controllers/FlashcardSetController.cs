using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.FlashcardSet;

namespace ltwnc.Controllers;

// Controller quản lý bộ thẻ flashcard - yêu cầu đăng nhập
[Authorize]
public class FlashcardSetController : Controller
{
    private readonly IFlashcardSetService _setService;
    private readonly IAccountService _accountService;

    // Inject service bộ thẻ và service tài khoản
    public FlashcardSetController(IFlashcardSetService setService, IAccountService accountService)
    {
        _setService = setService;
        _accountService = accountService;
    }

    // Hiển thị danh sách bộ thẻ của người dùng hiện tại
    [Route("/Set")]
    public async Task<IActionResult> Index()
    {
        // Lấy thông tin người dùng đang đăng nhập
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        // Lấy tất cả bộ thẻ thuộc về người dùng này
        var sets = await _setService.GetMySetsAsync(user.Id);
        return View(sets);
    }

    // Hiển thị form tạo bộ thẻ mới
    [Route("/Set/Create")]
    public IActionResult Create()
    {
        return View();
    }

    // Xử lý dữ liệu tạo bộ thẻ từ form
    [HttpPost]
    [Route("/Set/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSetViewModel model)
    {
        // Kiểm tra dữ liệu đầu vào hợp lệ
        if (!ModelState.IsValid) return View(model);

        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        // Tạo bộ thẻ mới → chuyển sang trang chỉnh sửa để thêm thẻ
        var set = await _setService.CreateSetAsync(model.Title, model.Description, model.IsPublic, user.Id);
        return RedirectToAction("Edit", new { id = set.Id });
    }

    // Hiển thị chi tiết bộ thẻ (ai cũng xem được nếu là public)
    [Route("/Set/{id}")]
    [AllowAnonymous] // Cho phép truy cập không cần đăng nhập
    public async Task<IActionResult> Details(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);

        // Chỉ cho xem bộ công khai hoặc bộ của chính người dùng hiện tại
        var set = await _setService.GetAccessibleSetWithCardsAsync(id, user?.Id);
        if (set == null) return NotFound();

        // Ánh xạ sang ViewModel để hiển thị trên View
        var model = new SetDetailViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            UserId = set.UserId,
            Flashcards = set.Flashcards.ToList(),
            IsOwner = user?.Id == set.UserId // Kiểm tra người xem có phải chủ sở hữu không
        };
        return View(model);
    }

    // Hiển thị form chỉnh sửa bộ thẻ (bao gồm danh sách thẻ)
    [Route("/Set/{id}/Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        // Lấy bộ thẻ kèm danh sách thẻ — chỉ chủ sở hữu mới sửa được
        var set = await _setService.GetSetWithCardsAsync(id, user.Id);
        if (set == null) return NotFound();

        // Ánh xạ dữ liệu sang ViewModel
        var model = new EditSetViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic
        };

        // Truyền danh sách thẻ qua ViewBag để hiển thị trên form
        ViewBag.Cards = set.Flashcards.ToList();
        return View(model);
    }

    // Xử lý cập nhật bộ thẻ từ form
    [HttpPost]
    [Route("/Set/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSetViewModel model)
    {
        // Kiểm tra dữ liệu đầu vào hợp lệ
        if (!ModelState.IsValid) return View(model);

        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            // Cập nhật thông tin bộ thẻ
            await _setService.UpdateSetAsync(id, model.Title, model.Description, model.IsPublic, user.Id);
            return RedirectToAction("Edit", new { id });
        }
        catch (UnauthorizedAccessException)
        {
            // Không phải chủ sở hữu → cấm truy cập
            return Forbid();
        }
    }

    // Xử lý xóa bộ thẻ
    [HttpPost]
    [Route("/Set/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            // Xóa bộ thẻ và tất cả thẻ bên trong
            await _setService.DeleteSetAsync(id, user.Id);
            return RedirectToAction("Index");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Xử lý thêm thẻ mới vào bộ thẻ
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
        bool isStarred = false)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            // Thêm thẻ mới vào bộ
            await _setService.AddCardAsync(
                setId,
                frontText,
                backText,
                pronunciation,
                partOfSpeech,
                exampleSentence,
                exampleMeaning,
                synonyms,
                isStarred,
                user.Id);
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

    // Xử lý chỉnh sửa thẻ
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
        bool isStarred = false)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            // Cập nhật nội dung thẻ, trả về setId để redirect
            var updatedSetId = await _setService.UpdateCardAsync(
                id,
                frontText,
                backText,
                pronunciation,
                partOfSpeech,
                exampleSentence,
                exampleMeaning,
                synonyms,
                isStarred,
                user.Id);
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

    // Xử lý xóa thẻ khỏi bộ
    [HttpPost]
    [Route("/Cards/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            // Xóa thẻ, trả về setId để redirect
            var setId = await _setService.DeleteCardAsync(id, user.Id);
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ltwnc.Services;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Controllers;

// CRUD bộ thẻ / thẻ, copy public set. Hầu hết action cần login; Details cho khách.
[Authorize]
public class FlashcardSetController : Controller
{
    // Nghiệp vụ set + card + copy
    private readonly IFlashcardSetService _setService;

    // User hiện tại
    private readonly UserManager<IdentityUser> _userManager;

    // Inject set service và UserManager
    public FlashcardSetController(
        IFlashcardSetService setService,
        UserManager<IdentityUser> userManager)
    {
        _setService = setService;
        _userManager = userManager;
    }

    // GET /Set: thư viện cá nhân kèm progress
    [Route("/Set")]
    public async Task<IActionResult> Index()
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        List<FlashcardSetListItemViewModel> sets =
            await _setService.GetMySetsWithProgressAsync(user.Id);
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

        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        FlashcardSet set = await _setService.CreateSetAsync(
            model.Title,
            model.Description,
            model.IsPublic,
            user.Id);

        return RedirectToAction("Edit", new { id = set.Id });
    }

    // GET /Set/{id}: public hoặc owner; map SetDetailViewModel + ExistingCopyId
    [Route("/Set/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);

        FlashcardSet? set = await _setService.GetAccessibleSetWithCardsAsync(id, user?.Id);
        if (set == null)
        {
            return NotFound();
        }

        // User khác owner: xem đã copy set này chưa (nút "Vào bản sao")
        int? existingCopyId = null;
        if (user != null && user.Id != set.UserId)
        {
            FlashcardSet? copy = await _setService.GetExistingCopyAsync(set.Id, user.Id);
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
            IsOwner = user?.Id == set.UserId,
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
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        try
        {
            FlashcardSet copy = await _setService.CopyPublicSetAsync(id, user.Id);
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
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        FlashcardSet? set = await _setService.GetSetWithCardsAsync(id, user.Id);
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

        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
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
                user.Id);
            return RedirectToAction("Edit", new { id });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST xóa cả bộ thẻ, về /Set
    [HttpPost]
    [Route("/Set/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        try
        {
            await _setService.DeleteSetAsync(id, user.Id);
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
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
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
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
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

    // POST xóa một thẻ, redirect Edit set
    [HttpPost]
    [Route("/Cards/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        try
        {
            int setId = await _setService.DeleteCardAsync(id, user.Id);
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

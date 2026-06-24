using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.FlashcardSet;

namespace ltwnc.Controllers;

[Authorize]
public class FlashcardSetController : Controller
{
    private readonly IFlashcardSetService _setService;
    private readonly IAccountService _accountService;

    public FlashcardSetController(IFlashcardSetService setService, IAccountService accountService)
    {
        _setService = setService;
        _accountService = accountService;
    }

    [Route("/Set")]
    public async Task<IActionResult> Index()
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        var sets = await _setService.GetMySetsAsync(user.Id);
        return View(sets);
    }

    [Route("/Set/Create")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Route("/Set/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSetViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        var set = await _setService.CreateSetAsync(model.Title, model.Description, model.IsPublic, user.Id);
        return RedirectToAction("Edit", new { id = set.Id });
    }

    [Route("/Set/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        var set = await _setService.GetSetByIdAsync(id);
        if (set == null) return NotFound();

        var setWithCards = await _setService.GetSetWithCardsAsync(id, set.UserId);

        var model = new SetDetailViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic,
            UserId = set.UserId,
            Flashcards = setWithCards?.Flashcards.ToList() ?? new(),
            IsOwner = user?.Id == set.UserId
        };
        return View(model);
    }

    [Route("/Set/{id}/Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        var set = await _setService.GetSetWithCardsAsync(id, user.Id);
        if (set == null) return NotFound();

        var model = new EditSetViewModel
        {
            Id = set.Id,
            Title = set.Title,
            Description = set.Description,
            IsPublic = set.IsPublic
        };
        ViewBag.Cards = set.Flashcards.ToList();
        return View(model);
    }

    [HttpPost]
    [Route("/Set/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSetViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await _setService.UpdateSetAsync(id, model.Title, model.Description, model.IsPublic, user.Id);
            return RedirectToAction("Edit", new { id });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [Route("/Set/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
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

    [HttpPost]
    [Route("/Set/{setId}/Cards/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCard(int setId, string frontText, string backText)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await _setService.AddCardAsync(setId, frontText, backText, user.Id);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [Route("/Cards/{id}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCard(int id, string frontText, string backText)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            var setId = await _setService.UpdateCardAsync(id, frontText, backText, user.Id);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [Route("/Cards/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            var setId = await _setService.DeleteCardAsync(id, user.Id);
            return RedirectToAction("Edit", new { id = setId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

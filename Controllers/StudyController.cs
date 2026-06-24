using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Controllers;

[Authorize]
public class StudyController : Controller
{
    private readonly IStudyService _studyService;
    private readonly IFlashcardSetService _setService;
    private readonly IAccountService _accountService;

    public StudyController(IStudyService studyService, IFlashcardSetService setService, IAccountService accountService)
    {
        _studyService = studyService;
        _setService = setService;
        _accountService = accountService;
    }

    [Route("/Study/{setId}")]
    public async Task<IActionResult> Index(int setId)
    {
        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null) return NotFound();
        ViewBag.SetTitle = set.Title;
        ViewBag.SetId = setId;
        return View();
    }

    [Route("/Study/{setId}/Flashcard")]
    public async Task<IActionResult> Flashcard(int setId, int index = 0)
    {
        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null) return NotFound();

        var cards = await _studyService.GetFlashcardsForStudyAsync(setId);
        if (!cards.Any())
        {
            TempData["Message"] = "Bộ thẻ này chưa có thẻ nào.";
            return RedirectToAction("Index", new { setId });
        }

        var model = new FlashcardStudyViewModel
        {
            SetId = setId,
            SetTitle = set.Title,
            Flashcards = cards,
            CurrentIndex = Math.Clamp(index, 0, cards.Count - 1)
        };

        return View(model);
    }

    [HttpPost]
    [Route("/Study/{setId}/Flashcard/Mark")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkLearned(int setId, int cardId, bool learned)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        await _studyService.MarkLearnedAsync(user.Id, cardId, learned);
        return RedirectToAction("Flashcard", new { setId });
    }

    [HttpPost]
    [Route("/Study/{setId}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int setId)
    {
        var user = await _accountService.GetCurrentUserAsync(User);
        if (user == null) return Challenge();

        await _studyService.CompleteSessionAsync(user.Id, setId, Models.Entities.StudyMode.Flashcard);
        TempData["Success"] = "Hoàn thành buổi học!";
        return RedirectToAction("Index", new { setId });
    }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Models;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Home;

namespace ltwnc.Controllers;

public class HomeController : Controller
{
    private readonly IFlashcardSetService _setService;

    public HomeController(IFlashcardSetService setService)
    {
        _setService = setService;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var model = new HomeViewModel();

        if (!string.IsNullOrEmpty(q))
        {
            model.SearchQuery = q;
            model.PublicSets = await _setService.SearchPublicSetsAsync(q);
        }
        else
        {
            model.PublicSets = await _setService.GetPublicSetsAsync();
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

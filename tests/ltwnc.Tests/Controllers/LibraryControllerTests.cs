using ltwnc.Controllers;
using ltwnc.Models.ViewModels.Library;
using ltwnc.Services.PublicLibrary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ltwnc.Tests.Controllers;

// Kiểm tra LibraryController: forward query đúng, map view model, route ẩn danh.
public class LibraryControllerTests
{
    [Fact]
    public async Task Index_ForwardsQueryAndReturnsMappedModel()
    {
        var service = new Mock<IPublicLibraryService>();
        var result = new PublicLibraryResult(
            "ielts", "recent", 2, 12, 13, 2,
            new PublicLibrarySummary(20, 300, 40),
            [new PublicLibrarySetItem(7, "IELTS", null, "minhanh", 20, 4, new DateTime(2026, 7, 20))]);
        service.Setup(item => item.BrowseAsync(
                new PublicLibraryQuery(" IELTS ", "recent", 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var controller = new LibraryController(service.Object);

        ViewResult response = Assert.IsType<ViewResult>(
            await controller.Index(" IELTS ", "recent", 2, default));

        LibraryIndexViewModel model = Assert.IsType<LibraryIndexViewModel>(response.Model);
        Assert.Equal("ielts", model.Search);
        Assert.Equal(7, Assert.Single(model.Items).Id);
    }

    [Fact]
    public void Index_AllowsAnonymousAndMapsToLibraryRoute()
    {
        Type controllerType = typeof(LibraryController);
        MethodInfo action = controllerType.GetMethod(nameof(LibraryController.Index))!;

        bool allowsAnonymous =
            controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any() ||
            action.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any();
        Assert.True(allowsAnonymous);

        HttpGetAttribute? route = action.GetCustomAttribute<HttpGetAttribute>();
        Assert.Equal("/Library", route?.Template);
    }
}

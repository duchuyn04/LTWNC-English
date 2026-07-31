using ltwnc.Areas.Admin.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.AiProviders;
using ltwnc.Services.Ai;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public sealed class AiProvidersControllerTests
{
    [Fact]
    public async Task Index_DisplaysProviderPositionInEffectiveRoutingOrder()
    {
        var service = new Mock<IAiProviderService>();
        service.Setup(candidate => candidate.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AiProvider { Id = 9, Name = "Primary", IsPrimary = true, Priority = 2 },
                new AiProvider { Id = 1, Name = "First fallback", Priority = 1 },
                new AiProvider { Id = 3, Name = "Second fallback", Priority = 3 }
            ]);
        var controller = new AiProvidersController(service.Object);

        ViewResult result = Assert.IsType<ViewResult>(
            await controller.Index(CancellationToken.None));
        AiProviderIndexViewModel model = Assert.IsType<AiProviderIndexViewModel>(result.Model);

        Assert.Equal([1, 2, 3], model.Providers.Select(provider => provider.Priority));
    }
}

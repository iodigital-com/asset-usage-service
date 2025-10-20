using AssetUsageService.Integration.ViewModel;
namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    public AssetItemController()
    {
    }
    public async Task ProcessPublishedItemAsync(PublishedItemViewModel publishedItemViewModel, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}

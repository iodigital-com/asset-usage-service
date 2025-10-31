using AssetUsageService.Business.Models;
using AssetUsageService.Business.Services;
using AssetUsageService.Integration.Models;
namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    private DeltaCalculationService deltaCalculationService;
    public AssetItemController(DeltaCalculationService deltaCalculationService)
    {
        this.deltaCalculationService = deltaCalculationService;
    }
    public async Task ProcessPublishedItemAsync(PublishedItem publishedItemViewModel, CancellationToken cancellationToken)
    {
       ItemAssetChanges itemAssetChanges = await deltaCalculationService.CalculateDeltaAsync(publishedItemViewModel.ItemId, publishedItemViewModel.AssetIds, cancellationToken);
    }
}

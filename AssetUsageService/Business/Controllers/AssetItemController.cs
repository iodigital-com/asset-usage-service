using AssetUsageService.Business.Services;
using AssetUsageService.Integration.ViewModel;
namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    private DeltaCalculationService deltaCalculationService;
    public AssetItemController(DeltaCalculationService deltaCalculationService)
    {
        this.deltaCalculationService = deltaCalculationService;
    }
    public async Task ProcessPublishedItemAsync(PublishedItemViewModel publishedItemViewModel, CancellationToken cancellationToken)
    {
        await deltaCalculationService.CalculateDeltaAsync(publishedItemViewModel.ItemId, publishedItemViewModel.AssetIds, cancellationToken);
    }
}

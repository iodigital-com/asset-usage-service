using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageService.Integration;

public interface IContentHubConnectionService
{
    IWebMClient CreateClient();
    Task<bool> IsReachableAsync(CancellationToken cancellationToken = default);
}

using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageService.Integration;

public class APIGateway
{
    private readonly ContentHubConnectionService _contentHubConnection;

    public APIGateway(ContentHubConnectionService contentHubConnection)
    {
        _contentHubConnection = contentHubConnection;
    }

    public Task<IWebMClient> GetContentHubClientAsync(CancellationToken cancellationToken = default)
    {
        var client = _contentHubConnection.CreateClient();
        return Task.FromResult(client);
    }

    public Task<bool> IsContentHubReachableAsync(CancellationToken cancellationToken = default)
    {
        return _contentHubConnection.IsReachableAsync(cancellationToken);
    }
}
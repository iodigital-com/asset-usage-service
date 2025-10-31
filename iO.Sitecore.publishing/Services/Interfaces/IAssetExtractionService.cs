using Sitecore.Data.Items;
using System.Collections.Generic;

namespace iO.Sitecore.Publishing.Services
{
    public interface IAssetExtractionService
    {
        List<string> ExtractAssetIds(Item item);
        string ExtractPublicLink(Item item);
    }
}
using System.Collections.Generic;

namespace iO.Sitecore.Publishing.Interfaces.Services
{
    public interface IAssetExtractionService
    {
        List<string> ExtractAssetIdsFromFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields);
        List<string> ExtractPublicLinksFromFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields);
        List<string> ExtractPublicLinksFromAnyFieldData(IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> fields);
    }
}
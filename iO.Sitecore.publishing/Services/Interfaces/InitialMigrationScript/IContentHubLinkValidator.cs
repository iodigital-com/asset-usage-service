using System;
using System.Collections.Generic;

namespace iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript
{
    public interface IContentHubLinkValidator
    {
        bool HasValidLinks(List<string> publicLinks);
    }
}

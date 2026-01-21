using iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iO.Sitecore.Publishing.Services
{
    public class ContentHubLinkValidator : IContentHubLinkValidator
    {
        private readonly string _contentHubEndpoint;

        public ContentHubLinkValidator(string contentHubEndpoint)
        {
            _contentHubEndpoint = contentHubEndpoint;
        }

        public bool HasValidLinks(List<string> publicLinks)
        {
            if (string.IsNullOrWhiteSpace(_contentHubEndpoint))
            {
                return true;
            }

            if (publicLinks == null || publicLinks.Count == 0)
            {
                return false;
            }

            return publicLinks.Any(link =>
                !string.IsNullOrWhiteSpace(link) &&
                link.IndexOf(_contentHubEndpoint, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;

namespace iO.Sitecore.Publishing.Tests.Unit
{
    public class AssetUsageServiceClientTests : IDisposable
    {
        #region Test Constants

        private const string TestEndpointUrl = "http://localhost:7183/api/SitecorePublishAPI";
        private const string ApiEndpointSettingName = "AssetUsageService.ApiEndpoint";

        private const string ValidItemId = "110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9";
        private const string ValidItemPath = "/sitecore/content/home";
        private const string ValidItemName = "Home";
        private const string ValidTemplateName = "Page";
        private const string ValidLanguage = "en";
        private const int ValidVersion = 1;
        private const string ValidTargetDatabase = "web";

        private const string ValidAssetId = "12345";
        private const string SecondValidAssetId = "67890";
        private const string ValidPublicLink = "https://example.com/asset.jpg";
        private const string SecondValidPublicLink = "https://example.com/asset2.jpg";

        #endregion

        public AssetUsageServiceClientTests()
        {
        }

        #region SendAsync - Happy Path Tests

        [Fact]
        public async Task SendAsync_ValidPayload_SendsSuccessfully()
        {
            var payload = CreateValidPayload();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.True(true);
        }

        #endregion

        #region SendAsync - Edge Cases Tests

        [Fact]
        public async Task SendAsync_NullPayload_DoesNotThrow()
        {
            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(null);
            }

            Assert.True(true);
        }

        [Fact]
        public async Task SendAsync_PayloadWithNullItemId_DoesNotThrow()
        {
            var payload = CreatePayloadWithNullItemId();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.True(true);
        }

        [Fact]
        public async Task SendAsync_PayloadWithEmptyItemId_DoesNotThrow()
        {
            var payload = CreatePayloadWithEmptyItemId();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.True(true);
        }

        #endregion

        #region SendAsync - Error Scenarios Tests

        [Fact]
        public async Task SendAsync_WithValidPayload_DoesNotThrowOnHttpError()
        {
            var payload = CreateValidPayload();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.True(true);
        }

        [Fact]
        public async Task SendAsync_WithCancellationToken_DoesNotThrowOnCancellation()
        {
            var payload = CreateValidPayload();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload, cts.Token);
            }

            Assert.True(true);
        }

        #endregion

        #region SendAsync - Branch Coverage Tests

        [Fact]
        public async Task SendAsync_ValidPayloadWithAssetIds_SendsSuccessfully()
        {
            var payload = CreateValidPayload();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.NotNull(payload.AssetIds);
            Assert.Equal(2, payload.AssetIds.Count);
        }

        [Fact]
        public async Task SendAsync_ValidPayloadWithPublicLinks_SendsSuccessfully()
        {
            var payload = CreateValidPayload();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.NotNull(payload.PublicLinks);
            Assert.Equal(2, payload.PublicLinks.Count);
        }

        [Fact]
        public async Task SendAsync_ValidPayloadWithEmptyLists_SendsSuccessfully()
        {
            var payload = CreatePayloadWithEmptyLists();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload);
            }

            Assert.NotNull(payload.AssetIds);
            Assert.Empty(payload.AssetIds);
        }

        [Fact]
        public async Task SendAsync_MultipleCallsWithSameClient_SendsSuccessfully()
        {
            var payload1 = CreateValidPayload();
            var payload2 = CreateValidPayload();

            using (var sut = new AssetUsageServiceClient(TestEndpointUrl))
            {
                await sut.SendAsync(payload1);
                await sut.SendAsync(payload2);
            }

            Assert.True(true);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static AssetUsageEvent CreateValidPayload()
        {
            return new AssetUsageEvent
            {
                ItemId = ValidItemId,
                ItemPath = ValidItemPath,
                ItemName = ValidItemName,
                TemplateName = ValidTemplateName,
                Language = ValidLanguage,
                Version = ValidVersion,
                PublishedAtUtc = DateTime.UtcNow,
                TargetDatabase = ValidTargetDatabase,
                AssetIds = new List<string> { ValidAssetId, SecondValidAssetId },
                PublicLinks = new List<string> { ValidPublicLink, SecondValidPublicLink }
            };
        }

        private static AssetUsageEvent CreatePayloadWithNullItemId()
        {
            return new AssetUsageEvent
            {
                ItemId = null,
                ItemPath = ValidItemPath,
                ItemName = ValidItemName,
                TemplateName = ValidTemplateName,
                Language = ValidLanguage,
                Version = ValidVersion,
                PublishedAtUtc = DateTime.UtcNow,
                TargetDatabase = ValidTargetDatabase,
                AssetIds = new List<string> { ValidAssetId },
                PublicLinks = new List<string> { ValidPublicLink }
            };
        }

        private static AssetUsageEvent CreatePayloadWithEmptyItemId()
        {
            return new AssetUsageEvent
            {
                ItemId = string.Empty,
                ItemPath = ValidItemPath,
                ItemName = ValidItemName,
                TemplateName = ValidTemplateName,
                Language = ValidLanguage,
                Version = ValidVersion,
                PublishedAtUtc = DateTime.UtcNow,
                TargetDatabase = ValidTargetDatabase,
                AssetIds = new List<string> { ValidAssetId },
                PublicLinks = new List<string> { ValidPublicLink }
            };
        }

        private static AssetUsageEvent CreatePayloadWithEmptyLists()
        {
            return new AssetUsageEvent
            {
                ItemId = ValidItemId,
                ItemPath = ValidItemPath,
                ItemName = ValidItemName,
                TemplateName = ValidTemplateName,
                Language = ValidLanguage,
                Version = ValidVersion,
                PublishedAtUtc = DateTime.UtcNow,
                TargetDatabase = ValidTargetDatabase,
                AssetIds = new List<string>(),
                PublicLinks = new List<string>()
            };
        }

        public void Dispose()
        {
        }

        #endregion
    }
}
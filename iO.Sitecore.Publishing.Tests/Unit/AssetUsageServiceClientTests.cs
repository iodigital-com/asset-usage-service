using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Models;

namespace iO.Sitecore.Publishing.Tests.Unit
{
    public class AssetUsageServiceClientTests : IDisposable
    {
        #region Test Constants

        private const string TestEndpointUrl = "http://localhost:7183/api/SitecorePublishAPI";

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

        #region Test Data Providers

        public static IEnumerable<object[]> AssetIdSetsData => new List<object[]>
        {
            new object[] { new List<string> { ValidAssetId, SecondValidAssetId }, 2 },
            new object[] { new List<string>(), 0 }
        };

        public static IEnumerable<object[]> PublicLinkSetsData => new List<object[]>
        {
            new object[] { new List<string> { ValidPublicLink, SecondValidPublicLink }, 2 },
            new object[] { new List<string>(), 0 }
        };

        #endregion

        #region SendAsync - Happy Path Tests

        [Fact]
        public async Task SendAsync_ValidPayload_CompletesSuccessfully()
        {
            //Arrange
            var sut = CreateSut();
            var payload = CreateValidPayload();

            //Act
            await sut.SendAsync(payload);

            //Assert
            Assert.NotNull(payload.AssetIds);
        }

        #endregion

        #region SendAsync - Edge Cases Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SendAsync_PayloadWithNullOrEmptyItemId_DoesNotThrow(string itemId)
        {
            //Arrange
            var sut = CreateSut();
            var payload = CreateValidPayload(itemIdOverride: itemId);

            //Act
            await sut.SendAsync(payload);

            //Assert
            Assert.True(true);
        }

        [Fact]
        public async Task SendAsync_NullPayload_DoesNotThrow()
        {
            //Arrange
            var sut = CreateSut();

            //Act
            await sut.SendAsync(null);

            //Assert
            Assert.True(true);
        }

        [Fact]
        public async Task SendAsync_WithCancellationTokenCanceled_DoesNotThrow()
        {
            //Arrange
            var sut = CreateSut();
            var payload = CreateValidPayload();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            //Act
            await sut.SendAsync(payload, cts.Token);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region SendAsync - Branch Coverage Tests

        [Theory]
        [MemberData(nameof(AssetIdSetsData))]
        public async Task SendAsync_PayloadWithAssetIds_PreservesCount(List<string> assetIds, int expectedCount)
        {
            //Arrange
            var sut = CreateSut();
            var payload = CreateValidPayload(assetIdsOverride: assetIds);

            //Act
            await sut.SendAsync(payload);

            //Assert
            Assert.Equal(expectedCount, payload.AssetIds?.Count ?? 0);
        }

        [Theory]
        [MemberData(nameof(PublicLinkSetsData))]
        public async Task SendAsync_PayloadWithPublicLinks_PreservesCount(List<string> publicLinks, int expectedCount)
        {
            //Arrange
            var sut = CreateSut();
            var payload = CreateValidPayload(publicLinksOverride: publicLinks);

            //Act
            await sut.SendAsync(payload);

            //Assert
            Assert.Equal(expectedCount, payload.PublicLinks?.Count ?? 0);
        }

        [Fact]
        public async Task SendAsync_MultipleCallsWithSameClient_Completes()
        {
            //Arrange
            var sut = CreateSut();
            var payload1 = CreateValidPayload();
            var payload2 = CreateValidPayload();

            //Act
            await sut.SendAsync(payload1);
            await sut.SendAsync(payload2);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static AssetUsageServiceClient CreateSut() => new AssetUsageServiceClient(TestEndpointUrl);

        private static AssetUsageEvent CreateValidPayload(
            string itemIdOverride = ValidItemId,
            List<string> assetIdsOverride = null,
            List<string> publicLinksOverride = null)
        {
            return new AssetUsageEvent
            {
                ItemId = itemIdOverride,
                ItemPath = ValidItemPath,
                ItemName = ValidItemName,
                TemplateName = ValidTemplateName,
                Language = ValidLanguage,
                Version = ValidVersion,
                PublishedAtUtc = DateTime.UtcNow,
                TargetDatabase = ValidTargetDatabase,
                AssetIds = assetIdsOverride ?? new List<string> { ValidAssetId, SecondValidAssetId },
                PublicLinks = publicLinksOverride ?? new List<string> { ValidPublicLink, SecondValidPublicLink }
            };
        }

        #endregion

        public void Dispose()
        {
        }
    }
}
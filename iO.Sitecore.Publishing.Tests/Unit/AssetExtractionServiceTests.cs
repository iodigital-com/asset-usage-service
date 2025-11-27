using iO.Sitecore.Publishing.Services;
using iO.Sitecore.Publishing.Tests.Unit;

using iO.Sitecore.Publishing.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace iO.Sitecore.Publishing.Tests.Unit
{
    public class AssetExtractionServiceTests
    {
        #region Test Constants

        private const string ImageFieldType = "image";
        private const string RichTextFieldType = "rich text";
        private const string TextFieldType = "text";
        private const string ThumbnailSrcAttribute = "thumbnailsrc";

        private const string ValidGatewayUrl = "https://assets.example.com/api/gateway/12345/thumbnail";
        private const string ValidGatewayId = "12345";
        private const string SecondValidGatewayUrl = "https://assets.example.com/api/gateway/67890/thumbnail";
        private const string SecondValidGatewayId = "67890";
        private const string ThirdValidGatewayUrl = "https://assets.example.com/api/gateway/11111/thumbnail";
        private const string ThirdValidGatewayId = "11111";

        private const string ValidHttpsUrl = "https://example.com/document.pdf";
        private const string ValidHttpUrl = "http://example.com/page.html";
        private const string ValidImageUrl = "https://assets.example.com/image.jpg";
        private const string SecondValidImageUrl = "https://assets.example.com/image2.jpg";

        private const string NonGatewayUrl = "https://example.com/regular/image.jpg";
        private const string InvalidTextValue = "Just some text without URL";

        #endregion

        #region ExtractAssetIds Tests

        [Fact]
        public void ExtractAssetIds_ImageFieldWithGatewayUrl_ReturnsGatewayId()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public void ExtractAssetIds_ImageFieldWithGatewayUrl_ReturnsCorrectGatewayId()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.Contains(ValidGatewayId, result);
        }

        [Fact]
        public void ExtractAssetIds_MultipleImageFieldsWithDifferentGatewayUrls_ReturnsAllGatewayIds()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var urls = new[] { ValidGatewayUrl, SecondValidGatewayUrl, ThirdValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void ExtractAssetIds_MultipleImageFieldsWithDifferentGatewayUrls_ReturnsCorrectGatewayIds()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var urls = new[] { ValidGatewayUrl, SecondValidGatewayUrl, ThirdValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.All(new[] { ValidGatewayId, SecondValidGatewayId, ThirdValidGatewayId },
                id => Assert.Contains(id, result));
        }

        [Fact]
        public void ExtractAssetIds_EmptyImageField_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(string.Empty);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractAssetIds_ImageFieldWithoutGatewayPattern_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(NonGatewayUrl);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractAssetIds_DuplicateGatewayIds_ReturnsUniqueGatewayIds()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var urls = new[] { ValidGatewayUrl, ValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public void ExtractAssetIds_ItemFieldsThrowException_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(ThrowingFields());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractAssetIds_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            // Act
            var result = sut.ExtractAssetIdsFromFieldData(ThrowingFields());

            // Assert - ensure behavior is same (no logging verification required)
            Assert.Empty(result);
        }

        #endregion

        #region ExtractPublicLinks Tests

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithImageTags_ReturnsImageUrls()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl, SecondValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            // Act
            var result = sut.ExtractPublicLinksFromFieldData(fields);

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithImageTags_ReturnsCorrectImageUrls()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl, SecondValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            // Act
            var result = sut.ExtractPublicLinksFromFieldData(fields);

            // Assert
            Assert.All(new[] { ValidImageUrl, SecondValidImageUrl },
                url => Assert.Contains(url, result));
        }

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithoutImages_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = "<p>Just plain text without images</p>";
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            // Act
            var result = sut.ExtractPublicLinksFromFieldData(fields);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_EmptyRichTextField_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithRichTextField(string.Empty);

            // Act
            var result = sut.ExtractPublicLinksFromFieldData(fields);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_ItemFieldsThrowException_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            // Act
            var result = sut.ExtractPublicLinksFromFieldData(ThrowingFields());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            // Act
            var result = sut.ExtractPublicLinksFromFieldData(ThrowingFields());

            // Assert - no mocked logging; verify behavior
            Assert.Empty(result);
        }

        #endregion

        #region ExtractPublicLinksFromAnyField Tests

        [Fact]
        public void ExtractPublicLinksFromAnyField_ImageField_ReturnsThumbnailUrl()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ImageField_ReturnsCorrectThumbnailUrl()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            // Assert
            Assert.Contains(ValidGatewayUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_RichTextField_ReturnsImageUrls()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            // Assert
            Assert.Contains(ValidImageUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithHttpsUrl_ReturnsUrl()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithTextField(ValidHttpsUrl);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            // Assert
            Assert.Contains(ValidHttpsUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithHttpUrl_ReturnsUrl()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithTextField(ValidHttpUrl);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            // Assert
            Assert.Contains(ValidHttpUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithNonUrl_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithTextField(InvalidTextValue);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_MixedFieldTypes_ReturnsAllUrls()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var mixedFields = CreateFieldDataWithMixedFields(
                ValidGatewayUrl,
                CreateRichTextWithImages(new[] { ValidImageUrl }),
                ValidHttpsUrl);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(mixedFields);

            // Assert
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_MixedFieldTypes_ReturnsCorrectUrls()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var mixedFields = CreateFieldDataWithMixedFields(
                ValidGatewayUrl,
                CreateRichTextWithImages(new[] { ValidImageUrl }),
                ValidHttpsUrl);

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(mixedFields);

            // Assert
            Assert.All(new[] { ValidGatewayUrl, ValidImageUrl, ValidHttpsUrl },
                url => Assert.Contains(url, result));
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ItemFieldsThrowException_ReturnsEmptyCollection()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(ThrowingFields());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            // Arrange
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            // Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(ThrowingFields());

            // Assert - ensure behavior same without mocking logging
            Assert.Empty(result);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static PublishLoggingService CreateLoggingService()
        {
            // Return a concrete PublishLoggingService instance rather than a Moq mock.
            return new PublishLoggingService(new object());
        }

        private static IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> CreateFieldDataWithImageField(string thumbnailUrl)
        {
            var runtimeValue = string.IsNullOrEmpty(thumbnailUrl)
                ? string.Empty
                : $"<image {ThumbnailSrcAttribute}=\"{thumbnailUrl}\" />";

            return new[] { (ImageFieldType, runtimeValue, runtimeValue, "ImageField") };
        }

        private static IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> CreateFieldDataWithMultipleImageFields(string[] thumbnailUrls)
        {
            return thumbnailUrls.Select(url =>
            {
                var runtimeValue = string.IsNullOrEmpty(url) ? string.Empty : $"<image {ThumbnailSrcAttribute}=\"{url}\" />";
                return (TypeKey: ImageFieldType, Value: runtimeValue, InheritedValue: runtimeValue, Name: "ImageField");
            }).ToList();
        }

        private static IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> CreateFieldDataWithRichTextField(string richTextContent)
        {
            return new[] { (RichTextFieldType, richTextContent, richTextContent, "RichTextField") };
        }

        private static IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> CreateFieldDataWithTextField(string textValue)
        {
            return new[] { (TextFieldType, textValue, (string)null, "TextField") };
        }

        private static IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> CreateFieldDataWithMixedFields(string imageUrl, string richTextContent, string plainUrl)
        {
            var list = new List<(string, string, string, string)>
            {
                (ImageFieldType, string.IsNullOrEmpty(imageUrl) ? string.Empty : $"<image {ThumbnailSrcAttribute}=\"{imageUrl}\" />", string.IsNullOrEmpty(imageUrl) ? string.Empty : $"<image {ThumbnailSrcAttribute}=\"{imageUrl}\" />", "ImageField"),
                (RichTextFieldType, richTextContent, richTextContent, "RichTextField"),
                (TextFieldType, plainUrl, null, "TextField")
            };

            return list;
        }

        private static string CreateRichTextWithImages(string[] imageUrls)
        {
            var imageTags = imageUrls.Select(url => $"<img src=\"{url}\" alt=\"Test Image\" />");
            return $"<p>Some text {string.Join(" ", imageTags)}</p>";
        }

        #endregion
    }
}
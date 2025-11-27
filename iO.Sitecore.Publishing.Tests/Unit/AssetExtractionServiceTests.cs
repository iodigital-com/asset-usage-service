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
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.Single(result);
        }

        [Fact]
        public void ExtractAssetIds_ImageFieldWithGatewayUrl_ReturnsCorrectGatewayId()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.Contains(ValidGatewayId, result);
        }

        [Fact]
        public void ExtractAssetIds_MultipleImageFieldsWithDifferentGatewayUrls_ReturnsAllGatewayIds()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var urls = new[] { ValidGatewayUrl, SecondValidGatewayUrl, ThirdValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void ExtractAssetIds_MultipleImageFieldsWithDifferentGatewayUrls_ReturnsCorrectGatewayIds()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var urls = new[] { ValidGatewayUrl, SecondValidGatewayUrl, ThirdValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.All(new[] { ValidGatewayId, SecondValidGatewayId, ThirdValidGatewayId },
                id => Assert.Contains(id, result));
        }

        [Fact]
        public void ExtractAssetIds_EmptyImageField_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(string.Empty);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractAssetIds_ImageFieldWithoutGatewayPattern_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(NonGatewayUrl);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractAssetIds_DuplicateGatewayIds_ReturnsUniqueGatewayIds()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var urls = new[] { ValidGatewayUrl, ValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            var result = sut.ExtractAssetIdsFromFieldData(fields);

            Assert.Single(result);
        }

        [Fact]
        public void ExtractAssetIds_ItemFieldsThrowException_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            var result = sut.ExtractAssetIdsFromFieldData(ThrowingFields());

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractAssetIds_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            var result = sut.ExtractAssetIdsFromFieldData(ThrowingFields());

            Assert.Empty(result);
        }

        #endregion

        #region ExtractPublicLinks Tests

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithImageTags_ReturnsImageUrls()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl, SecondValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            var result = sut.ExtractPublicLinksFromFieldData(fields);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithImageTags_ReturnsCorrectImageUrls()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl, SecondValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            var result = sut.ExtractPublicLinksFromFieldData(fields);

            Assert.All(new[] { ValidImageUrl, SecondValidImageUrl },
                url => Assert.Contains(url, result));
        }

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithoutImages_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = "<p>Just plain text without images</p>";
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            var result = sut.ExtractPublicLinksFromFieldData(fields);

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_EmptyRichTextField_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithRichTextField(string.Empty);

            var result = sut.ExtractPublicLinksFromFieldData(fields);

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_ItemFieldsThrowException_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            var result = sut.ExtractPublicLinksFromFieldData(ThrowingFields());

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            var result = sut.ExtractPublicLinksFromFieldData(ThrowingFields());

            Assert.Empty(result);
        }

        #endregion

        #region ExtractPublicLinksFromAnyField Tests

        [Fact]
        public void ExtractPublicLinksFromAnyField_ImageField_ReturnsThumbnailUrl()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            Assert.Single(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ImageField_ReturnsCorrectThumbnailUrl()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            Assert.Contains(ValidGatewayUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_RichTextField_ReturnsImageUrls()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            Assert.Contains(ValidImageUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithHttpsUrl_ReturnsUrl()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithTextField(ValidHttpsUrl);

            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            Assert.Contains(ValidHttpsUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithHttpUrl_ReturnsUrl()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithTextField(ValidHttpUrl);

            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            Assert.Contains(ValidHttpUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithNonUrl_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var fields = CreateFieldDataWithTextField(InvalidTextValue);

            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_MixedFieldTypes_ReturnsAllUrls()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var mixedFields = CreateFieldDataWithMixedFields(
                ValidGatewayUrl,
                CreateRichTextWithImages(new[] { ValidImageUrl }),
                ValidHttpsUrl);

            var result = sut.ExtractPublicLinksFromAnyFieldData(mixedFields);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_MixedFieldTypes_ReturnsCorrectUrls()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            var mixedFields = CreateFieldDataWithMixedFields(
                ValidGatewayUrl,
                CreateRichTextWithImages(new[] { ValidImageUrl }),
                ValidHttpsUrl);

            var result = sut.ExtractPublicLinksFromAnyFieldData(mixedFields);

            Assert.All(new[] { ValidGatewayUrl, ValidImageUrl, ValidHttpsUrl },
                url => Assert.Contains(url, result));
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ItemFieldsThrowException_ReturnsEmptyCollection()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            var result = sut.ExtractPublicLinksFromAnyFieldData(ThrowingFields());

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            var loggingService = CreateLoggingService();
            var sut = new AssetExtractionService(loggingService);
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            var result = sut.ExtractPublicLinksFromAnyFieldData(ThrowingFields());

            Assert.Empty(result);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static PublishLoggingService CreateLoggingService()
        {
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
            var imageFieldValue = string.IsNullOrEmpty(imageUrl) ? string.Empty : $"<image {ThumbnailSrcAttribute}=\"{imageUrl}\" />";

            return new List<(string, string, string, string)>
            {
                (ImageFieldType, imageFieldValue, imageFieldValue, "ImageField"),
                (RichTextFieldType, richTextContent, richTextContent, "RichTextField"),
                (TextFieldType, plainUrl, null, "TextField")
            };
        }

        private static string CreateRichTextWithImages(string[] imageUrls)
        {
            var imageTags = imageUrls.Select(url => $"<img src=\"{url}\" alt=\"Test Image\" />");
            return $"<p>Some text {string.Join(" ", imageTags)}</p>";
        }

        #endregion
    }
}
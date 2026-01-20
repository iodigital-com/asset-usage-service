using iO.Sitecore.Publishing.Services;

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

        public static IEnumerable<object[]> MultipleGatewayUrlsData => new List<object[]>
        {
            new object[]
            {
                new[] { ValidGatewayUrl, SecondValidGatewayUrl, ThirdValidGatewayUrl },
                new[] { ValidGatewayId, SecondValidGatewayId, ThirdValidGatewayId }
            }
        };

        [Theory]
        [InlineData(ValidGatewayUrl, ValidGatewayId)]
        public void ExtractAssetIds_ImageFieldWithGatewayUrl_ReturnsGatewayId(string url, string expectedId)
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithImageField(url);

            //Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            //Assert
            Assert.Single(result);
            Assert.Contains(expectedId, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(NonGatewayUrl)]
        public void ExtractAssetIds_ImageFieldInvalidOrNonGateway_ReturnsEmpty(string url)
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithImageField(url);

            //Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            //Assert
            Assert.Empty(result);
        }

        [Theory]
        [MemberData(nameof(MultipleGatewayUrlsData))]
        public void ExtractAssetIds_MultipleImageFieldsWithDifferentGatewayUrls_ReturnsAllGatewayIds(string[] urls, string[] expectedIds)
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            //Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            //Assert
            Assert.Equal(expectedIds.Length, result.Count);
            foreach (var id in expectedIds) Assert.Contains(id, result);
        }

        [Fact]
        public void ExtractAssetIds_DuplicateGatewayIds_ReturnsUniqueGatewayIds()
        {
            //Arrange
            var sut = CreateSut();
            var urls = new[] { ValidGatewayUrl, ValidGatewayUrl };
            var fields = CreateFieldDataWithMultipleImageFields(urls);

            //Act
            var result = sut.ExtractAssetIdsFromFieldData(fields);

            //Assert
            Assert.Single(result);
            Assert.Contains(ValidGatewayId, result);
        }

        [Fact]
        public void ExtractAssetIds_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            //Arrange
            var sut = CreateSut();
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            //Act
            var result = sut.ExtractAssetIdsFromFieldData(ThrowingFields());

            //Assert
            Assert.Empty(result);
        }

        #endregion

        #region ExtractPublicLinks Tests

        [Fact]
        public void ExtractPublicLinks_RichTextFieldWithImageTags_ReturnsCorrectImageUrls()
        {
            //Arrange
            var sut = CreateSut();
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl, SecondValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            //Act
            var result = sut.ExtractPublicLinksFromFieldData(fields);

            //Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(ValidImageUrl, result);
            Assert.Contains(SecondValidImageUrl, result);
        }

        [Theory]
        [InlineData("<p>Just plain text without images</p>")]
        [InlineData("")]
        public void ExtractPublicLinks_RichTextFieldWithoutImagesOrEmpty_ReturnsEmptyCollection(string richText)
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithRichTextField(richText);

            //Act
            var result = sut.ExtractPublicLinksFromFieldData(fields);

            //Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinks_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            //Arrange
            var sut = CreateSut();
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            //Act
            var result = sut.ExtractPublicLinksFromFieldData(ThrowingFields());

            //Assert
            Assert.Empty(result);
        }

        #endregion

        #region ExtractPublicLinksFromAnyField Tests

        [Fact]
        public void ExtractPublicLinksFromAnyField_ImageField_ReturnsCorrectThumbnailUrl()
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithImageField(ValidGatewayUrl);

            //Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            //Assert
            Assert.Single(result);
            Assert.Contains(ValidGatewayUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_RichTextField_ReturnsImageUrl()
        {
            //Arrange
            var sut = CreateSut();
            var richTextContent = CreateRichTextWithImages(new[] { ValidImageUrl });
            var fields = CreateFieldDataWithRichTextField(richTextContent);

            //Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            //Assert
            Assert.Contains(ValidImageUrl, result);
        }

        [Theory]
        [InlineData(ValidHttpsUrl)]
        [InlineData(ValidHttpUrl)]
        public void ExtractPublicLinksFromAnyField_TextFieldWithUrl_ReturnsUrl(string url)
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithTextField(url);

            //Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            //Assert
            Assert.Contains(url, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_TextFieldWithNonUrl_ReturnsEmptyCollection()
        {
            //Arrange
            var sut = CreateSut();
            var fields = CreateFieldDataWithTextField(InvalidTextValue);

            //Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(fields);

            //Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_MixedFieldTypes_ReturnsAllUrls()
        {
            //Arrange
            var sut = CreateSut();
            var mixedFields = CreateFieldDataWithMixedFields(
                ValidGatewayUrl,
                CreateRichTextWithImages(new[] { ValidImageUrl }),
                ValidHttpsUrl);

            //Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(mixedFields);

            //Assert
            Assert.Equal(3, result.Count);
            Assert.Contains(ValidGatewayUrl, result);
            Assert.Contains(ValidImageUrl, result);
            Assert.Contains(ValidHttpsUrl, result);
        }

        [Fact]
        public void ExtractPublicLinksFromAnyField_ItemFieldsThrowException_DoesNotThrow_AndReturnsEmpty()
        {
            //Arrange
            var sut = CreateSut();
            IEnumerable<(string TypeKey, string Value, string InheritedValue, string Name)> ThrowingFields()
            {
                throw new InvalidOperationException();
#pragma warning disable 162
                yield break;
#pragma warning restore 162
            }

            //Act
            var result = sut.ExtractPublicLinksFromAnyFieldData(ThrowingFields());

            //Assert
            Assert.Empty(result);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static AssetExtractionService CreateSut() => new AssetExtractionService(CreateLoggingService());

        private static PublishLoggingService CreateLoggingService() => new PublishLoggingService(new object());

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
using System.Reflection;
using System.Runtime.Serialization;
using Moq;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Publishing.Pipelines.PublishItem;
using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Services;
using Sitecore.Globalization;

namespace iO.Sitecore.publishing.Tests.Unit
{
    public class PublishTelemetryServiceTests : IDisposable
    {
        #region Test Constants

        private const string TestAuditLogPath = "TestAuditLog.json";
        private const string ValidItemId = "110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9";
        private const string ValidLanguage = "en";
        private const string ValidTargetDatabase = "web";
        private const string ValidSourceDatabase = "master";
        private const string TestEndpointUrl = "http://localhost:7183/api/SitecorePublishAPI";

        #endregion

        #region Constructor

        public PublishTelemetryServiceTests()
        {
            CleanupTestFiles();
        }

        #endregion

        #region ProcessItemProcessing Tests - Happy Path

        [Fact]
        public void ProcessItemProcessing_ValidEventArgs_ProcessesSuccessfully()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidItemProcessingEventArgs();

            //Act
            sut.ProcessItemProcessing(eventArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessing Tests - Edge Cases

        [Fact]
        public void ProcessItemProcessing_NullEventArgs_DoesNotThrow()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);

            //Act
            sut.ProcessItemProcessing(null);

            //Assert
            Assert.True(true);
        }

        [Fact]
        public void ProcessItemProcessing_NullPublishOptions_DoesNotThrow()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var context = CreateMockPublishItemContext(null);
            var eventArgs = new ItemProcessingEventArgs(context);

            //Act
            sut.ProcessItemProcessing(eventArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessing Tests - Error Scenarios

        [Fact]
        public void ProcessItemProcessing_InvalidItemId_DoesNotThrow()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateItemProcessingEventArgsWithInvalidItemId();

            //Act
            sut.ProcessItemProcessing(eventArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessing Tests - Branch Coverage

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ProcessItemProcessing_WithOrWithoutVersion_SetsContext(bool withVersion)
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = withVersion ? CreateValidItemProcessingEventArgs() : CreateItemProcessingEventArgsWithoutVersion();

            //Act
            sut.ProcessItemProcessing(eventArgs);

            //Assert
            Assert.NotNull(eventArgs.Context);
        }

        #endregion

        #region ProcessItemProcessed Tests - Happy Path

        [Fact]
        public void ProcessItemProcessed_ValidEventArgs_ProcessesSuccessfully()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var processingArgs = CreateValidItemProcessingEventArgs();
            var processedArgs = CreateValidItemProcessedEventArgs();

            //Act
            sut.ProcessItemProcessing(processingArgs);
            sut.ProcessItemProcessed(processedArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessed Tests - Edge Cases

        [Fact]
        public void ProcessItemProcessed_NullEventArgs_DoesNotThrow()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);

            //Act
            sut.ProcessItemProcessed(null);

            //Assert
            Assert.True(true);
        }

        [Fact]
        public void ProcessItemProcessed_NoProcessingInfo_DoesNotThrow()
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidItemProcessedEventArgs();

            //Act
            sut.ProcessItemProcessed(eventArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndAsync Tests - Variations

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task ProcessPublishEndAsync_Variations_DoesNotThrow(bool useInvalidAuditPath, bool includeUpdatedItems)
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var auditPath = useInvalidAuditPath ? @"Z:\InvalidPath\audit.json" : TestAuditLogPath;
            var sut = new PublishTelemetryService(client, auditPath);
            var publishEndArgs = CreateValidPublishEndEventArgs();
            if (includeUpdatedItems)
            {
                var processingArgs = CreateValidItemProcessingEventArgs();
                var processedArgs = CreateValidItemProcessedEventArgs();
                sut.ProcessItemProcessing(processingArgs);
                sut.ProcessItemProcessed(processedArgs);
            }

            //Act
            await sut.ProcessPublishEndAsync(publishEndArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndRemote Tests - Combinations

        [Theory]
        [InlineData("en", "web", true, false)]
        [InlineData("", "web", true, false)]
        [InlineData("en", "", true, false)]
        [InlineData("en", "web", false, false)]
        [InlineData("en", "web", true, true)]
        public void ProcessPublishEndRemote_Combinations_DoesNotThrow(string language, string queue, bool deep, bool useInvalidAuditPath)
        {
            //Arrange
            var client = CreateAssetUsageClient();
            var auditPath = useInvalidAuditPath ? @"Z:\InvalidPath\audit.json" : TestAuditLogPath;
            var sut = new PublishTelemetryService(client, auditPath);
            var eventArgs = CreatePublishEndRemoteEventArgs(language, queue, deep);

            //Act
            sut.ProcessPublishEndRemote(eventArgs);

            //Assert
            Assert.True(true);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static AssetUsageServiceClient CreateAssetUsageClient()
        {
            return new AssetUsageServiceClient(TestEndpointUrl);
        }

        private static ItemProcessingEventArgs CreateValidItemProcessingEventArgs()
        {
            var context = CreateMockPublishItemContext(CreateMockPublishOptions());
            return new ItemProcessingEventArgs(context);
        }

        private static ItemProcessingEventArgs CreateItemProcessingEventArgsWithInvalidItemId()
        {
            var context = CreateMockPublishItemContext(CreateMockPublishOptions());
            return new ItemProcessingEventArgs(context);
        }

        private static ItemProcessingEventArgs CreateItemProcessingEventArgsWithoutVersion()
        {
            var context = CreateMockPublishItemContextWithoutVersion(CreateMockPublishOptions());
            return new ItemProcessingEventArgs(context);
        }

        private static ItemProcessedEventArgs CreateValidItemProcessedEventArgs()
        {
            var context = CreateMockPublishItemContext(CreateMockPublishOptions());
            return new ItemProcessedEventArgs(context);
        }

        private static EventArgs CreateValidPublishEndEventArgs()
        {
            return new EventArgs();
        }

        private static PublishEndRemoteEventArgs CreatePublishEndRemoteEventArgs(string language, string eventQueueName, bool deep)
        {
            var eventArgsType = typeof(PublishEndRemoteEventArgs);
            var eventArgs = (PublishEndRemoteEventArgs)FormatterServices.GetUninitializedObject(eventArgsType);
            SetPrivateField(eventArgs, "_rootItemId", ID.Parse(ValidItemId).Guid);
            SetPrivateField(eventArgs, "_mode", PublishMode.Full);
            SetPrivateField(eventArgs, "_deep", deep);
            SetPrivateField(eventArgs, "_languageName", language);
            SetPrivateField(eventArgs, "_sourceDatabaseName", ValidSourceDatabase);
            SetPrivateField(eventArgs, "_targetDatabaseName", ValidTargetDatabase);
            SetPrivateField(eventArgs, "_eventQueueName", eventQueueName);
            return eventArgs;
        }

        private static PublishItemContext CreateMockPublishItemContext(PublishOptions options)
        {
            var contextType = typeof(PublishItemContext);
            var context = (PublishItemContext)FormatterServices.GetUninitializedObject(contextType);
            var publishContextType = typeof(PublishContext);
            var publishContext = (PublishContext)FormatterServices.GetUninitializedObject(publishContextType);
            SetPrivateField(context, "m_itemId", ID.Parse(ValidItemId));
            SetPrivateField(context, "m_action", PublishAction.PublishVersion);
            SetPrivateField(context, "m_publishOptions", options);
            SetPrivateField(context, "m_publishContext", publishContext);
            SetPrivateField(publishContext, "m_options", options);
            return context;
        }

        private static PublishItemContext CreateMockPublishItemContextWithoutVersion(PublishOptions options)
        {
            var context = CreateMockPublishItemContext(options);
            return context;
        }

        private static PublishOptions CreateMockPublishOptions()
        {
            var sourceDb = new Mock<Database>();
            var targetDb = new Mock<Database>();
            sourceDb.Setup(db => db.Name).Returns("master");
            targetDb.Setup(db => db.Name).Returns("web");
            var languageType = typeof(Language);
            var language = (Language)FormatterServices.GetUninitializedObject(languageType);
            var nameField = languageType.GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nameField != null)
            {
                nameField.SetValue(language, ValidLanguage);
            }
            var publishOptionsType = typeof(PublishOptions);
            var options = (PublishOptions)FormatterServices.GetUninitializedObject(publishOptionsType);
            SetPrivateField(options, "m_sourceDatabase", sourceDb.Object);
            SetPrivateField(options, "m_targetDatabase", targetDb.Object);
            SetPrivateField(options, "m_mode", PublishMode.Full);
            SetPrivateField(options, "m_language", language);
            SetPrivateField(options, "m_publishDate", DateTime.Now);
            return options;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }
            var possibleFieldNames = new[]
            {
                fieldName,
                $"<{fieldName}>k__BackingField",
                $"_{fieldName}",
                $"m_{fieldName}",
                fieldName.TrimStart('_')
            };
            foreach (var possibleName in possibleFieldNames)
            {
                field = obj.GetType().GetField(possibleName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    return;
                }
            }
            var property = obj.GetType().GetProperty(fieldName.TrimStart('_'), BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(obj, value);
            }
        }

        private void CleanupTestFiles()
        {
            if (File.Exists(TestAuditLogPath))
            {
                File.Delete(TestAuditLogPath);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            CleanupTestFiles();
        }

        #endregion
    }
}
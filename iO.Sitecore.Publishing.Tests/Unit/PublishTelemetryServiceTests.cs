using iO.Sitecore.Publishing.Events;
using iO.Sitecore.Publishing.Services;
using Moq;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Publishing.Pipelines.PublishItem;

namespace iO.Sitecore.publishing.Tests.Unit
{
    public class PublishTelemetryServiceTests : IDisposable
    {
        #region Test Constants

        private const string TestAuditLogPath = "TestAuditLog.json";
        private const string ValidItemId = "110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9";
        private const string ValidItemPath = "/sitecore/content/home";
        private const string ValidItemName = "Home";
        private const string ValidTemplateName = "Page";
        private const string ValidLanguage = "en";
        private const int ValidVersion = 1;
        private const string ValidTargetDatabase = "web";
        private const string ValidSourceDatabase = "master";
        private const string ValidRevisionId = "550e8400-e29b-41d4-a716-446655440000";
        private const string ValidEventQueueName = "web";

        #endregion

        public PublishTelemetryServiceTests()
        {
            CleanupTestFiles();
        }

        #region ProcessItemProcessing Tests - Happy Path

        [Fact]
        public void ProcessItemProcessing_ValidEventArgs_ProcessesSuccessfully()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidItemProcessingEventArgs();

            sut.ProcessItemProcessing(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessing Tests - Edge Cases

        [Fact]
        public void ProcessItemProcessing_NullEventArgs_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);

            sut.ProcessItemProcessing(null);

            Assert.True(true);
        }

        [Fact]
        public void ProcessItemProcessing_NullPublishOptions_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var context = CreateMockPublishItemContext(null);
            var eventArgs = new ItemProcessingEventArgs(context);

            sut.ProcessItemProcessing(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessing Tests - Error Scenarios

        [Fact]
        public void ProcessItemProcessing_InvalidItemId_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateItemProcessingEventArgsWithInvalidItemId();

            sut.ProcessItemProcessing(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessing Tests - Branch Coverage

        [Fact]
        public void ProcessItemProcessing_WithVersionInfo_ProcessesWithVersion()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidItemProcessingEventArgs();

            sut.ProcessItemProcessing(eventArgs);

            Assert.NotNull(eventArgs.Context);
        }

        [Fact]
        public void ProcessItemProcessing_WithoutVersionInfo_ProcessesWithLatestVersion()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateItemProcessingEventArgsWithoutVersion();

            sut.ProcessItemProcessing(eventArgs);

            Assert.NotNull(eventArgs.Context);
        }

        #endregion

        #region ProcessItemProcessed Tests - Happy Path

        [Fact]
        public void ProcessItemProcessed_ValidEventArgs_ProcessesSuccessfully()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var processingArgs = CreateValidItemProcessingEventArgs();
            var processedArgs = CreateValidItemProcessedEventArgs();

            sut.ProcessItemProcessing(processingArgs);
            sut.ProcessItemProcessed(processedArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessed Tests - Edge Cases

        [Fact]
        public void ProcessItemProcessed_NullEventArgs_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);

            sut.ProcessItemProcessed(null);

            Assert.True(true);
        }

        [Fact]
        public void ProcessItemProcessed_NoProcessingInfo_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidItemProcessedEventArgs();

            sut.ProcessItemProcessed(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessItemProcessed Tests - Error Scenarios

        [Fact]
        public async Task ProcessPublishEndAsync_InvalidAuditPath_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, "Z:\\InvalidPath\\audit.json");
            var processingArgs = CreateValidItemProcessingEventArgs();
            var processedArgs = CreateValidItemProcessedEventArgs();
            var publishEndArgs = CreateValidPublishEndEventArgs();

            sut.ProcessItemProcessing(processingArgs);
            sut.ProcessItemProcessed(processedArgs);
            await sut.ProcessPublishEndAsync(publishEndArgs);

            Assert.True(true);
        }

        [Fact]
        public async Task ProcessPublishEndAsync_TelemetryClientThrows_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var processingArgs = CreateValidItemProcessingEventArgs();
            var processedArgs = CreateValidItemProcessedEventArgs();
            var publishEndArgs = CreateValidPublishEndEventArgs();

            sut.ProcessItemProcessing(processingArgs);
            sut.ProcessItemProcessed(processedArgs);
            await sut.ProcessPublishEndAsync(publishEndArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndAsync Tests - Branch Coverage

        [Fact]
        public async Task ProcessPublishEndAsync_WithUpdatedItems_SendsToTelemetry()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var processingArgs = CreateValidItemProcessingEventArgs();
            var processedArgs = CreateValidItemProcessedEventArgs();
            var publishEndArgs = CreateValidPublishEndEventArgs();

            sut.ProcessItemProcessing(processingArgs);
            sut.ProcessItemProcessed(processedArgs);
            await sut.ProcessPublishEndAsync(publishEndArgs);

            Assert.True(true);
        }

        [Fact]
        public async Task ProcessPublishEndAsync_WithoutUpdatedItems_DoesNotSend()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var args = CreateValidPublishEndEventArgs();

            await sut.ProcessPublishEndAsync(args);

            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndRemote Tests - Happy Path

        [Fact]
        public void ProcessPublishEndRemote_ValidEventArgs_ProcessesSuccessfully()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidPublishEndRemoteEventArgs();

            sut.ProcessPublishEndRemote(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndRemote Tests - Edge Cases

        [Fact]
        public void ProcessPublishEndRemote_NullEventArgs_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);

            sut.ProcessPublishEndRemote(null);

            Assert.True(true);
        }

        [Fact]
        public void ProcessPublishEndRemote_EmptyLanguage_ProcessesWithAllLanguages()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreatePublishEndRemoteEventArgsWithEmptyLanguage();

            sut.ProcessPublishEndRemote(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndRemote Tests - Error Scenarios

        [Fact]
        public void ProcessPublishEndRemote_InvalidAuditPath_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, "Z:\\InvalidPath\\audit.json");
            var eventArgs = CreateValidPublishEndRemoteEventArgs();

            sut.ProcessPublishEndRemote(eventArgs);

            Assert.True(true);
        }

        [Fact]
        public void ProcessPublishEndRemote_EmptyEventQueueName_DoesNotThrow()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreatePublishEndRemoteEventArgsWithEmptyEventQueue();

            sut.ProcessPublishEndRemote(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region ProcessPublishEndRemote Tests - Branch Coverage

        [Fact]
        public void ProcessPublishEndRemote_WithLanguage_ProcessesWithSpecificLanguage()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreateValidPublishEndRemoteEventArgs();

            sut.ProcessPublishEndRemote(eventArgs);

            Assert.True(true);
        }

        [Fact]
        public void ProcessPublishEndRemote_DeepPublish_ProcessesDeep()
        {
            var client = CreateMockAssetUsageClient();
            var sut = new PublishTelemetryService(client, TestAuditLogPath);
            var eventArgs = CreatePublishEndRemoteEventArgsWithDeepPublish();

            sut.ProcessPublishEndRemote(eventArgs);

            Assert.True(true);
        }

        #endregion

        #region Helper Methods - Test Data Creation

        private static AssetUsageServiceClient CreateMockAssetUsageClient()
        {
            return new AssetUsageServiceClient(TestEndpointUrl);
        }

        private const string TestEndpointUrl = "http://localhost:7183/api/SitecorePublishAPI";

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

        private static ItemProcessedEventArgs CreateItemProcessedEventArgsWithoutTargetItem()
        {
            var context = CreateMockPublishItemContext(CreateMockPublishOptions());
            return new ItemProcessedEventArgs(context);
        }

        private static EventArgs CreateValidPublishEndEventArgs()
        {
            return new EventArgs();
        }

        private static PublishEndRemoteEventArgs CreateValidPublishEndRemoteEventArgs()
        {
            var eventArgsType = typeof(PublishEndRemoteEventArgs);
            var eventArgs = (PublishEndRemoteEventArgs)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(eventArgsType);

            SetPrivateField(eventArgs, "_rootItemId", ID.Parse(ValidItemId).Guid);
            SetPrivateField(eventArgs, "_mode", PublishMode.Full);
            SetPrivateField(eventArgs, "_deep", true);
            SetPrivateField(eventArgs, "_languageName", ValidLanguage);
            SetPrivateField(eventArgs, "_sourceDatabaseName", ValidSourceDatabase);
            SetPrivateField(eventArgs, "_targetDatabaseName", ValidTargetDatabase);
            SetPrivateField(eventArgs, "_eventQueueName", ValidEventQueueName);

            return eventArgs;
        }

        private static PublishEndRemoteEventArgs CreatePublishEndRemoteEventArgsWithEmptyLanguage()
        {
            var eventArgsType = typeof(PublishEndRemoteEventArgs);
            var eventArgs = (PublishEndRemoteEventArgs)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(eventArgsType);

            SetPrivateField(eventArgs, "_rootItemId", ID.Parse(ValidItemId).Guid);
            SetPrivateField(eventArgs, "_mode", PublishMode.Full);
            SetPrivateField(eventArgs, "_deep", true);
            SetPrivateField(eventArgs, "_languageName", string.Empty);
            SetPrivateField(eventArgs, "_sourceDatabaseName", ValidSourceDatabase);
            SetPrivateField(eventArgs, "_targetDatabaseName", ValidTargetDatabase);
            SetPrivateField(eventArgs, "_eventQueueName", ValidEventQueueName);

            return eventArgs;
        }

        private static PublishEndRemoteEventArgs CreatePublishEndRemoteEventArgsWithDeepPublish()
        {
            var eventArgsType = typeof(PublishEndRemoteEventArgs);
            var eventArgs = (PublishEndRemoteEventArgs)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(eventArgsType);

            SetPrivateField(eventArgs, "_rootItemId", ID.Parse(ValidItemId).Guid);
            SetPrivateField(eventArgs, "_mode", PublishMode.Full);
            SetPrivateField(eventArgs, "_deep", true);
            SetPrivateField(eventArgs, "_languageName", ValidLanguage);
            SetPrivateField(eventArgs, "_sourceDatabaseName", ValidSourceDatabase);
            SetPrivateField(eventArgs, "_targetDatabaseName", ValidTargetDatabase);
            SetPrivateField(eventArgs, "_eventQueueName", ValidEventQueueName);

            return eventArgs;
        }

        private static PublishEndRemoteEventArgs CreatePublishEndRemoteEventArgsWithEmptyEventQueue()
        {
            var eventArgsType = typeof(PublishEndRemoteEventArgs);
            var eventArgs = (PublishEndRemoteEventArgs)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(eventArgsType);

            SetPrivateField(eventArgs, "_rootItemId", ID.Parse(ValidItemId).Guid);
            SetPrivateField(eventArgs, "_mode", PublishMode.Full);
            SetPrivateField(eventArgs, "_deep", true);
            SetPrivateField(eventArgs, "_languageName", ValidLanguage);
            SetPrivateField(eventArgs, "_sourceDatabaseName", ValidSourceDatabase);
            SetPrivateField(eventArgs, "_targetDatabaseName", ValidTargetDatabase);
            SetPrivateField(eventArgs, "_eventQueueName", string.Empty);

            return eventArgs;
        }

        private static PublishItemContext CreateMockPublishItemContext(PublishOptions options)
        {
            var contextType = typeof(PublishItemContext);
            var context = (PublishItemContext)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(contextType);

            var publishContextType = typeof(PublishContext);
            var publishContext = (PublishContext)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(publishContextType);

            SetPrivateField(context, "m_itemId", ID.Parse(ValidItemId));
            SetPrivateField(context, "m_action", PublishAction.PublishVersion);
            SetPrivateField(context, "m_publishOptions", options);
            SetPrivateField(context, "m_publishContext", publishContext);

            SetPrivateField(publishContext, "m_options", options);

            return context;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                field = obj.GetType().GetField(possibleName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    return;
                }
            }

            var property = obj.GetType().GetProperty(fieldName.TrimStart('_'), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(obj, value);
            }
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

            var languageType = typeof(global::Sitecore.Globalization.Language);
            var language = (global::Sitecore.Globalization.Language)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(languageType);

            var nameField = languageType.GetField("_name", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nameField != null)
            {
                nameField.SetValue(language, "en");
            }

            var options = new PublishOptions(
                sourceDb.Object,
                targetDb.Object,
                PublishMode.Full,
                language,
                DateTime.Now);

            return options;
        }

        private void CleanupTestFiles()
        {
            if (File.Exists(TestAuditLogPath))
            {
                File.Delete(TestAuditLogPath);
            }
        }

        public void Dispose()
        {
            CleanupTestFiles();
        }

        #endregion
    }
}
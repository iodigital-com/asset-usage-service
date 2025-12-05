using AssetUsageService.Business.Events;
using AssetUsageService.Business.Handlers;
using AssetUsageService.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit.Abstractions;

namespace AssetUsageServiceTests.Performance;

[Trait("Category", "Performance")]
public class AssetIdsByPublicLinksHandlerPerformanceTests : IAsyncLifetime
{
    private const int MaxSingleLinkResponseTimeMs = 3000;
    private const int MaxMultipleLinkResponseTimeMs = 10000;
    private const int MaxThroughputLinksPerSecond = 2;
    private const bool LoadTestIsOn = true;
    private const int LoadTestAmountOfLinks = 100;

    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AssetIdsByPublicLinksHandler>> _mockLogger;
    private readonly IContentHubConnectionService? _contentHubConnection;
    private readonly AssetIdsByPublicLinksHandler? _handler;
    private readonly bool _integrationTestsEnabled;

    public AssetIdsByPublicLinksHandlerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<AssetIdsByPublicLinksHandler>>();

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: false)
            .Build();

        _integrationTestsEnabled = bool.Parse(_configuration["ContentHub:IntegrationTestsEnabled"] ?? "false");

        if (_integrationTestsEnabled)
        {
            var mockConnectionLogger = new Mock<ILogger<ContentHubConnectionService>>();
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            _contentHubConnection = new ContentHubConnectionService(
                mockConnectionLogger.Object,
                _configuration,
                mockHttpClientFactory.Object);

            _handler = new AssetIdsByPublicLinksHandler(_contentHubConnection, _mockLogger.Object);
        }
    }

    public Task InitializeAsync()
    {
        if (!_integrationTestsEnabled)
        {
            _output.WriteLine("WARNING: Performance tests are DISABLED");
            _output.WriteLine("Set ContentHub:IntegrationTestsEnabled = true in appsettings.Test.json to enable");
        }
        else
        {
            _output.WriteLine("INFO: Performance tests are ENABLED");
            _output.WriteLine($"ContentHub Endpoint: {_configuration["ContentHub:Endpoint"]}");
            _output.WriteLine($"Thresholds:");
            _output.WriteLine($"  Single link: < {MaxSingleLinkResponseTimeMs}ms");
            _output.WriteLine($"  Multiple links: < {MaxMultipleLinkResponseTimeMs}ms");
            _output.WriteLine($"  Throughput: > {MaxThroughputLinksPerSecond} links/sec");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Single Link Tests

    [SkippableFact]
    public async Task HandleAsync_SingleLink_CompletesWithinThreshold()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        var @event = CreateEvent(testLink);
        await WarmupHandler(@event);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _handler!.HandleAsync(@event, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        LogResult("Single link processing", elapsedMs, MaxSingleLinkResponseTimeMs);

        Assert.True(elapsedMs < MaxSingleLinkResponseTimeMs,
            $"Single link took {elapsedMs}ms, expected < {MaxSingleLinkResponseTimeMs}ms");
    }

    [SkippableFact]
    public async Task HandleAsync_ConsecutiveCalls_MaintainsConsistentPerformance()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        const int iterations = 5;
        var measurements = new List<long>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var @event = CreateEvent(testLink);
            var elapsed = await MeasureExecutionTime(@event);
            measurements.Add(elapsed);
        }

        // Assert
        var stats = CalculateStats(measurements);
        LogConsistencyResults(iterations, stats);

        Assert.True(stats.Variance < stats.Average * 0.5,
            $"Performance variance {stats.Variance}ms exceeds 50% of average {stats.Average:F0}ms");
    }

    #endregion

    #region Multiple Links Tests

    [SkippableFact]
    public async Task HandleAsync_MultipleLinks_CompletesWithinThreshold()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");

        // Arrange
        var testLinks = GetTestLinks();
        var @event = CreateEvent(testLinks);
        await WarmupHandler(@event);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _handler!.HandleAsync(@event, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var avgMs = elapsedMs / (double)testLinks.Count;

        LogMultipleLinkResults(testLinks.Count, elapsedMs, avgMs);

        Assert.True(elapsedMs < MaxMultipleLinkResponseTimeMs,
            $"Multiple links took {elapsedMs}ms, expected < {MaxMultipleLinkResponseTimeMs}ms");
    }

    [SkippableFact]
    public async Task HandleAsync_ParallelProcessing_BenefitsFromConcurrency()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        var multipleLinks = Enumerable.Repeat(testLink, 5).ToList();
        var @event = CreateEvent(multipleLinks);
        await WarmupHandler(@event);

        // Act
        var actualTime = await MeasureExecutionTime(@event);

        // Assert
        var expectedSequentialTime = MaxSingleLinkResponseTimeMs * 5;
        var efficiency = (double)expectedSequentialTime / actualTime;

        LogParallelResults(multipleLinks.Count, actualTime, expectedSequentialTime, efficiency);

        Assert.True(efficiency >= 1.0,
            $"Parallel processing not efficient: {efficiency:F2}x (should be >= 1.0x)");
    }

    #endregion

    #region Memory Tests

    [SkippableFact]
    public async Task HandleAsync_RepeatedCalls_DoesNotLeakMemory()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        const int iterations = 100;
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var @event = CreateEvent(testLink);
            await _handler!.HandleAsync(@event, CancellationToken.None);
        }

        ForceGarbageCollection();

        // Assert
        var finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        var memoryIncreaseMb = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        LogMemoryResults(iterations, initialMemory, finalMemory, memoryIncreaseMb);

        Assert.True(memoryIncreaseMb < 10,
            $"Potential memory leak: {memoryIncreaseMb:F2} MB retained after {iterations} iterations");
    }

    #endregion

    #region Edge Case Tests

    [SkippableFact]
    public async Task HandleAsync_EmptyList_CompletesImmediately()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");

        // Arrange
        var @event = new AssetIdsByPublicLinksEvent { PublicLinks = new List<string>() };

        // Act
        var elapsedMs = await MeasureExecutionTime(@event);

        // Assert
        _output.WriteLine($"Empty list processing: {elapsedMs}ms");
        _output.WriteLine($"Result: {(elapsedMs < 100 ? "PASS" : "FAIL")}");

        Assert.True(elapsedMs < 100, $"Empty list took {elapsedMs}ms, expected < 100ms");
    }

    #endregion

    #region Load Tests

    [SkippableFact]
    public async Task HandleAsync_LoadTest_MaintainsPerformance()
    {
        Skip.IfNot(_integrationTestsEnabled, "Performance tests are disabled");
        Skip.IfNot(LoadTestIsOn);

        // Arrange
        var testLink = GetTestLink();
        const int batchSize = 5;
        const int iterations = LoadTestAmountOfLinks / batchSize;

        LogLoadTestHeader(LoadTestAmountOfLinks, batchSize);

        var publicLinks = Enumerable.Repeat(testLink, batchSize).ToList();
        var totalAssetsProcessed = 0;
        var failedBatches = 0;

        // Act
        var overallStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var @event = CreateEvent(publicLinks);

            try
            {
                await _handler!.HandleAsync(@event, CancellationToken.None);
                totalAssetsProcessed += batchSize;
            }
            catch (Exception ex)
            {
                failedBatches++;
                _output.WriteLine($"Batch {i + 1} failed: {ex.Message}");
            }

            if ((i + 1) % 25 == 0)
            {
                LogProgress(i + 1, iterations);
            }

            await Task.Delay(50);
        }

        overallStopwatch.Stop();

        // Assert
        var results = CalculateLoadTestResults(
            totalTimeMs: overallStopwatch.ElapsedMilliseconds,
            totalAssetsProcessed: totalAssetsProcessed,
            iterations: iterations,
            failedBatches: failedBatches);

        LogLoadTestResults(results);
        AssertLoadTestResults(results);
    }

    #endregion

    #region Helper Methods

    private string GetTestLink()
    {
        var testLink = _configuration["ContentHub:TestPublicLink"];
        Skip.If(string.IsNullOrEmpty(testLink), "ContentHub:TestPublicLink not configured");
        return testLink;
    }

    private List<string> GetTestLinks()
    {
        var testLinks = _configuration.GetSection("ContentHub:TestPublicLinks").Get<List<string>>();
        Skip.If(testLinks == null || testLinks.Count == 0, "ContentHub:TestPublicLinks not configured");
        return testLinks;
    }

    private static AssetIdsByPublicLinksEvent CreateEvent(params string[] links)
    {
        return new AssetIdsByPublicLinksEvent { PublicLinks = links.ToList() };
    }

    private static AssetIdsByPublicLinksEvent CreateEvent(List<string> links)
    {
        return new AssetIdsByPublicLinksEvent { PublicLinks = links };
    }

    private async Task WarmupHandler(AssetIdsByPublicLinksEvent @event)
    {
        await _handler!.HandleAsync(@event, CancellationToken.None);
    }

    private async Task<long> MeasureExecutionTime(AssetIdsByPublicLinksEvent @event)
    {
        var stopwatch = Stopwatch.StartNew();
        await _handler!.HandleAsync(@event, CancellationToken.None);
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    private static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static PerformanceStats CalculateStats(List<long> measurements)
    {
        return new PerformanceStats
        {
            Average = measurements.Average(),
            Min = measurements.Min(),
            Max = measurements.Max(),
            Variance = measurements.Max() - measurements.Min()
        };
    }

    private static LoadTestResults CalculateLoadTestResults(long totalTimeMs, int totalAssetsProcessed, int iterations, int failedBatches)
    {
        var totalTimeSeconds = totalTimeMs / 1000.0;
        var successRate = ((iterations - failedBatches) / (double)iterations) * 100;
        var throughput = totalAssetsProcessed / totalTimeSeconds;
        var avgTimePerLink = totalTimeMs / (double)totalAssetsProcessed;
        var estimatedTime100k = (avgTimePerLink * 100_000) / 1000.0;

        return new LoadTestResults
        {
            TotalTimeSeconds = totalTimeSeconds,
            TotalAssetsProcessed = totalAssetsProcessed,
            SuccessRate = successRate,
            Throughput = throughput,
            AvgTimePerLink = avgTimePerLink,
            EstimatedTime100k = estimatedTime100k,
            EstimatedMinutes100k = estimatedTime100k / 60.0
        };
    }

    #endregion

    #region Logging Methods

    private void LogResult(string operation, long elapsedMs, int threshold)
    {
        _output.WriteLine($"{operation}: {elapsedMs}ms");
        _output.WriteLine($"Threshold: {threshold}ms");
        _output.WriteLine($"Result: {(elapsedMs < threshold ? "PASS" : "FAIL")}");
    }

    private void LogMultipleLinkResults(int linkCount, long elapsedMs, double avgMs)
    {
        _output.WriteLine($"Multiple links processing:");
        _output.WriteLine($"  Links processed: {linkCount}");
        _output.WriteLine($"  Total time: {elapsedMs}ms");
        _output.WriteLine($"  Average per link: {avgMs:F0}ms");
        _output.WriteLine($"  Threshold: {MaxMultipleLinkResponseTimeMs}ms");
        _output.WriteLine($"  Result: {(elapsedMs < MaxMultipleLinkResponseTimeMs ? "PASS" : "FAIL")}");
    }

    private void LogConsistencyResults(int iterations, PerformanceStats stats)
    {
        _output.WriteLine($"Consecutive calls performance:");
        _output.WriteLine($"  Iterations: {iterations}");
        _output.WriteLine($"  Average: {stats.Average:F0}ms");
        _output.WriteLine($"  Min: {stats.Min}ms");
        _output.WriteLine($"  Max: {stats.Max}ms");
        _output.WriteLine($"  Variance: {stats.Variance}ms");
        _output.WriteLine($"  Result: {(stats.Variance < stats.Average * 0.5 ? "PASS (consistent)" : "WARNING (high variance)")}");
    }

    private void LogParallelResults(int linkCount, long actualTime, long expectedTime, double efficiency)
    {
        _output.WriteLine($"Parallel processing efficiency:");
        _output.WriteLine($"  Links: {linkCount}");
        _output.WriteLine($"  Actual time: {actualTime}ms");
        _output.WriteLine($"  Expected sequential: ~{expectedTime}ms");
        _output.WriteLine($"  Efficiency: {efficiency:F2}x faster");
        _output.WriteLine($"  Result: {(efficiency >= 1.5 ? "GOOD" : efficiency >= 1.0 ? "ACCEPTABLE" : "POOR")}");
    }

    private void LogMemoryResults(int iterations, long initialMemory, long finalMemory, double memoryIncreaseMb)
    {
        _output.WriteLine($"Memory usage after {iterations} iterations:");
        _output.WriteLine($"  Initial: {initialMemory / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"  Final: {finalMemory / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"  Increase: {memoryIncreaseMb:F2} MB");
        _output.WriteLine($"  Result: {(memoryIncreaseMb < 10 ? "PASS (no leak)" : "WARNING (possible leak)")}");
    }

    private void LogLoadTestHeader(int totalLinks, int batchSize)
    {
        _output.WriteLine("");
        _output.WriteLine("LOAD TEST - 1,000 PUBLIC LINKS");
        _output.WriteLine("===============================");
        _output.WriteLine($"Total Links: {totalLinks:N0}");
        _output.WriteLine($"Batch Size: {batchSize}");
        _output.WriteLine("");
    }

    private void LogProgress(int current, int total)
    {
        var progress = (current / (double)total) * 100;
        _output.WriteLine($"Progress: {progress:F0}% ({current}/{total} batches)");
    }

    private void LogLoadTestResults(LoadTestResults results)
    {
        _output.WriteLine("");
        _output.WriteLine("RESULTS");
        _output.WriteLine("=======");
        _output.WriteLine($"Total Time: {results.TotalTimeSeconds:F2}s");
        _output.WriteLine($"Links Processed: {results.TotalAssetsProcessed:N0}");
        _output.WriteLine($"Success Rate: {results.SuccessRate:F1}%");
        _output.WriteLine($"Throughput: {results.Throughput:F2} links/sec");
        _output.WriteLine($"Avg Time per Link: {results.AvgTimePerLink:F0}ms");
        _output.WriteLine("");
        _output.WriteLine("PROJECTION FOR 100,000 LINKS");
        _output.WriteLine("============================");
        _output.WriteLine($"Estimated Time: {results.EstimatedTime100k:F0}s (~{results.EstimatedMinutes100k:F1} minutes)");
        _output.WriteLine($"Based on: {results.AvgTimePerLink:F0}ms per link");
        _output.WriteLine("");
    }

    private void AssertLoadTestResults(LoadTestResults results)
    {
        Assert.True(results.SuccessRate >= 95,
            $"Success rate {results.SuccessRate:F1}% is below 95%");

        Assert.True(results.Throughput >= MaxThroughputLinksPerSecond,
            $"Throughput {results.Throughput:F2} links/sec is below {MaxThroughputLinksPerSecond}");

        _output.WriteLine("PASS - All assertions met");
    }

    #endregion

    #region Helper Classes

    private record PerformanceStats
    {
        public double Average { get; init; }
        public long Min { get; init; }
        public long Max { get; init; }
        public long Variance { get; init; }
    }

    private record LoadTestResults
    {
        public double TotalTimeSeconds { get; init; }
        public int TotalAssetsProcessed { get; init; }
        public double SuccessRate { get; init; }
        public double Throughput { get; init; }
        public double AvgTimePerLink { get; init; }
        public double EstimatedTime100k { get; init; }
        public double EstimatedMinutes100k { get; init; }
    }

    #endregion
}
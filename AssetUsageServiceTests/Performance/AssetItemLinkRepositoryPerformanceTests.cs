using AssetUsageService.Domain.Data;
using AssetUsageService.Infrastructure;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.Diagnostics;
using Xunit.Abstractions;

namespace AssetUsageServiceTests.Performance;

[Trait("Category", "Performance")]
public class AssetItemLinkRepositoryPerformanceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IAssetItemLinkRepository _repository = null!;
    private DBContext _dbContext = null!;
    private IMongoClient _mongoClient = null!;
    private readonly List<Guid> _testItemIds = new();

    private const int TotalTestItems = 100_000;
    private const int AssetsPerItem = 20;
    private const int SeedBatchSize = 1000;

    private const int MaxInsertTimeMs = 100;
    private const int MaxGetTimeMs = 50;
    private const int MaxQueryByAssetTimeMs = 500;

    public AssetItemLinkRepositoryPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"=== Setting up test database with {TotalTestItems:N0} items ===\n");
        var setupStopwatch = Stopwatch.StartNew();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDB:DatabaseName"] = "AssetUsagePerformanceTest"
            })
            .Build();

        _mongoClient = new MongoClient(configuration["MongoDB:ConnectionString"]);
        _dbContext = new DBContext(_mongoClient, configuration);

        await _dbContext.AssetItemLinks.Database.DropCollectionAsync("AssetItemLinks");

        _repository = new AssetItemLinkRepository(_dbContext);

        var numberOfBatches = TotalTestItems / SeedBatchSize;

        for (int batchIndex = 0; batchIndex < numberOfBatches; batchIndex++)
        {
            var batchItems = new List<AssetItemLink>();

            for (int itemIndex = 0; itemIndex < SeedBatchSize; itemIndex++)
            {
                var itemId = Guid.NewGuid();
                _testItemIds.Add(itemId);

                var baseAssetId = 1000 + (batchIndex * 100);
                var assetIds = Enumerable.Range(baseAssetId, AssetsPerItem)
                    .Select(assetId => assetId + Random.Shared.Next(0, 50))
                    .Distinct()
                    .OrderBy(assetId => assetId)
                    .ToList();

                batchItems.Add(new AssetItemLink
                {
                    ItemId = itemId,
                    AssetIds = assetIds,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _dbContext.AssetItemLinks.InsertManyAsync(batchItems);
        }

        setupStopwatch.Stop();
        _output.WriteLine($"Setup completed in {setupStopwatch.Elapsed.TotalSeconds:F1}s");
        _output.WriteLine($"Database ready: {TotalTestItems:N0} items, ~{AssetsPerItem} assets each\n");
    }

    public async Task DisposeAsync()
    {
        await _dbContext.AssetItemLinks.Database.DropCollectionAsync("AssetItemLinks");
    }

    #region Insert Performance Tests

    [Fact]
    public async Task InsertSingleItem_WithLargeDatabase_ShouldCompleteQuickly()
    {
        var itemId = Guid.NewGuid();
        var assetIds = Enumerable.Range(5000, AssetsPerItem).ToList();
        var stopwatch = Stopwatch.StartNew();

        await _repository.InsertAssetItemLinkAsync(itemId, assetIds, CancellationToken.None);

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"Single insert: {elapsedMs}ms (threshold: {MaxInsertTimeMs}ms)");
        Assert.True(elapsedMs < MaxInsertTimeMs,
            $"Insert took {elapsedMs}ms, expected < {MaxInsertTimeMs}ms");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task InsertMultipleItems_WithLargeDatabase_ShouldMaintainPerformance(int insertCount)
    {
        var stopwatch = Stopwatch.StartNew();

        for (int index = 0; index < insertCount; index++)
        {
            var assetIds = Enumerable.Range(6000 + index * AssetsPerItem, AssetsPerItem).ToList();
            await _repository.InsertAssetItemLinkAsync(Guid.NewGuid(), assetIds, CancellationToken.None);
        }

        stopwatch.Stop();
        var totalMs = stopwatch.ElapsedMilliseconds;
        var averageMs = totalMs / (double)insertCount;

        _output.WriteLine($"Bulk insert ({insertCount} items): {totalMs}ms total, {averageMs:F1}ms avg");
        Assert.True(averageMs < MaxInsertTimeMs,
            $"Average insert time {averageMs:F1}ms exceeds {MaxInsertTimeMs}ms threshold");
    }

    #endregion

    #region Get Performance Tests

    [Fact]
    public async Task GetByItemId_WithLargeDatabase_ShouldCompleteQuickly()
    {
        var randomItemId = _testItemIds[Random.Shared.Next(_testItemIds.Count)];
        var stopwatch = Stopwatch.StartNew();

        var result = await _repository.GetAssetItemLinkByItemIdAsync(randomItemId, CancellationToken.None);

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"Single get: {elapsedMs}ms (threshold: {MaxGetTimeMs}ms)");
        Assert.NotNull(result);
        Assert.True(elapsedMs < MaxGetTimeMs,
            $"Get took {elapsedMs}ms, expected < {MaxGetTimeMs}ms");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task GetMultipleRandomItems_WithLargeDatabase_ShouldMaintainPerformance(int queryCount)
    {
        var stopwatch = Stopwatch.StartNew();

        for (int index = 0; index < queryCount; index++)
        {
            var randomItemId = _testItemIds[Random.Shared.Next(_testItemIds.Count)];
            await _repository.GetAssetItemLinkByItemIdAsync(randomItemId, CancellationToken.None);
        }

        stopwatch.Stop();
        var totalMs = stopwatch.ElapsedMilliseconds;
        var averageMs = totalMs / (double)queryCount;

        _output.WriteLine($"Bulk get ({queryCount} items): {totalMs}ms total, {averageMs:F1}ms avg");
        Assert.True(averageMs < MaxGetTimeMs,
            $"Average query time {averageMs:F1}ms exceeds {MaxGetTimeMs}ms threshold");
    }

    [Fact]
    public async Task GetItemsByAssetId_WithLargeDatabase_ShouldHandleLargeResultSet()
    {
        var commonAssetId = 1050;
        var stopwatch = Stopwatch.StartNew();

        var results = await _repository.GetItemIdsByAssetIdAsync(commonAssetId, CancellationToken.None);

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"Query by AssetId: {results.Count} results in {elapsedMs}ms (threshold: {MaxQueryByAssetTimeMs}ms)");
        Assert.NotEmpty(results);
        Assert.True(elapsedMs < MaxQueryByAssetTimeMs,
            $"Query took {elapsedMs}ms, expected < {MaxQueryByAssetTimeMs}ms");
    }

    #endregion

    #region Parallel Operations Tests

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetInParallel_WithLargeDatabase_ShouldShowParallelizationBenefit(int parallelCount)
    {
        var randomItemIds = Enumerable.Range(0, parallelCount)
            .Select(_ => _testItemIds[Random.Shared.Next(_testItemIds.Count)])
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        var queryTasks = randomItemIds.Select(itemId =>
            _repository.GetAssetItemLinkByItemIdAsync(itemId, CancellationToken.None));
        var results = await Task.WhenAll(queryTasks);

        stopwatch.Stop();
        var totalMs = stopwatch.ElapsedMilliseconds;
        var throughput = parallelCount / (totalMs / 1000.0);

        _output.WriteLine($"Parallel get ({parallelCount} items): {totalMs}ms, {throughput:F0} ops/sec");
        Assert.Equal(parallelCount, results.Length);
    }

    [Fact]
    public async Task InsertInParallel_WithLargeDatabase_ShouldShowParallelizationBenefit()
    {
        const int parallelInsertCount = 100;
        var stopwatch = Stopwatch.StartNew();

        var insertTasks = Enumerable.Range(0, parallelInsertCount)
            .Select(index => {
                var assetIds = Enumerable.Range(7000 + index * AssetsPerItem, AssetsPerItem).ToList();
                return _repository.InsertAssetItemLinkAsync(Guid.NewGuid(), assetIds, CancellationToken.None);
            })
            .ToList();

        await Task.WhenAll(insertTasks);

        stopwatch.Stop();
        var totalMs = stopwatch.ElapsedMilliseconds;
        var throughput = parallelInsertCount / (totalMs / 1000.0);

        _output.WriteLine($"Parallel insert ({parallelInsertCount} items): {totalMs}ms, {throughput:F0} ops/sec");
    }

    #endregion

    #region Realistic Scenario Tests

    [Fact]
    public async Task MixedWorkload_WithRealisticReadWriteRatio_ShouldPerformWell()
    {
        const int totalOperations = 100;
        const int readPercentage = 70;

        var readCount = 0;
        var writeCount = 0;
        var stopwatch = Stopwatch.StartNew();

        var operationTasks = new List<Task>();

        for (int index = 0; index < totalOperations; index++)
        {
            if (Random.Shared.Next(100) < readPercentage)
            {
                var randomItemId = _testItemIds[Random.Shared.Next(_testItemIds.Count)];
                operationTasks.Add(_repository.GetAssetItemLinkByItemIdAsync(randomItemId, CancellationToken.None));
                readCount++;
            }
            else
            {
                var assetIds = Enumerable.Range(8000 + index * AssetsPerItem, AssetsPerItem).ToList();
                operationTasks.Add(_repository.InsertAssetItemLinkAsync(Guid.NewGuid(), assetIds, CancellationToken.None));
                writeCount++;
            }
        }

        await Task.WhenAll(operationTasks);

        stopwatch.Stop();
        var totalMs = stopwatch.ElapsedMilliseconds;
        var throughput = totalOperations / (totalMs / 1000.0);

        _output.WriteLine($"Mixed workload: {readCount} reads, {writeCount} writes");
        _output.WriteLine($"Completed in {totalMs}ms, {throughput:F0} ops/sec");

        const int maxMixedWorkloadTimeMs = 5000;
        Assert.True(totalMs < maxMixedWorkloadTimeMs,
            $"Mixed workload took {totalMs}ms, expected < {maxMixedWorkloadTimeMs}ms");
    }

    [Fact]
    public async Task DatabaseScaling_ShouldMaintainConsistentPerformance()
    {
        const int queriesPerBatch = 10;
        var measurements = new List<(string Position, long Milliseconds)>();

        var firstBatch = _testItemIds.Take(queriesPerBatch).ToList();
        var middleBatch = _testItemIds.Skip(TotalTestItems / 2).Take(queriesPerBatch).ToList();
        var lastBatch = _testItemIds.TakeLast(queriesPerBatch).ToList();

        measurements.Add(("First", await MeasureBatchQuery(firstBatch)));
        measurements.Add(("Middle", await MeasureBatchQuery(middleBatch)));
        measurements.Add(("Last", await MeasureBatchQuery(lastBatch)));

        foreach (var (position, milliseconds) in measurements)
        {
            _output.WriteLine($"{position} 10 items: {milliseconds}ms");
        }

        var maxVarianceMs = measurements.Max(measurement => measurement.Milliseconds)
                          - measurements.Min(measurement => measurement.Milliseconds);

        _output.WriteLine($"Performance variance: {maxVarianceMs}ms");

        const int maxVarianceThresholdMs = 100;
        Assert.True(maxVarianceMs < maxVarianceThresholdMs,
            $"Variance {maxVarianceMs}ms exceeds {maxVarianceThresholdMs}ms (potential indexing issue)");
    }

    private async Task<long> MeasureBatchQuery(List<Guid> itemIds)
    {
        var stopwatch = Stopwatch.StartNew();

        foreach (var itemId in itemIds)
        {
            await _repository.GetAssetItemLinkByItemIdAsync(itemId, CancellationToken.None);
        }

        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    #endregion
}
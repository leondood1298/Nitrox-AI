using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class WorldEntityManagerLoadingProgressTest
{
    [TestMethod]
    public async Task ProcessesEntireGridAndReportsMonotonicProgressFromZeroToOneHundred()
    {
        List<NitroxInt3> processedBatches = [];
        List<int> reportedPercentages = [];

        await WorldEntityManager.ProcessBatchGridAsync(
            new(2, 1, 2),
            batchId =>
            {
                processedBatches.Add(batchId);
                return Task.CompletedTask;
            },
            reportedPercentages.Add,
            CancellationToken.None);

        processedBatches.Should().Equal(
            new NitroxInt3(0, 0, 0),
            new NitroxInt3(0, 0, 1),
            new NitroxInt3(1, 0, 0),
            new NitroxInt3(1, 0, 1));
        reportedPercentages.Should().Equal(0, 50, 100);
        reportedPercentages.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [TestMethod]
    public async Task EmptyGridReportsZeroThenOneHundredWithoutProcessingBatches()
    {
        int processedBatchCount = 0;
        List<int> reportedPercentages = [];

        await WorldEntityManager.ProcessBatchGridAsync(
            new(2, 0, 3),
            _ =>
            {
                processedBatchCount++;
                return Task.CompletedTask;
            },
            reportedPercentages.Add,
            CancellationToken.None);

        processedBatchCount.Should().Be(0);
        reportedPercentages.Should().Equal(0, 100);
    }

    [TestMethod]
    public async Task CancellationInsideGridStopsBeforeNextBatchWithoutReportingOneHundred()
    {
        using CancellationTokenSource cancellation = new();
        int processedBatchCount = 0;
        List<int> reportedPercentages = [];

        Func<Task> action = () => WorldEntityManager.ProcessBatchGridAsync(
            new(1, 2, 2),
            _ =>
            {
                processedBatchCount++;
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            reportedPercentages.Add,
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();

        processedBatchCount.Should().Be(1);
        reportedPercentages.Should().Equal(0);
    }
}

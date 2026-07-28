using System.Linq;
using GlasLauncher.Core.Models;
using GlasLauncher.Core.Services.Fakes;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class FakeJavaModServiceTests
{
    private sealed class RecordingProgress : IProgress<RepairProgress>
    {
        public List<RepairProgress> Reports { get; } = new();
        public void Report(RepairProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task RepairAsync_ReportsAllFourStepsInOrder()
    {
        var service = new FakeJavaModService();
        var progress = new RecordingProgress();

        await service.RepairAsync(progress);

        var stepNames = progress.Reports.Select(r => r.StepName).Distinct().ToList();
        Assert.Equal(
            new[]
            {
                RepairStepNames.OldVersionRemoved,
                RepairStepNames.DownloadingJavaMod,
                RepairStepNames.VerifyingIntegrity,
                RepairStepNames.Installing
            },
            stepNames);
    }

    [Fact]
    public async Task RepairAsync_DownloadStep_ReportsMegabytes()
    {
        var service = new FakeJavaModService();
        var progress = new RecordingProgress();

        await service.RepairAsync(progress);

        var downloadReports = progress.Reports.Where(r => r.StepName == RepairStepNames.DownloadingJavaMod).ToList();
        Assert.NotEmpty(downloadReports);
        Assert.All(downloadReports, r =>
        {
            Assert.NotNull(r.MegabytesDownloaded);
            Assert.NotNull(r.MegabytesTotal);
        });
    }

    [Fact]
    public async Task RepairAsync_NonDownloadSteps_DoNotReportMegabytes()
    {
        var service = new FakeJavaModService();
        var progress = new RecordingProgress();

        await service.RepairAsync(progress);

        var otherReports = progress.Reports.Where(r => r.StepName != RepairStepNames.DownloadingJavaMod);
        Assert.All(otherReports, r =>
        {
            Assert.Null(r.MegabytesDownloaded);
            Assert.Null(r.MegabytesTotal);
        });
    }
}

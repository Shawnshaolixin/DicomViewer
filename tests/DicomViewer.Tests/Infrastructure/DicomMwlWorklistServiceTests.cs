using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Application.Abstractions;
using DicomViewer.Infrastructure.Worklist;

namespace DicomViewer.Tests.Infrastructure;

public sealed class DicomMwlWorklistServiceTests
{
    [Fact]
    public async Task QueryAsync_WhenMwlHostMissing_FallsBackToMockOrders()
    {
        // 构造一个“MWL 未配置”的场景，验证服务会安全降级。
        var store = new TestConsoleConfigurationStore
        {
            Configuration = new ConsoleConfiguration(
                new PacsConfiguration(
                    CallingAeTitle: "CALLING",
                    CalledAeTitle: "CALLED",
                    Host: "127.0.0.1",
                    Port: 4242,
                    RestApiPort: 8042,
                    OutputDirectory: "output",
                    MwlHost: string.Empty,
                    MwlPort: 0),
                ExposureParameterRange.Default)
        };

        var service = new DicomMwlWorklistService(store, new MockWorklistService());

        var result = await service.QueryAsync(MwlQueryCriteria.Empty);

        // 断言至少有结果且来源标记为 Mock，说明 fallback 生效。
        Assert.NotEmpty(result);
        Assert.All(result, order => Assert.Equal("Mock", order.SourceType));
    }

    private sealed class TestConsoleConfigurationStore : IConsoleConfigurationStore
    {
        // 测试替身：直接把配置保存在内存，避免引入数据库依赖。
        public ConsoleConfiguration Configuration { get; set; } = ConsoleConfiguration.Default;

        public ConsoleConfiguration Load()
        {
            return Configuration;
        }

        public void Save(ConsoleConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.BackgroundServices;
using Web.Server.DTOs;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class ServerHealthMonitorServiceTests
{
    [TestMethod]
    public async Task CheckOnce_SendsEachChangeToItsRecipientsOnly()
    {
        var change = new ServerStatusChange(
            new ServerStatusChangedDTO { BeaconID = 1, BeaconName = "Sussex", PreviousStatus = ServerStatus.Online, Status = ServerStatus.Offline },
            [1, 40]);
        var unaddressed = new ServerStatusChange(new ServerStatusChangedDTO { BeaconID = 2 }, []);
        var serverHealthServiceMock = new Mock<IServerHealthService>();
        serverHealthServiceMock.Setup(s => s.DetectStatusChangesAsync()).ReturnsAsync([change, unaddressed]);

        var clientProxyMock = new Mock<IClientProxy>();
        IReadOnlyList<string>? sentToGroups = null;
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock
            .Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(groups => sentToGroups = groups)
            .Returns(clientProxyMock.Object);
        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        hubContextMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

        var services = new ServiceCollection();
        services.AddScoped(_ => serverHealthServiceMock.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var monitor = new TestableMonitor(scopeFactory, hubContextMock.Object);
        await monitor.RunOnceAsync();

        CollectionAssert.AreEqual(new[] { "user-1", "user-40" }, sentToGroups!.ToArray());
        hubClientsMock.Verify(c => c.Groups(It.IsAny<IReadOnlyList<string>>()), Times.Once);
        clientProxyMock.Verify(p => p.SendCoreAsync(
            NotificationMethods.ServerStatusChanged,
            It.Is<object?[]>(args => args.Length == 1 && args[0] == change.Notification),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestableMonitor(IServiceScopeFactory scopeFactory, IHubContext<NotificationHub> hubContext)
        : ServerHealthMonitorService(scopeFactory, hubContext, new Mock<ILogger<ServerHealthMonitorService>>().Object)
    {
        public Task RunOnceAsync() => CheckOnceAsync(CancellationToken.None);
    }
}

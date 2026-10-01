using BudgetTracker.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;

namespace Test.Infrastructure;

[TestFixture]
public class BacenServiceTest
{
    private static BacenService BuildService(HttpMessageHandler handler)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Bacen:Url"]).Returns("https://api.bcb.gov.br");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        return new BacenService(httpClientFactory.Object, config.Object, NullLogger<BacenService>.Instance);
    }

    private static HttpMessageHandler RespondWith(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(json) });
        return handler.Object;
    }

    [Test]
    public async Task GetHistoryCdiAsync_ValidResponse_ReturnsParsedEntries()
    {
        var service = BuildService(RespondWith(HttpStatusCode.OK, """[{"data":"15/07/2026","valor":"0,1365"},{"data":"16/07/2026","valor":"0,1365"}]"""));

        var result = await service.GetHistoryCdiAsync("15/07/2026", "16/07/2026");

        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.First().Date, Is.EqualTo("2026-07-15"));
        Assert.That(result.First().Value, Is.EqualTo(0.1365m));
    }

    [Test]
    public async Task GetHistoryCdiAsync_EmptyResponse_ReturnsEmptyList()
    {
        var service = BuildService(RespondWith(HttpStatusCode.OK, "[]"));

        var result = await service.GetHistoryCdiAsync("15/07/2026", "16/07/2026");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetHistoryCdiAsync_ErrorStatus_Throws()
    {
        var service = BuildService(RespondWith(HttpStatusCode.InternalServerError, "{}"));

        Assert.ThrowsAsync<HttpRequestException>(() => service.GetHistoryCdiAsync("15/07/2026", "16/07/2026"));
    }
}

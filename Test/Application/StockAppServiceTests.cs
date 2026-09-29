using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.OutPut;
using BudgetTracker.Core.Infrastructure.Repository;
using BudgetTracker.Core.Infrastructure.Services;
using Moq;

namespace Test.Application;

/// <summary>
/// A missing market quote (rate limit, unknown ticker, API outage) must degrade to
/// price 0 — never throw — so the whole portfolio still loads.
/// </summary>
[TestFixture]
public class StockAppServiceTests
{
    private const long AccountId = 1;

    private static Stock BuildStock(string ticker, decimal priceBuyed, decimal priceMarket = 0m) =>
        new() { Id = 1, AccountId = AccountId, Ticker = ticker, Title = ticker, PriceBuyed = priceBuyed, PriceMarket = priceMarket, Quantity = 1, IsStock = true };

    private static StockAppService BuildService(List<Stock> stocks)
    {
        var stockRepository = new Mock<IStockRepository>();
        stockRepository.Setup(r => r.GetAllAsync(AccountId)).ReturnsAsync(stocks);
        stockRepository.Setup(r => r.UpdateAsync(It.IsAny<Stock>())).ReturnsAsync(true);

        var marketService = new Mock<IStockMarketService>();
        marketService.Setup(m => m.GetStockByTickerAsync(It.IsAny<List<string>>())).ReturnsAsync(new List<StockMarketResponse>());

        return new StockAppService(stockRepository.Object, marketService.Object, new Mock<IUnitOfWork>().Object);
    }

    [Test]
    public async Task Dado_MercadoSemCotacao_Quando_ListarAtivos_Entao_RetornaComPrecoZeroSemErro()
    {
        var service = BuildService(new List<Stock> { BuildStock("PETR4", 10m) });

        var result = await service.GetAllStockAsync(AccountId, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Count(), Is.EqualTo(1));
            Assert.That(result.Items.First().Ticker, Is.EqualTo("PETR4"));
            Assert.That(result.Items.First().PriceMarket, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Dado_StocksComPrecosDiferentes_Quando_ListarAtivos_Entao_RetornaPrecosDoBanco()
    {
        var service = BuildService(new List<Stock>
        {
            BuildStock("PETR4", 10m, priceMarket: 15m),
            BuildStock("VALE3", 20m),
        });

        var result = await service.GetAllStockAsync(AccountId, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Count(), Is.EqualTo(2));
            Assert.That(result.Items.First(s => s.Ticker == "PETR4").PriceMarket, Is.EqualTo(15m));
            Assert.That(result.Items.First(s => s.Ticker == "VALE3").PriceMarket, Is.EqualTo(0));
        });
    }
}

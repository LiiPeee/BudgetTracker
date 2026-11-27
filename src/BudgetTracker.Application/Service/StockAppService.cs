using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Models.Output;
using BudgetTracker.Core.Domain.Models.Request.Stock;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using BudgetTracker.Core.Infrastructure.Services;


namespace BudgetTracker.Application.Service
{
    public class StockAppService(IStockRepository stockRepository, IStockMarketService stockMarketService, IUnitOfWork unitOfWork) : IStockAppService
    {
        private readonly IStockRepository _stockRepository = stockRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        private readonly IStockMarketService _stockMarketService = stockMarketService;
        public async Task CreateAsync(long accountId, CreateStockRequest request)
        {
            if (request.Price <= 0)
                throw new ArgumentException("price must be greater than zero");

            if (request.Quantity <= 0)
                throw new ArgumentException("quantity must be greater than zero");

            var ticker = request.Ticker.Trim().ToUpperInvariant();

            // Só ações somam na mesma posição. Cada aplicação de renda fixa tem data e taxa
            // próprias, então vira um registro separado.
            if (request.IsStock && await _stockRepository.GetByStockAndAccountAsync(accountId, ticker) is { } stock)
            {
                await AddToPositionAsync(stock, request);
                return;
            }

            var newStock = new Stock()
            {
                AccountId = accountId,
                Ticker = ticker,
                Title = request.Title,
                Quantity = request.Quantity,
                Description = request.Description,
                FixedIncomeType = request.FixedIncomeType,
                PriceBuyed = request.Price,
                PriceMarket = request.IsStock ? await GetMarketPriceAsync(ticker) ?? 0 : 0,
                CdiRate = request.CdiRate,
                IsStock = request.IsStock,
                InvestmentDate = request.InvestmentDate
            };

            await _stockRepository.AddAsync(newStock);
        }

        public async Task<IPagedResult<GetAllStockResponse>> GetAllStockAsync(long accountId, int page = 1)
        {
            var stocks = (await _stockRepository.GetAllAsync(accountId))
                .Where(x => x.IsStock == true).ToList();

            var allStocks = stocks.Select(x =>
            {
                var priceMarket = x.PriceMarket;
                var percentage = Variation(priceMarket, x.PriceBuyed);
                return new GetAllStockResponse()
                {
                    Ticker = x.Ticker,
                    PriceBuyed = x.PriceBuyed,
                    PriceMarket = priceMarket,
                    Percentage = $"{Math.Round(percentage, 2)}%",
                    Quantity = x.Quantity,
                    InvestmentDate = x.InvestmentDate,
                    CdiRate = x.CdiRate,
                    IsStock = x.IsStock,
                    FixedIncomeType = x.FixedIncomeType,
                };

            }).ToList();

            var pageSize = 10;

            var paged = allStocks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new IPagedResult<GetAllStockResponse>()
            {
                Items = paged,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = allStocks.Count,
            };
        }

        public async Task<IPagedResult<GetAllStockResponse>> GetAllFundsAsync(long accountId, int page = 1)
        {
            var funds = (await _stockRepository.GetAllAsync(accountId)).ToList();

            var allFunds = funds.Where(x => x.IsStock == false).Select(x =>
            {
                var priceMarket = x.PriceMarket;
                var percentage = Variation(priceMarket, x.PriceBuyed);
                return new GetAllStockResponse()
                {
                    Ticker = x.Ticker,
                    PriceBuyed = x.PriceBuyed,
                    PriceMarket = priceMarket,
                    Percentage = $"{Math.Round(percentage, 2)}%",
                    Quantity = x.Quantity,
                    InvestmentDate = x.InvestmentDate,
                    CdiRate = x.CdiRate,
                    IsStock = x.IsStock,
                    FixedIncomeType = x.FixedIncomeType
                };

            }).ToList();

            var pageSize = 10;

            var paged = allFunds
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new IPagedResult<GetAllStockResponse>()
            {
                Items = paged,
                PageNumber = page,
                PageSize = pageSize,
                TotalRecords = allFunds.Count,
            };
        }

        // A compra é gravada mesmo sem cotação; o preço de mercado anterior é mantido.
        private async Task AddToPositionAsync(Stock stock, CreateStockRequest request)
        {
            var totalQuantity = stock.Quantity + request.Quantity;
            var averagePrice = (stock.Quantity * stock.PriceBuyed + request.Quantity * request.Price) / totalQuantity;

            var priceMarket = await GetMarketPriceAsync(stock.Ticker) ?? stock.PriceMarket;

            // Altera o registro lido, para não apagar CreatedAt.
            stock.Quantity = totalQuantity;
            stock.PriceBuyed = averagePrice;
            stock.PriceMarket = priceMarket;
            stock.Avarage = $"{Math.Round(Variation(priceMarket, averagePrice), 2)}%";
            stock.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.BeginTransaction();
            await _stockRepository.UpdateAsync(stock);
            _unitOfWork.Commit();
        }

        private async Task<decimal?> GetMarketPriceAsync(string ticker)
        {
            var quotes = await _stockMarketService.GetStockByTickerAsync([ticker]);
            var quote = quotes.FirstOrDefault(x => string.Equals(x.Ticker, ticker, StringComparison.OrdinalIgnoreCase));

            return quote is { PriceMarket: > 0 } ? quote.PriceMarket : null;
        }

        // Sem cotação não há variação a mostrar (e não uma queda de 100%).
        private static decimal Variation(decimal priceMarket, decimal priceBuyed)
            => priceMarket > 0 && priceBuyed > 0 ? (priceMarket - priceBuyed) / priceBuyed * 100 : 0;

    }
}

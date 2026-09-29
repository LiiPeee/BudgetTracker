using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Models.Output;
using BudgetTracker.Core.Domain.Models.Request.Stock;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using BudgetTracker.Core.Infrastructure.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace BudgetTracker.Application.Service
{
    public class StockAppService(IStockRepository stockRepository, IStockMarketService stockMarketService, IUnitOfWork unitOfWork) : IStockAppService
    {
        private readonly IStockRepository _stockRepository = stockRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        private readonly IStockMarketService _stockMarketService = stockMarketService;
        public async Task CreateAsync(long accountId, CreateStockRequest request)
        {
            var stock = await _stockRepository.GetByStockAndAccountAsync(accountId, request.Ticker);

            if(stock is not null)
            {
               await UpdateStockAsync(stock, request);
               return;
            }

            var newStock = new Stock()
            {
                AccountId = accountId,
                Ticker = request.Ticker,
                Title = request.Title,
                Quantity = request.Quantity,
                Description = request.Description,
                FixedIncomeType = request.FixedIncomeType,
                PriceBuyed = request.Price,
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
                var percentage = x.PriceBuyed > 0
                        ? (priceMarket - x.PriceBuyed) / x.PriceBuyed * 100
                        : 0;
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
                var percentage = x.PriceBuyed > 0
                        ? (priceMarket - x.PriceBuyed) / x.PriceBuyed * 100
                        : 0;
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


        private async Task UpdateStockAsync(Stock stock, CreateStockRequest request) 
        {

            var existingPrice = stock.Quantity * stock.PriceBuyed;

            var priceBuyed = request.Quantity * request.Price;

            var totalPrice = priceBuyed + existingPrice;

            var totalQuantity = request.Quantity + stock.Quantity;

            var avaragePrice = totalPrice / totalQuantity;

            var liveStock = await _stockMarketService.GetStockByTickerAsync([stock.Ticker]);

            var result = liveStock.FirstOrDefault(x => x.Ticker == stock.Ticker);

            if (result is null ||  result.PriceMarket == 0) return;

            var priceMarket = result.PriceMarket;

            var percentage = avaragePrice > 0
                    ? (priceMarket - avaragePrice) / avaragePrice * 100
                    : 0;

            Stock updateStock = new Stock()
            {
                Id = stock.Id,
                Quantity = totalQuantity,
                AccountId = stock.AccountId,
                Title = stock.Title,
                PriceMarket = priceMarket,
                IsStock = stock.IsStock,
                Ticker = stock.Ticker,
                PriceBuyed = avaragePrice,
                Avarage = $"{Math.Round(percentage, 2)}%",
                CdiRate = stock.CdiRate,
                InvestmentDate = stock.InvestmentDate,
                FixedIncomeType = stock.FixedIncomeType,
            };

            _unitOfWork.BeginTransaction();

            await _stockRepository.UpdateAsync(updateStock);

            _unitOfWork.Commit();
        }

    }
}

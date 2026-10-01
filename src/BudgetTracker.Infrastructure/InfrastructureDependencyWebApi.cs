using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using BudgetTracker.Core.Infrastructure.Repository;
using BudgetTracker.Core.Infrastructure.Services;
using BudgetTracker.Infrastructure.Persistence.Repository;
using BudgetTracker.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetTracker.Infrastructure;

public static class InfrastructureDependencyWebApi
{
    public static IServiceCollection AddInfrastructureWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("Brevo", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("api-key", configuration["Brevo:ApiKey"]);
        });

        services.AddHttpClient("BrApi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient("Bacen", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services
        .AddScoped<DbSession>()
        .AddScoped<IAccountRepository, AccountRepository>()
        .AddScoped<ITransactionsRepository, TransactionsRepository>()
        .AddScoped<ICategoryRepository, CategoryRepository>()
        .AddScoped<IAddressRepository, AddressRepository>()
        .AddScoped<IContactRepository, ContactRepository>()
        .AddScoped<IBudgetLimitRepository, BudgetLimitRepository>()
        .AddScoped<ISubCategoryRepository, SubCategoryRepository>()
        .AddScoped<IResetPasswordRepository, ResetPasswordRepository>()
        .AddScoped<IStockRepository, StockRepository>()
        .AddScoped<IUnitOfWork, UnitOfWork>()
        .AddScoped<IEmailService, EmailService>()
        .AddScoped<IStockMarketService, StockMarketService>()
        .AddScoped<IBacenService, BacenService>();
    }
}



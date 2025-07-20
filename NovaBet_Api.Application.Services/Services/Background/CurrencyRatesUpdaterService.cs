using Microsoft.Extensions.Logging;
using NoviBet_Api.Core.ICoreServices;
using NoviBet_Api.Core.Models;
using Quartz;
using Microsoft.EntityFrameworkCore;

public class CurrencyRatesUpdaterJob : IJob
{
    private readonly INoviBet_Core _coreService;
    private readonly NoviBetContext _dbContext;
    private readonly ILogger<CurrencyRatesUpdaterJob> _logger;

    public CurrencyRatesUpdaterJob(INoviBet_Core coreService, NoviBetContext dbContext, ILogger<CurrencyRatesUpdaterJob> logger)
    {
        _coreService = coreService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("CurrencyRatesUpdaterJob triggered.");

        try
        {
            var rates = await _coreService.GetLatestRatesAsync();

            if (rates == null || rates.Count == 0)
            {
                _logger.LogWarning("No rates fetched.");
                return;
            }

            var rateDate = rates[0].Date;

            bool exists = await _dbContext.CurrencyRates
                .AsNoTracking()
                .AnyAsync(r => r.RateDate == rateDate);

            if (exists)
            {
                _logger.LogInformation($"Rates for date {rateDate} already exist. Skipping.");
                return;
            }

            var entities = rates.Select(r => new CurrencyRate
            {
                CurrencyCode = r.Currency,
                Rate = r.Rate,
                RateDate = r.Date
            });

            await _dbContext.CurrencyRates.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Inserted {rates.Count} new rates for {rateDate}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during currency rate update.");
        }
    }
}

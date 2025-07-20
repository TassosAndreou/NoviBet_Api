using NoviBet_Api.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NoviBet_Api.Core.ICoreServices;
using Shared.Dtos;
using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Http;
using System.Threading;
using Shared.Enums;
using Shared.Dtos.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;


namespace NoviBet_Api.Application.Services.Services
{
    public class NoviBetService : INovibetService
    {
        private readonly INoviBet_Core _noviBetService_Core;
        private readonly IConfiguration _configuration;
        private readonly CurrencySettings _settings;
        private readonly IMemoryCache _cache;
        private readonly string _cacheKey;


        public NoviBetService(INoviBet_Core noviBetService_Core,
                              IConfiguration configuration,
                              IOptions<CurrencySettings> settings,
                                IMemoryCache cache
                              )
        {

            _noviBetService_Core = noviBetService_Core;
            _configuration = configuration;
            _settings = settings.Value;
            _cache = cache;
            
            _cacheKey = _configuration.GetValue<string>("CacheKey") ?? "LatestCurrencyRates";
        }

        public async Task<List<CurrencyRateDto>> GetLatestRatesAsync()
        {
            var results = await _noviBetService_Core.GetLatestRatesAsync();

            return results;
        }

        public async Task<long> CreateWalletAsync(decimal initialBalance, string currency)
        {
            // You can add validation here
            return await _noviBetService_Core.CreateWalletAsync(initialBalance, currency);
        }

        public async Task<List<CurrencyRateDto>> GetLatestRatesDBAsync()
        {
            if (_cache.TryGetValue(_cacheKey, out List<CurrencyRateDto> cachedRates))
            {
                return cachedRates;
            }


            var rates = await _noviBetService_Core.GetLatestRatesDBAsync();

            var ratesDto = rates.Select(r => new CurrencyRateDto
            {
                Currency = r.CurrencyCode,
                Rate = r.Rate,
                Date = r.RateDate
            }).ToList();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10)); // Cache expires if not accessed for 10 mins

            _cache.Set(_cacheKey, ratesDto, cacheEntryOptions);

            return ratesDto;
        }

        public async Task<WalletBalanceDto?> GetWalletBalanceAsync(long walletId, string? convertToCurrency = null)
        {
            var wallet = await _noviBetService_Core.GetWalletAsync(walletId);
            if (wallet == null)
                return null;

            decimal balance = wallet.Balance;
            string walletCurrency = wallet.Currency.ToUpperInvariant();
            string targetCurrency = convertToCurrency?.ToUpperInvariant();

            if (string.IsNullOrEmpty(targetCurrency) || targetCurrency == walletCurrency)
            {
                // No conversion needed
                return new WalletBalanceDto
                {
                    Id = wallet.Id,
                    Balance = balance,
                    Currency = walletCurrency
                };
            }

            var rates = await GetLatestRatesDBAsync();

            // Add BaseCurrency manually (ECB rates don't include BaseCurrency)
            rates.Add(new CurrencyRateDto { Currency = _settings.BaseCurrency, Rate = 1m, Date = DateTime.UtcNow });

            var walletRate = rates.FirstOrDefault(r => r.Currency == walletCurrency);
            var targetRate = rates.FirstOrDefault(r => r.Currency == targetCurrency);

            if (walletRate == null)
                throw new Exception($"Currency rate not found for wallet currency: {walletCurrency}");

            if (targetRate == null)
                throw new Exception($"Currency rate not found for target currency: {targetCurrency}");

            decimal convertedBalance;

            if (walletCurrency == targetCurrency)
            {
                convertedBalance = balance;
            }
            else
            {
                // Convert based on ratio of target/wallet rates relative to BaseCurrency
                decimal conversionFactor = targetRate.Rate / walletRate.Rate;
                convertedBalance = balance * conversionFactor;
            }

            return new WalletBalanceDto
            {
                Id = wallet.Id,
                Balance = Math.Round(convertedBalance, 2),
                Currency = targetCurrency
            };
        }


        public async Task AdjustWalletBalanceAsync(long walletId, decimal amount, string amountCurrency, string strategy)
        {
            if (!Enum.TryParse<WalletBalanceAdjustmentStrategy>(strategy, ignoreCase: true, out var parsedStrategy))
                throw new ArgumentException("Invalid strategy.");

            if (string.IsNullOrWhiteSpace(amountCurrency))
                throw new ArgumentException("Amount currency must be specified.");

            await _noviBetService_Core.AdjustWalletBalanceAsync(walletId, amount, amountCurrency, parsedStrategy);
        }

    }
}

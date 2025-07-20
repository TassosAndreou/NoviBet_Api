using Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NoviBet_Api.Core.ICoreServices;
using System.Globalization;
using System.Net.Http;
using System.Xml.Linq;
using NoviBet_Api.Core.Models;
using Shared.Enums;
using Shared.Dtos.Settings;
using Microsoft.Extensions.Options;

namespace NoviBet_Api.Core.CoreServices
{
    public class Novibet_Core : INoviBet_Core
    { 
        private readonly HttpClient _httpClient;
        private readonly NoviBetContext _dbContext;
        private readonly CurrencySettings _settings;

        public Novibet_Core(HttpClient httpClient, NoviBetContext dbContext, IOptions<CurrencySettings> settings)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
            _settings = settings.Value;
        }


        public async Task<List<CurrencyRateDto>> GetLatestRatesAsync()
        {
            var response = await _httpClient.GetStringAsync(_settings.GetRatesUrl);

            var doc = XDocument.Parse(response);
            XNamespace ns = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";

            var dateAttr = doc.Descendants()
                              .FirstOrDefault(e => e.Name.LocalName == "Cube" && e.Attribute("time") != null)?
                              .Attribute("time")?.Value;

            if (dateAttr == null || !DateTime.TryParse(dateAttr, out DateTime date))
                throw new Exception("Unable to extract date from ECB XML.");

            var rates = doc.Descendants()
                           .Where(e => e.Name.LocalName == "Cube" && e.Attribute("currency") != null)
                           .Select(e => new CurrencyRateDto
                           {
                               Currency = e.Attribute("currency")!.Value,
                               Rate = decimal.Parse(e.Attribute("rate")!.Value, CultureInfo.InvariantCulture),
                               Date = date
                           })
                           .ToList();

            return rates;
        }

        public async Task<List<CurrencyRate>> GetLatestRatesDBAsync()
        {
            // Get the most recent date with currency rates
            var latestDate = await _dbContext.CurrencyRates
                .MaxAsync(r => (DateTime?)r.RateDate); 

            if (latestDate == null)
                return new List<CurrencyRate>();

            // Return all rates for that date
            return await _dbContext.CurrencyRates
                .Where(r => r.RateDate == latestDate.Value)
                .ToListAsync();
        }

        public async Task<long> CreateWalletAsync(decimal initialBalance, string currency)
        {
            var wallet = new Wallet
            {
                Balance = initialBalance,
                Currency = currency
            };

            await _dbContext.Wallets.AddAsync(wallet);
            await _dbContext.SaveChangesAsync();

            return wallet.Id;
        }

        public async Task<Wallet?> GetWalletAsync(long id)
        {
            return await _dbContext.Wallets.FindAsync(id);
        }

        public async Task AdjustWalletBalanceAsync(long walletId, decimal amount, string amountCurrency, WalletBalanceAdjustmentStrategy strategy)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive.");

            var wallet = await _dbContext.Wallets.FindAsync(walletId);
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found.");

            string walletCurrency = wallet.Currency.ToUpperInvariant();
            string incomingCurrency = amountCurrency.ToUpperInvariant();

            decimal convertedAmount = amount;

            if (walletCurrency != incomingCurrency)
            {
                // Get latest currency rates (base currency)
                var rates = await GetLatestRatesAsync();

                // Add BaseCurrency with rate 1
                rates.Add(new CurrencyRateDto { Currency = _settings.BaseCurrency, Rate = 1m, Date = DateTime.UtcNow });

                var walletRate = rates.FirstOrDefault(r => r.Currency == walletCurrency);
                var incomingRate = rates.FirstOrDefault(r => r.Currency == incomingCurrency);

                if (walletRate == null)
                    throw new Exception($"Rate not found for wallet currency: {walletCurrency}");
                if (incomingRate == null)
                    throw new Exception($"Rate not found for amount currency: {incomingCurrency}");

                // Convert incoming amount to wallet currency:
                // conversion factor = walletRate / incomingRate (inverse of what we did for balance display)
                decimal conversionFactor = walletRate.Rate / incomingRate.Rate;

                convertedAmount = amount * conversionFactor;
            }

            switch (strategy)
            {
                case WalletBalanceAdjustmentStrategy.AddFundsStrategy:
                    wallet.Balance += convertedAmount;
                    break;

                case WalletBalanceAdjustmentStrategy.SubtractFundsStrategy:
                    if (wallet.Balance < convertedAmount)
                        throw new InvalidOperationException("Insufficient funds.");
                    wallet.Balance -= convertedAmount;
                    break;

                case WalletBalanceAdjustmentStrategy.ForceSubtractFundsStrategy:
                    wallet.Balance -= convertedAmount;
                    break;

                default:
                    throw new ArgumentException("Invalid strategy.");
            }

            _dbContext.Wallets.Update(wallet);
            await _dbContext.SaveChangesAsync();
        }



    }



    //public async Task SaveChanges()
    //{
    //    await _dbContext.SaveChangesAsync();
    //}

}


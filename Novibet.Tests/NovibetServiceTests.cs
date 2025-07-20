using Moq;
using Xunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NoviBet_Api.Application.Services.Services;
using NoviBet_Api.Core.ICoreServices;
using NoviBet_Api.Core.Models;
using Shared.Dtos.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Dtos;
using Shared.Enums;

namespace Novibet.Tests
{
    public class NoviBetServiceTests
    {
        private readonly Mock<INoviBet_Core> _coreMock;
        private readonly NoviBetService _service;

        public NoviBetServiceTests()
        {
            _coreMock = new Mock<INoviBet_Core>();
            var configuration = new ConfigurationBuilder().Build();
            var currencySettings = Options.Create(new CurrencySettings { BaseCurrency = "EUR" });
            var cache = new MemoryCache(new MemoryCacheOptions());

            _service = new NoviBetService(_coreMock.Object, configuration, currencySettings, cache);
        }

        [Fact]
        public async Task GetWalletBalanceAsync_ReturnsBalance_WhenWalletExists()
        {
            // Arrange
            var walletId = 1;
            _coreMock.Setup(x => x.GetWalletAsync(walletId)).ReturnsAsync(new Wallet
            {
                Id = walletId,
                Balance = 100,
                Currency = "EUR"
            });

            // Act
            var result = await _service.GetWalletBalanceAsync(walletId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result!.Balance);
            Assert.Equal("EUR", result.Currency);
        }

        [Fact]
        public async Task GetLatestRatesAsync_ReturnsRatesFromCore()
        {
            // Arrange
            var fakeRates = new List<CurrencyRateDto>
            {
                new CurrencyRateDto { Currency = "EUR", Rate = 1m, Date = DateTime.UtcNow },
                new CurrencyRateDto { Currency = "USD", Rate = 1.1m, Date = DateTime.UtcNow }
            };
            _coreMock.Setup(x => x.GetLatestRatesAsync()).ReturnsAsync(fakeRates);

            // Act
            var result = await _service.GetLatestRatesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Currency == "EUR");
            Assert.Contains(result, r => r.Currency == "USD");
        }

        [Fact]
        public async Task CreateWalletAsync_CallsCoreAndReturnsWalletId()
        {
            // Arrange
            decimal initialBalance = 50m;
            string currency = "USD";
            long expectedWalletId = 123;

            _coreMock.Setup(x => x.CreateWalletAsync(initialBalance, currency))
                .ReturnsAsync(expectedWalletId);

            // Act
            var walletId = await _service.CreateWalletAsync(initialBalance, currency);

            // Assert
            Assert.Equal(expectedWalletId, walletId);
        }

        [Fact]
        public async Task GetWalletBalanceAsync_ReturnsConvertedBalance_WhenDifferentCurrency()
        {
            // Arrange
            long walletId = 1;
            string walletCurrency = "EUR";
            string targetCurrency = "USD";

            // Wallet has 100 EUR
            _coreMock.Setup(x => x.GetWalletAsync(walletId))
                .ReturnsAsync(new Wallet { Id = walletId, Balance = 100m, Currency = walletCurrency });

            // Mock rates for EUR and USD (BaseCurrency = EUR assumed in settings)
            var cachedRates = new List<CurrencyRateDto>
            {
                new CurrencyRateDto { Currency = "EUR", Rate = 1m, Date = DateTime.UtcNow },
                new CurrencyRateDto { Currency = "USD", Rate = 2m, Date = DateTime.UtcNow }
            };

            // Setup cache to simulate currency rates in memory cache
            // We cannot directly mock MemoryCache so we mock the core call to GetLatestRatesDBAsync instead
            _coreMock.Setup(x => x.GetLatestRatesDBAsync())
                .ReturnsAsync(new List<CurrencyRate> {
            new CurrencyRate { CurrencyCode = "EUR", Rate = 1m, RateDate = DateTime.UtcNow },
            new CurrencyRate { CurrencyCode = "USD", Rate = 2m, RateDate = DateTime.UtcNow }
                });

            // Replace service with one that uses our mock cache if needed, but for now, we can just test service directly
            // Act
            var result = await _service.GetWalletBalanceAsync(walletId, targetCurrency);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(walletId, result!.Id);
            Assert.Equal(targetCurrency, result.Currency);
            // 100 EUR * (2 / 1) = 200 USD
            Assert.Equal(200m, result.Balance);
        }

        [Fact]
        public async Task AdjustWalletBalanceAsync_CallsCore_WithValidStrategy()
        {
            // Arrange
            long walletId = 1;
            decimal amount = 50m;
            string amountCurrency = "USD";
            string strategy = "AddFundsStrategy";

            _coreMock.Setup(x => x.AdjustWalletBalanceAsync(walletId, amount, amountCurrency, WalletBalanceAdjustmentStrategy.AddFundsStrategy))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            await _service.AdjustWalletBalanceAsync(walletId, amount, amountCurrency, strategy);

            // Assert
            _coreMock.Verify();
        }

        [Fact]
        public async Task AdjustWalletBalanceAsync_ThrowsArgumentException_OnInvalidStrategy()
        {
            // Arrange
            long walletId = 1;
            decimal amount = 50m;
            string amountCurrency = "USD";
            string invalidStrategy = "InvalidStrategy";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.AdjustWalletBalanceAsync(walletId, amount, amountCurrency, invalidStrategy));
        }

        [Theory]
        [InlineData("USD", "EUR", 1.2, 1.1)]
        [InlineData("EUR", "USD", 1.1, 1.2)]
        [InlineData("GBP", "USD", 0.9, 1.3)]
        public async Task GetWalletBalanceAsync_ReturnsCorrectConvertedBalance(
    string walletCurrency, string targetCurrency, decimal walletRate, decimal targetRate)
        {
            // Arrange
            long walletId = 1;
            decimal balance = 100m;

            _coreMock.Setup(x => x.GetWalletAsync(walletId))
                .ReturnsAsync(new Wallet { Id = walletId, Balance = balance, Currency = walletCurrency });

            _coreMock.Setup(x => x.GetLatestRatesDBAsync())
                .ReturnsAsync(new List<CurrencyRate> {
            new CurrencyRate { CurrencyCode = walletCurrency, Rate = walletRate, RateDate = DateTime.UtcNow },
            new CurrencyRate { CurrencyCode = targetCurrency, Rate = targetRate, RateDate = DateTime.UtcNow }
                });

            // Act
            var result = await _service.GetWalletBalanceAsync(walletId, targetCurrency);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(walletId, result!.Id);
            Assert.Equal(targetCurrency, result.Currency);

            // Calculate expected converted balance
            var expectedBalance = Math.Round(balance * (targetRate / walletRate), 2);
            Assert.Equal(expectedBalance, result.Balance);
        }

    }

}

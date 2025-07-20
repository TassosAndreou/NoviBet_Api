using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NoviBet_Api.Core.Models;
using Shared.Dtos;

namespace NoviBet_Api.Application.Services.Interfaces
{
    public interface INovibetService
    {
        Task<List<CurrencyRateDto>> GetLatestRatesAsync();
        Task<long> CreateWalletAsync(decimal initialBalance, string currency);
        Task<WalletBalanceDto?> GetWalletBalanceAsync(long walletId, string? convertToCurrency = null);
        Task AdjustWalletBalanceAsync(long walletId, decimal amount, string amountCurrency, string strategy);
        Task<List<CurrencyRateDto>> GetLatestRatesDBAsync();
    }
}

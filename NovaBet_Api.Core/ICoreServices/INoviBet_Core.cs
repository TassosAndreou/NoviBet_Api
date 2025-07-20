using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NoviBet_Api.Core.Models;
using Shared.Dtos;
using Shared.Enums;

namespace NoviBet_Api.Core.ICoreServices
{
    public interface INoviBet_Core
    {
        Task<List<CurrencyRateDto>> GetLatestRatesAsync();
        //Task SaveChanges();
        Task<long> CreateWalletAsync(decimal initialBalance, string currency);
        Task<Wallet?> GetWalletAsync(long id);
        Task AdjustWalletBalanceAsync(long walletId, decimal amount, string amountCurrency, WalletBalanceAdjustmentStrategy strategy);

        Task<List<CurrencyRate>> GetLatestRatesDBAsync();
    }
}

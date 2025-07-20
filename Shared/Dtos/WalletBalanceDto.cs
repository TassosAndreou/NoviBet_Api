using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Dtos
{
    public class WalletBalanceDto
    {
        public long Id { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = null!;
    }
}

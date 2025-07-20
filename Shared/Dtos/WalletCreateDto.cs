using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Dtos
{
    public class WalletCreateDto
    {
        public decimal InitialBalance { get; set; }
        public string Currency { get; set; } = null!;
    }
}

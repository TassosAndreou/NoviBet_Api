using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Dtos.Settings
{
    public class CurrencySettings
    {
        public string BaseCurrency { get; set; } = "EUR";
        public string GetRatesUrl { get; set; } = string.Empty;
    }
}

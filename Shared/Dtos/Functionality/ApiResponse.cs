using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Dtos.Functionality
{
    public class ApiResponse
    {
        public bool IsSuccess { get; set; }

        public string? Data { get; set; }

        public string? ErrorMessage { get; set; }
    }
}

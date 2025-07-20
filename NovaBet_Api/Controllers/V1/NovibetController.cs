using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text;
using Shared.Dtos.Functionality;
using NoviBet_Api.Application.Services.Interfaces;
using Shared.Dtos;
using Microsoft.AspNetCore.RateLimiting;

namespace NoviBet_Api.Controllers.V1
{
    [Asp.Versioning.ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class NovibetController : CommonBaseController
    {
        private readonly INovibetService _novibetService;

        public NovibetController(INovibetService novibetService)
        {
            _novibetService = novibetService;
        }



        [HttpGet("curencyRates")]
        public async Task<IActionResult> GetLatestRatesAsync()
        {
            try
            {
                var result = await _novibetService.GetLatestRatesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while retrieving currency rates results");
                return StatusCode(500, new { message = "An error occurred while retrieving currency rates results." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateWallet([FromBody] WalletCreateDto dto)
        {
            try
            {
                var newId = await _novibetService.CreateWalletAsync(dto.InitialBalance, dto.Currency);
                return Ok(newId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating wallet");
                return StatusCode(500, new { message = "Error creating wallet" });
            }
        }

        [HttpGet("{walletId}")]
        [EnableRateLimiting("ClientIpPolicy")]
        public async Task<IActionResult> GetBalance(long walletId, [FromQuery] string? currency = null)
        {
            try
            {
                var balanceDto = await _novibetService.GetWalletBalanceAsync(walletId, currency);
                if (balanceDto == null)
                    return NotFound();

                return Ok(balanceDto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving wallet balance");
                return StatusCode(500, new { message = "Error retrieving wallet balance" });
            }
        }

        [HttpPost("{walletId}/adjustbalance")]
        public async Task<IActionResult> AdjustBalance(long walletId, [FromQuery] decimal amount, [FromQuery] string currency, [FromQuery] string strategy)
        {
            try
            {
                // Validate positive amount
                if (amount <= 0)
                    return BadRequest("Amount must be positive.");

                // Optional: Validate currency matches wallet currency, or do conversion here

                await _novibetService.AdjustWalletBalanceAsync(walletId, amount,currency, strategy);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                // For insufficient funds, etc.
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adjusting wallet balance");
                return StatusCode(500, new { message = "Error adjusting wallet balance" });
            }
        }


    }
}

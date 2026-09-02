using Microsoft.AspNetCore.Mvc;
using MyAIAgent.Services;

namespace MyAIAgent.Controllers
{
    /// <summary>
    /// Volatility Factor Research endpoints.
    ///
    /// POST /api/volatility/run
    ///   Body: { "symbols": ["AAPL","XOM",...] }
    ///   Runs RSI backtest for 2016–2026, groups results by annualised volatility bucket.
    ///
    /// POST /api/volatility/validate
    ///   Body: { "symbols": [...], "fromYear": 2006, "toYear": 2016 }
    ///   Same bucketing, different date range — for cross-period validation.
    /// </summary>
    [ApiController]
    [Route("api/volatility")]
    public class VolatilityController : ControllerBase
    {
        private readonly IVolatilityFactorService _service;

        public VolatilityController(IVolatilityFactorService service)
        {
            _service = service;
        }

        // POST /api/volatility/run
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] VolatilityRunRequest request)
        {
            if (request?.Symbols == null || request.Symbols.Count == 0)
                return BadRequest(new { error = "Provide at least one symbol." });

            var result = await _service.RunAsync(request.Symbols);
            return Ok(result);
        }

        // POST /api/volatility/validate
        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] VolatilityValidateRequest request)
        {
            if (request?.Symbols == null || request.Symbols.Count == 0)
                return BadRequest(new { error = "Provide at least one symbol." });

            var from = new DateTime(request.FromYear, 1, 1);
            var to = new DateTime(request.ToYear, 12, 31);

            var result = await _service.RunRangeAsync(request.Symbols, from, to);
            return Ok(result);
        }
    }

    public class VolatilityRunRequest
    {
        public List<string> Symbols { get; set; } = new();
    }

    public class VolatilityValidateRequest
    {
        public List<string> Symbols { get; set; } = new();
        public int FromYear { get; set; } = 2006;
        public int ToYear { get; set; } = 2016;
    }
}

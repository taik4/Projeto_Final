using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Endpoint de verificação de saúde da API
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            var response = new HealthResponse
            {
                Status = "ok",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Version = "1.0.0",
                Application = "TodoApi"
            };

            _logger.LogInformation("Health check accessed at {Time}", response.Timestamp);

            return Ok(response);
        }
    }
}

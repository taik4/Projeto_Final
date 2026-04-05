namespace TodoApi.Models
{
    public class HealthResponse
    {
        public string Status { get; set; } = "ok";
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        public string Version { get; set; } = "1.0.0";
        public string Application { get; set; } = "TodoApi";
    }
}

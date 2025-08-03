// Services/HeartbeatService.cs
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PatientApi.Services
{
    public class HeartbeatService : BackgroundService
    {
        private readonly ILogger<HeartbeatService> _logger;
        private readonly Uri _webSocketUri = new Uri("ws://localhost:4000/heartbeat");

        public HeartbeatService(ILogger<HeartbeatService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                try
                {
                    await ws.ConnectAsync(_webSocketUri, stoppingToken);

                    while (ws.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                    {
                        var heartbeat = new
                        {
                            serviceName = "patient-api",
                            podName = Environment.MachineName,
                            status = "running",
                            message = "Patient API is alive",
                            timestamp = DateTime.UtcNow
                        };

                        string json = JsonSerializer.Serialize(heartbeat);
                        var buffer = Encoding.UTF8.GetBytes(json);

                        await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, stoppingToken);
                        _logger.LogInformation("Sent heartbeat to WebSocket server.");

                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send heartbeat");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Retry delay
                }
            }
        }
    }
}

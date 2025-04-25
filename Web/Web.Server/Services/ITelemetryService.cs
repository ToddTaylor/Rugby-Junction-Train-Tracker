using Web.Server.Models;

namespace Web.Server.Services
{
    public interface ITelemetryService
    {
        void ProcessTelemetry(Telemetry alert);
    }
}
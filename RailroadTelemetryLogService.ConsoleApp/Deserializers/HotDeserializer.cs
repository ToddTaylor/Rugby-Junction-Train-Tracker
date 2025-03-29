using RailroadTelemetryLogService.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RailroadTelemetryLogService.ConsoleApp.Deserializers
{
    public class HotDeserializer
    {
        public HotDeserializer() { }

        public static HotPacket Deserialize(string data)
        {
            data = ReplaceMultipleSpaces(data);

            var rawDataArray = SplitIntoToArrayBySpaces(data);

            var packet = new HotPacket()
            {
                TimeReceived = ConvertToDateTime(rawDataArray[0]),
                EstimatedSignalStrength = ConvertToDecimal(rawDataArray[1]),
                Source = ConvertToNullIfDash(rawDataArray[2]),
                ID = ConvertToInteger(rawDataArray[3]),
                BreakPipePressure = ConvertToInteger(rawDataArray[4]),
                MotionStatus = ConvertToInteger(rawDataArray[5]),
                MarkerLightStatus = ConvertToInteger(rawDataArray[6]),
                BatteryStatus = ConvertToNullIfDash(rawDataArray[7]),
                BatteryChargeUsed = ConvertToInteger(rawDataArray[8]),
                AirTurbineEquipped = ConvertToInteger(rawDataArray[9]),
                Command = ConvertToNullIfDash(rawDataArray[10]),
                MessageType = ConvertToNullIfDash(rawDataArray[11]),
                EmergencyValveHealth = ConvertToInteger(rawDataArray[12]),
                TwoWayLinkConfirmation = ConvertToInteger(rawDataArray[13]),
                MovementRailroad = ConvertToNullIfDash(rawDataArray[14]),
                MovementSymbol = ConvertToNullIfDash(rawDataArray[15])
            };

            // Display each element value in the array, prefixed by its position in the array
            for (int i = 0; i < rawDataArray.Length; i++)
            {
                Debug.WriteLine($"{i}: {rawDataArray[i]}");
            }

            return packet;
        }

        private static string[] SplitIntoToArrayBySpaces(string data)
        {
            return data.Split(' ', StringSplitOptions.TrimEntries);
        }

        private static string ReplaceMultipleSpaces(string data)
        {
            return Regex.Replace(data, @"\s+", " ");
        }

        private static DateTime ConvertToDateTime(string input)
        {
            return DateTime.ParseExact(input, "yyyy/MM/dd-HH:mm:ss", null);
        }

        private static decimal? ConvertToDecimal(string input)
        {
            return decimal.TryParse(input, out decimal output) ? output : null;
        }

        private static int? ConvertToInteger(string input)
        {
            return int.TryParse(input, out int output) ? output : null;
        }

        private static string? ConvertToNullIfDash(string input)
        {
            return string.IsNullOrWhiteSpace(input.Replace("-", "")) ? null : input;
        }
    }
}

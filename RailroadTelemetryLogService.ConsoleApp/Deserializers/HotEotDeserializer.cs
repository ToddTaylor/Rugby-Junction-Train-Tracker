using ConsoleApp.Models;
using System.Text.RegularExpressions;

namespace ConsoleApp.Deserializers
{
    public class HotEotDeserializer
    {
        public HotEotDeserializer() { }

        public static HotEotPacket Deserialize(string data)
        {
            data = ReplaceMultipleSpaces(data);

            var rawDataArray = SplitIntoToArrayBySpaces(data);

            var packet = new HotEotPacket()
            {
                TimeReceived = ConvertToDateTime(rawDataArray[0]),
                SIG = ConvertToDecimal(rawDataArray[1]),
                SRC = ConvertToNullIfDash(rawDataArray[2]),
                ID = ConvertToInteger(rawDataArray[3]),
                BP = ConvertToInteger(rawDataArray[4]),
                MOT = ConvertToInteger(rawDataArray[5]),
                MarkerLightStatus = ConvertToInteger(rawDataArray[6]),
                BATST = ConvertToNullIfDash(rawDataArray[7]),
                BATCU = ConvertToInteger(rawDataArray[8]),
                TRB = ConvertToInteger(rawDataArray[9]),
                CMD = ConvertToNullIfDash(rawDataArray[10]),
                TYP = ConvertToNullIfDash(rawDataArray[11]),
                VLV = ConvertToInteger(rawDataArray[12]),
                CNF = ConvertToInteger(rawDataArray[13]),
                RR = ConvertToNullIfDash(rawDataArray[14]),
                SYMB = ConvertToNullIfDash(rawDataArray[15])
            };

            // Display each element value in the array, prefixed by its position in the array
            //for (int i = 0; i < rawDataArray.Length; i++)
            //{
            //    Debug.WriteLine($"{i}: {rawDataArray[i]}");
            //}

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

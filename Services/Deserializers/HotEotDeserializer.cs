using Services.Models;

namespace Services.Deserializers
{
    public class HotEotDeserializer : DeserializerBase, IDeserializer<HotEotPacket>
    {
        public static HotEotPacket Deserialize(string data)
        {
            data = ReplaceMultipleSpaces(data);

            var rawDataArray = SplitIntoToArrayBySpaces(data);

            var packet = new HotEotPacket()
            {
                TimeReceived = ConvertToDateTimeUTC(rawDataArray[0]),
                SIG = ConvertToDecimal(rawDataArray[1]),
                SRC = ConvertToNullIfDash(rawDataArray[2]),
                ID = ConvertToNullIfDash(rawDataArray[3]),
                BP = ConvertToInteger(rawDataArray[4]),
                MOT = ConvertToInteger(rawDataArray[5]),
                MRK = ConvertToInteger(rawDataArray[6]),
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

            return packet;
        }
    }
}

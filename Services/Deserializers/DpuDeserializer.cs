using Services.Models;

namespace Services.Deserializers
{
    public class DpuDeserializer : DeserializerBase, IDeserializer<DpuPacket>
    {
        public static DpuPacket Deserialize(string data)
        {
            data = ReplaceMultipleSpaces(data);

            var rawDataArray = SplitIntoToArrayBySpaces(data);

            var packet = new DpuPacket()
            {
                TimeReceived = ConvertToDateTimeUTC(rawDataArray[0]),
                SIG = ConvertToDecimal(rawDataArray[1]),
                ADDR = ConvertToNullIfDash(rawDataArray[2]),
                TRID = ConvertToNullIfDash(rawDataArray[3]),
                TP = ConvertToNullIfDash(rawDataArray[4]),
                OR = ConvertToNullIfDash(rawDataArray[5]),
                RP = ConvertToNullIfDash(rawDataArray[6]),
                SEQ = ConvertToInteger(rawDataArray[7]),
                NRM = ConvertToInteger(rawDataArray[8]),
                RMID = ConvertToInteger(rawDataArray[9]),
                Power = ConvertToNullIfDash(rawDataArray[10]),
                REV = ConvertToNullIfDash(rawDataArray[11]),
                MTR = ConvertToInteger(rawDataArray[12]),
                BVIN = ConvertToInteger(rawDataArray[13]),
                Sand = ConvertToInteger(rawDataArray[14]),
                PRK = ConvertToInteger(rawDataArray[15]),
                TRE = ConvertToInteger(rawDataArray[16]),
                BPRED = ConvertToNullIfDash(rawDataArray[17]),
                BP = ConvertToDecimal(rawDataArray[18]),
                ER = ConvertToDecimal(rawDataArray[19]),
                AF = ConvertToInteger(rawDataArray[20]),
                MR = ConvertToInteger(rawDataArray[21]),
                IB = ConvertToInteger(rawDataArray[22]),
                BC = ConvertToDecimal(rawDataArray[23]),
                LOWN = ConvertToNullIfDash(rawDataArray[24]),
                LLOC = ConvertToNullIfDash(rawDataArray[25]),
                ROWN = ConvertToNullIfDash(rawDataArray[26]),
                RLOC = ConvertToNullIfDash(rawDataArray[27]),
                MISC = ConvertToNullIfDash(rawDataArray[28]),
                RR = ConvertToNullIfDash(rawDataArray[29]),
                SYMB = ConvertToNullIfDash(rawDataArray[30]),
            };

            return packet;
        }
    }
}
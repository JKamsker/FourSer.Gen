using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_HOTSEND_REQ)]
    public partial class CsHotsendReq
    {
        /// <summary>
        /// Original name: dlValue. A check value.
        /// </summary>
        public long Value;

        /// <summary>
        /// Original name: bAll. A flag.
        /// </summary>
        public byte All;
    }
}

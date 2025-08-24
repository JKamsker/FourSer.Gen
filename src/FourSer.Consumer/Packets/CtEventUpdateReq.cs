using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_EVENTUPDATE_REQ)]
    public partial class CtEventUpdateReq
    {
        /// <summary>
        /// Original name: bEventID. The ID of the event to update.
        /// </summary>
        public byte EventID;

        /// <summary>
        /// Original name: wValue. The new value for the event.
        /// </summary>
        public ushort Value;
    }
}

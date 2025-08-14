using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEMONITOR_REQ)]
    public partial class CtServiceMonitorReq
    {
        /// <summary>
        /// Original name: dwTick. A tick value for monitoring.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Original name: dwSession. The number of sessions.
        /// </summary>
        public uint Session;

        /// <summary>
        /// Original name: dwUser. The number of users.
        /// </summary>
        public uint User;

        /// <summary>
        /// Original name: dwActiveUser. The number of active users.
        /// </summary>
        public uint ActiveUser;
    }
}

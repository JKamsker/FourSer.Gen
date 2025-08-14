using System;

namespace Serializer.Consumer.Packets
{
    [AttributeUsage(AttributeTargets.Class)]
    public class OpCodeAttribute : Attribute
    {
        public OpCode OpCode { get; }

        public OpCodeAttribute(OpCode opCode)
        {
            OpCode = opCode;
        }
    }
}

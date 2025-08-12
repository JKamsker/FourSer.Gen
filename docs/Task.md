# Serializer SourceGen

- NET 9
- Generates source code for serializing and deserializing objects based on attributes and conventions

example:
```csharp
[GenerateSerializer]
public partial class LoginAckPacket
{
    public byte bResult;
    public uint dwUserID;
    public uint dwKickID;
    public uint dwKEY;
    public uint Address;
    public ushort Port;
    public byte bCreateCardCnt;
    public byte bInPcRoom;
    public uint dwPremiumPcRoom;
    public long dCurrentTime;
    public long dKey;
}
```

Generated Code:
```csharp

public partial class LoginAckPacket : ISerializable<LoginAckPacket>
{
    // In ISerializable<>
    public static int GetPacketSize(in LoginAckPacket packet)
    {
        return
            sizeof(byte) 	/* BYTE bResult */
            + sizeof(uint) 	/* DWORD dwUserID */
            + sizeof(uint) 	/* DWORD dwKickID */
            + sizeof(uint) 	/* DWORD dwKEY */
            + sizeof(uint) 	/* DWORD Address */
            + sizeof(ushort) 	/* USHORT Port */
            + sizeof(byte) 	/* BYTE bCreateCardCnt */
            + sizeof(byte) 	/* BYTE bInPcRoom */
            + sizeof(uint) 	/* DWORD dwPremiumPcRoom */
            + sizeof(long) 	/* __time64_t dCurrentTime */
            + sizeof(long)     /* INT64 dKey */;
    }

    // ReadXXX come from existing extension methods
    public static LoginAckPacket Deserialize(ref ReadOnlySpan<byte> sourceStream)
    {
        var bResult = sourceStream.ReadByte();
        var dwUserID = sourceStream.ReadUInt32();
        var dwKickID = sourceStream.ReadUInt32();
        var dwKEY = sourceStream.ReadUInt32();
        var Address = sourceStream.ReadUInt32();
        var Port = sourceStream.ReadUInt16();
        var bCreateCardCnt = sourceStream.ReadByte();
        var bInPcRoom = sourceStream.ReadByte();
        var dwPremiumPcRoom = sourceStream.ReadUInt32();
        var dCurrentTime = sourceStream.ReadInt64();
        var dKey = sourceStream.ReadInt64();

        return new LoginAckPacket
        (
            bResult: bResult,
            dwUserID: dwUserID,
            dwKickID: dwKickID,
            dwKEY: dwKEY,
            Address: Address,
            Port: Port,
            bCreateCardCnt: bCreateCardCnt,
            bInPcRoom: bInPcRoom,
            dwPremiumPcRoom: dwPremiumPcRoom,
            dCurrentTime: dCurrentTime,
            dKey: dKey
        );
    }

    public static void Serialize(LoginAckPacket packet, ref Span<byte> outputStream)
    {
        outputStream.WriteByte(packet.bResult);
        outputStream.WriteUInt32(packet.dwUserID);
        outputStream.WriteUInt32(packet.dwKickID);
        outputStream.WriteUInt32(packet.dwKEY);
        outputStream.WriteUInt32(packet.Address);
        outputStream.WriteUInt16(packet.Port);
        outputStream.WriteByte(packet.bCreateCardCnt);
        outputStream.WriteByte(packet.bInPcRoom);
        outputStream.WriteUInt32(packet.dwPremiumPcRoom);
        outputStream.WriteInt64(packet.dCurrentTime);
        outputStream.WriteInt64(packet.dKey);
    }
}

```

definition of `ISerializable<T>`:
```csharp
public interface ISerializable<T>
{
    static abstract int GetPacketSize(in T packet);
    static abstract T Deserialize(ref ReadOnlySpan<byte> sourceStream);
    static abstract void Serialize(T packet, ref Span<byte> outputStream);
}
```

Special cases:
## Dynamic collection size
```csharp
[GenerateSerializer]
public partial class MyPacket
{
    // It is implicitly assumed, that the count of the collection is int32 (4 bytes)
    // Or we can use [SerializeCollection(CountSize = 2)] to specify the static count size
    // Or we can specify the count type by using [SerializeCollection(CountType = typeof(ushort))]
    [SerializeCollection]
    public Entity[] Entities { get; set; }

    public partial class Entity{
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
```


Generated Code:
```csharp
public partial class MyPacket : ISerializable<MyPacket>
{
    public static int GetPacketSize(in MyPacket packet)
    {
        int size = sizeof(int); // Entity count
        for (int i = 0; i < packet.Entities.Length; i++)
        {
            size += sizeof(int); // Entity.Id
            size += sizeof(int) + Encoding.UTF8.GetByteCount(packet.Entities[i].Name); // Entity.Name
        }
        return size;
    }

    public static MyPacket Deserialize(ref ReadOnlySpan<byte> sourceStream)
    {
        var entityCount = sourceStream.ReadInt32();
        var entities = new Entity[entityCount];
        
        for (int i = 0; i < entityCount; i++)
        {
            var id = sourceStream.ReadInt32();
            var name = sourceStream.ReadString();
            entities[i] = new Entity { Id = id, Name = name };
        }

        return new MyPacket
        {
            Entities = entities
        };
    }

    public static void Serialize(MyPacket packet, ref Span<byte> outputStream)
    {
        outputStream.WriteInt32(packet.Entities.Length);
        for (int i = 0; i < packet.Entities.Length; i++)
        {
            outputStream.WriteInt32(packet.Entities[i].Id);
            outputStream.WriteString(packet.Entities[i].Name);
        }
    }
}
```

## Count is not directly preceding the collection
```csharp
[GenerateSerializer]
public partial class MyPacket
{
    public int EntityCount { get; set; }

    public string Name { get; set; }

    [SerializeCollection(CountSizeReference = nameof(EntityCount))]
    public Entity[] Entities { get; set; }

    public partial class Entity{
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
```

Generated Code:
```csharp
public partial class MyPacket : ISerializable<MyPacket>
{
    public static int GetPacketSize(in MyPacket packet)
    {
        int size = sizeof(int); // EntityCount
        size += sizeof(int) + Encoding.UTF8.GetByteCount(packet.Name); // Name
        
        for (int i = 0; i < packet.EntityCount; i++)
        {
            size += sizeof(int); // Entity.Id
            size += sizeof(int) + Encoding.UTF8.GetByteCount(packet.Entities[i].Name); // Entity.Name
        }
        return size;
    }

    public static MyPacket Deserialize(ref ReadOnlySpan<byte> sourceStream)
    {
        var entityCount = sourceStream.ReadInt32();
        var name = sourceStream.ReadString();
        var entities = new Entity[entityCount];
        
        for (int i = 0; i < entityCount; i++)
        {
            var id = sourceStream.ReadInt32();
            var entityName = sourceStream.ReadString();
            entities[i] = new Entity { Id = id, Name = entityName };
        }

        return new MyPacket
        {
            EntityCount = entityCount,
            Name = name,
            Entities = entities
        };
    }

    public static void Serialize(MyPacket packet, ref Span<byte> outputStream)
    {
        outputStream.WriteInt32(packet.Entities.Length);
        outputStream.WriteString(packet.Name);
        for (int i = 0; i < packet.Entities.Length; i++)
        {
            outputStream.WriteInt32(packet.Entities[i].Id);
            outputStream.WriteString(packet.Entities[i].Name);
        }
    }
}
```



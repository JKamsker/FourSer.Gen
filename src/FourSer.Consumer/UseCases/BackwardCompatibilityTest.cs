using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

/// <summary>
/// Test class to verify backward compatibility with existing functionality
/// after implementing the nested type serialization fix
/// </summary>
public class BackwardCompatibilityTest
{
    /// <summary>
    /// Test that primitive types continue to generate the same code as before
    /// Requirements: 3.1, 3.2, 3.3, 3.4
    /// </summary>
    public void PrimitiveTypesBackwardCompatibilityTest()
    {
        // Test with LoginAckPacket which contains various primitive types
        var original = new LoginAckPacket
        {
            bResult = 1,
            dwUserID = 12345,
            dwKickID = 67890,
            dwKEY = 0xDEADBEEF,
            Address = 0x7F000001, // 127.0.0.1
            Port = 8080,
            bCreateCardCnt = 5,
            bInPcRoom = 1,
            dwPremiumPcRoom = 999,
            dCurrentTime = 1640995200000, // Unix timestamp
            dKey = 0x1234567890ABCDEF
        };

        var size = LoginAckPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        LoginAckPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = LoginAckPacket.Deserialize(readOnlySpan);

        // Verify all primitive types are correctly serialized/deserialized
        Assert.AreEqual(original.bResult, deserialized.bResult);
        Assert.AreEqual(original.dwUserID, deserialized.dwUserID);
        Assert.AreEqual(original.dwKickID, deserialized.dwKickID);
        Assert.AreEqual(original.dwKEY, deserialized.dwKEY);
        Assert.AreEqual(original.Address, deserialized.Address);
        Assert.AreEqual(original.Port, deserialized.Port);
        Assert.AreEqual(original.bCreateCardCnt, deserialized.bCreateCardCnt);
        Assert.AreEqual(original.bInPcRoom, deserialized.bInPcRoom);
        Assert.AreEqual(original.dwPremiumPcRoom, deserialized.dwPremiumPcRoom);
        Assert.AreEqual(original.dCurrentTime, deserialized.dCurrentTime);
        Assert.AreEqual(original.dKey, deserialized.dKey);
    }

    /// <summary>
    /// Test that string types continue to work unchanged
    /// Requirements: 3.2, 3.3
    /// </summary>
    public void StringTypesBackwardCompatibilityTest()
    {
        // Test with LoginReqPacket which contains string fields
        var original = new LoginReqPacket
        {
            wVersion = 1001,
            strUserID = "testuser123",
            strPasswd = "securepassword456",
            dlCheck = 0x123456789ABCDEF0
        };

        var size = LoginReqPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        LoginReqPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = LoginReqPacket.Deserialize(readOnlySpan);

        // Verify strings are correctly serialized/deserialized
        Assert.AreEqual(original.wVersion, deserialized.wVersion);
        Assert.AreEqual(original.strUserID, deserialized.strUserID);
        Assert.AreEqual(original.strPasswd, deserialized.strPasswd);
        Assert.AreEqual(original.dlCheck, deserialized.dlCheck);
    }

    /// <summary>
    /// Test that unmanaged types in collections continue to work unchanged
    /// Requirements: 3.1, 3.4
    /// </summary>
    public void UnmanagedTypesInCollectionsBackwardCompatibilityTest()
    {
        // Test with MyPacket which contains List<byte> (unmanaged type collection)
        var original = new MyPacket
        {
            Data = new List<byte> { 0x01, 0x02, 0x03, 0x04, 0x05, 0xFF, 0xAA, 0xBB }
        };

        var size = MyPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        MyPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = MyPacket.Deserialize(readOnlySpan);

        // Verify unmanaged type collections work unchanged
        Assert.AreEqual(original.Data.Count, deserialized.Data.Count);
        Assert.AreEqual(true, original.Data.SequenceEqual(deserialized.Data));
    }

    /// <summary>
    /// Test that non-nested reference types in collections continue to work unchanged
    /// Requirements: 3.1, 3.2, 3.3, 3.4
    /// </summary>
    public void NonNestedReferenceTypesInCollectionsBackwardCompatibilityTest()
    {
        // Test with TestWithListOfReferenceTypes which contains List<CXEntity> (non-nested reference type)
        var original = new TestWithListOfReferenceTypes
        {
            MyList = new List<CXEntity>
            {
                new CXEntity { Id = 100, Name = "Entity100" },
                new CXEntity { Id = 200, Name = "Entity200" },
                new CXEntity { Id = 300, Name = "Entity300" }
            }
        };

        var size = TestWithListOfReferenceTypes.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithListOfReferenceTypes.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithListOfReferenceTypes.Deserialize(readOnlySpan);

        // Verify non-nested reference types in collections work unchanged
        Assert.AreEqual(original.MyList.Count, deserialized.MyList.Count);
        for (int i = 0; i < original.MyList.Count; i++)
        {
            Assert.AreEqual(original.MyList[i].Id, deserialized.MyList[i].Id);
            Assert.AreEqual(original.MyList[i].Name, deserialized.MyList[i].Name);
        }
    }

    /// <summary>
    /// Test that collection attributes (CountType, CountSizeReference) continue to work unchanged
    /// Requirements: 3.1, 3.2, 3.3, 3.4
    /// </summary>
    public void CollectionAttributesBackwardCompatibilityTest()
    {
        // Test CountType attribute
        var countTypeTest = new TestWithCountType
        {
            MyList = new List<int> { 1000, 2000, 3000, 4000, 5000 }
        };

        var size1 = TestWithCountType.GetPacketSize(countTypeTest);
        var buffer1 = new byte[size1];
        var span1 = new Span<byte>(buffer1);
        TestWithCountType.Serialize(countTypeTest, span1);
        var readOnlySpan1 = new ReadOnlySpan<byte>(buffer1);
        var deserialized1 = TestWithCountType.Deserialize(readOnlySpan1);

        Assert.AreEqual(true, countTypeTest.MyList.SequenceEqual(deserialized1.MyList));

        // Test CountSizeReference attribute
        var countSizeRefTest = new TestWithCountSizeReference
        {
            MyList = new List<int> { 10000, 20000, 30000 }
        };
        countSizeRefTest.MyListCount = (ushort)countSizeRefTest.MyList.Count;

        var size2 = TestWithCountSizeReference.GetPacketSize(countSizeRefTest);
        var buffer2 = new byte[size2];
        var span2 = new Span<byte>(buffer2);
        TestWithCountSizeReference.Serialize(countSizeRefTest, span2);
        var readOnlySpan2 = new ReadOnlySpan<byte>(buffer2);
        var deserialized2 = TestWithCountSizeReference.Deserialize(readOnlySpan2);

        Assert.AreEqual(countSizeRefTest.MyListCount, deserialized2.MyListCount);
        Assert.AreEqual(true, countSizeRefTest.MyList.SequenceEqual(deserialized2.MyList));
    }

    /// <summary>
    /// Test that mixed fields and properties continue to work unchanged
    /// Requirements: 3.1, 3.2, 3.3, 3.4
    /// </summary>
    public void MixedFieldsAndPropertiesBackwardCompatibilityTest()
    {
        var original = new MixedFieldsAndPropsPacket
        {
            PropertyInt = 12345,
            PropertyString = "PropertyValue",
            FieldInt = 67890,
            FieldString = "FieldValue",
            FieldFloat = 3.14159f
        };

        var size = MixedFieldsAndPropsPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        MixedFieldsAndPropsPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = MixedFieldsAndPropsPacket.Deserialize(readOnlySpan);

        // Verify mixed fields and properties work unchanged
        Assert.AreEqual(original.PropertyInt, deserialized.PropertyInt);
        Assert.AreEqual(original.PropertyString, deserialized.PropertyString);
        Assert.AreEqual(original.FieldInt, deserialized.FieldInt);
        Assert.AreEqual(original.FieldString, deserialized.FieldString);
        Assert.AreEqual(original.FieldFloat, deserialized.FieldFloat);
    }

    /// <summary>
    /// Test that nested objects (non-collection) continue to work unchanged
    /// Requirements: 3.1, 3.2, 3.3, 3.4
    /// </summary>
    public void NestedObjectsBackwardCompatibilityTest()
    {
        var original = new ContainerPacket
        {
            Nested = new NestedPacket { Id = 999 },
            Name = "ContainerName"
        };

        var size = ContainerPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        ContainerPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = ContainerPacket.Deserialize(readOnlySpan);

        // Verify nested objects work unchanged
        Assert.AreEqual(original.Name, deserialized.Name);
        Assert.AreEqual(original.Nested.Id, deserialized.Nested.Id);
    }
}
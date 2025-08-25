using System.Diagnostics;
using System.Text;
using FourSer.Gen.Helpers;
using Xunit;

namespace FourSer.Tests.Behavioural.Demo;

using FourSer.Contracts;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Represents the entire TItem.tcd file structure.
/// It contains a list of TItem entries, prefixed by a count of type ushort.
/// </summary>
[GenerateSerializer]
public partial class TItemChart
{
    /// <summary>
    /// A list of all item templates. The binary file starts with a WORD (ushort)
    /// indicating the number of items, which this attribute handles automatically.
    /// </summary>
    [SerializeCollection(CountType = typeof(ushort))]
    public List<TItem> Items { get; set; } = new();
}

/// <summary>
/// Represents a single item template entry, corresponding to the C++ tagTITEM struct.
/// </summary>
[GenerateSerializer]
public partial class TItem
{
    public ushort ItemID { get; set; }
    public byte Type { get; set; }
    public byte Kind { get; set; }
    public ushort AttrID { get; set; }
    [Serializer(typeof(MfcStringSerializer))]
    public string Name { get; set; }
    public ushort UseValue { get; set; }
    public uint SlotID { get; set; }
    public uint ClassID { get; set; }
    public byte PrmSlotID { get; set; }
    public byte SubSlotID { get; set; }
    public byte Level { get; set; }
    public byte CanRepair { get; set; }
    public uint DuraMax { get; set; }
    public byte RefineMax { get; set; }
    public float PriceRate { get; set; }
    public uint Price { get; set; }
    public byte MinRange { get; set; }
    public byte MaxRange { get; set; }
    public byte Stack { get; set; }
    public byte SlotCount { get; set; }
    public byte CanGamble { get; set; }
    public byte GambleProb { get; set; }
    public byte DestoryProb { get; set; }
    public byte CanGrade { get; set; }
    public byte CanMagic { get; set; }
    public byte CanRare { get; set; }
    public ushort DelayGroupID { get; set; }
    public uint Delay { get; set; }
    public byte CanTrade { get; set; }
    public byte IsSpecial { get; set; }
    public ushort UseTime { get; set; }
    public byte UseType { get; set; }
    public byte WeaponID { get; set; }
    public float ShotSpeed { get; set; }
    public float Gravity { get; set; }
    public uint InfoID { get; set; }
    public byte SkillItemType { get; set; }

    /// <summary>
    /// Fixed-size array for Visual data (m_wVisual).
    /// Serializes exactly 5 ushorts without a count prefix.
    /// </summary>
    [SerializeCollection(CountSize = 5)]
    public ushort[] Visual { get; set; } = new ushort[5];

    public ushort GradeSFX { get; set; }

    /// <summary>
    /// Fixed-size array for OptionSFX data (m_wOptionSFX).
    /// Serializes exactly 3 ushorts without a count prefix.
    /// </summary>
    [SerializeCollection(CountSize = 3)]
    public ushort[] OptionSFX { get; set; } = new ushort[3];

    public byte CanWrap { get; set; }
    public uint AuctionCode { get; set; }
    public byte CanColor { get; set; }
}

public class TitemReader
{
    // "C:\Users\W31rd0\source\repos\4Story\4StoryCC\4StoryCC_Client\Tcd\TItem.tcd"
    [Fact]
    public void ReadTitemFile()
    {
        var originalStream = GetTestFileStream();

        #region Perf
        // 900 vs 600ms for 100 deserializations
        // var sw = Stopwatch.StartNew();
        // for (int x = 0; x < 5; x++)
        // {
        //     sw.Restart();
        //     for (int i = 0; i < 100; i++)
        //     {
        //         TItemChart.Deserialize(originalStream);
        //         originalStream.Position = 0;
        //     }
        //     sw.Stop();
        //     Console.WriteLine($"Deserialized TItem.tcd 100 times in {sw.Elapsed.TotalMilliseconds} ms");
        //
        //     var bytes = originalStream.ToArray();
        //     sw.Restart();
        //     for (int i = 0; i < 100; i++)
        //     {
        //         TItemChart.Deserialize(bytes);
        //     }
        //     sw.Stop();
        //     Console.WriteLine($"Deserialized TItem.tcd from byte[] 100 times in {sw.Elapsed.TotalMilliseconds} ms");
        //     Console.WriteLine();
        // }
        

        #endregion
        
        
        // Read the original data
        var titemChart = TItemChart.Deserialize(originalStream);

        Assert.NotNull(titemChart);
        Assert.NotNull(titemChart.Items);
        Assert.True(titemChart.Items.Count > 0, "No items found in TItem.tcd");

        // Example assertions for the first item
        var firstItem = titemChart.Items[0];
        Assert.Equal(1, firstItem.ItemID); // Assuming the first item's ID is 1
        Assert.False(string.IsNullOrEmpty(firstItem.Name), "First item's name should not be empty");

        // Test round-trip serialization
        var serializedStream = new MemoryStream();
        TItemChart.Serialize(titemChart, serializedStream);
        
        // Test round-trip serialization by deserializing our serialized data
        serializedStream.Position = 0;
        var deserializedChart = TItemChart.Deserialize(serializedStream);
        
        // Verify the round-trip worked
        Assert.NotNull(deserializedChart);
        Assert.NotNull(deserializedChart.Items);
        Assert.Equal(titemChart.Items.Count, deserializedChart.Items.Count);
        
        // Check that the first few items match
        for (int i = 0; i < Math.Min(3, titemChart.Items.Count); i++)
        {
            var original = titemChart.Items[i];
            var roundTrip = deserializedChart.Items[i];
            
            Assert.Equal(original.ItemID, roundTrip.ItemID);
            Assert.Equal(original.Name, roundTrip.Name);
            Assert.Equal(original.Type, roundTrip.Type);
            Assert.Equal(original.Price, roundTrip.Price);
            Assert.Equal(original.Visual.Length, roundTrip.Visual.Length);
            for (int j = 0; j < original.Visual.Length; j++)
            {
                Assert.Equal(original.Visual[j], roundTrip.Visual[j]);
            }
        }

        // Additional assertions can be added here based on known values in the TItem.tcd file
    }

    // Read Resources/TestFiles-BOM.zip/TItem.tcd
    private MemoryStream GetTestFileStream()
    {
        var assembly = typeof(TitemReader).Assembly;
        var resourceName = "FourSer.Tests.Behavioural.Resources.TestFiles-BOM.zip";

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found in assembly.");
        }

        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var entry = archive.GetEntry("TItem.tcd");
        if (entry == null)
        {
            throw new FileNotFoundException("TItem.tcd not found in the zip archive.");
        }

        var memoryStream = new MemoryStream();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(memoryStream);
        }

        memoryStream.Position = 0; // Reset stream position to the beginning
        return memoryStream;
    }
}

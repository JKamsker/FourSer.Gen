using System.Collections.Concurrent;
using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class EnumerationTypesPacket
{
    // List<T> - already supported
    [SerializeCollection]
    public List<int> Numbers { get; set; } = new();

    // ICollection<T>
    [SerializeCollection]
    public ICollection<string> Names { get; set; } = new List<string>();

    // IEnumerable<T>
    [SerializeCollection]
    public IEnumerable<byte> Data { get; set; } = new List<byte>();

    // ConcurrentBag<T>
    [SerializeCollection]
    public ConcurrentBag<float> Values { get; set; } = new();

    // HashSet<T>
    [SerializeCollection]
    public HashSet<long> UniqueIds { get; set; } = new();

    // Queue<T>
    [SerializeCollection]
    public Queue<ushort> ProcessingQueue { get; set; } = new();

    // Stack<T>
    [SerializeCollection]
    public Stack<uint> ProcessingStack { get; set; } = new();

    // Array (T[])
    [SerializeCollection]
    public int[] ArrayData { get; set; } = new int[1];

    // IList<T>
    [SerializeCollection]
    public IList<double> Measurements { get; set; } = new List<double>();

    // Collection<T>
    [SerializeCollection]
    public System.Collections.ObjectModel.Collection<bool> Flags { get; set; } = new();

    // ObservableCollection<T>
    [SerializeCollection]
    public System.Collections.ObjectModel.ObservableCollection<short> ObservableData { get; set; } = new();

    // LinkedList<T>
    [SerializeCollection]
    public LinkedList<ushort> Characters { get; set; } = new();

    // SortedSet<T>
    [SerializeCollection]
    public SortedSet<long> SortedValues { get; set; } = new();

    // Custom count types with different enumeration types
    [SerializeCollection(CountType = typeof(byte))]
    public HashSet<string> SmallSet { get; set; } = new();

    [SerializeCollection(CountType = typeof(ushort))]
    public Queue<int> MediumQueue { get; set; } = new();

    [SerializeCollection(CountSize = 8)]
    public ConcurrentBag<long> LargeBag { get; set; } = new();
}
# Polymorphic Collections

This guide describes the current contract for polymorphic collections.

`docs/design-choices.md` is the authoritative source for the underlying wire rules.

## Modes

### `PolymorphicMode.IndividualTypeIds`

- Each element is prefixed with its own discriminator.
- Use this for heterogeneous collections.
- The generated serializer validates every item against the declared `[PolymorphicOption]` set.

```csharp
[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.IndividualTypeIds,
        TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public List<Item> Items { get; set; } = new();
}
```

### `PolymorphicMode.SingleTypeId`

- One discriminator is written for the whole collection.
- All items must be of the same concrete type.
- When `TypeIdProperty` is configured, serialization derives the discriminator from the collection contents, not from the current property value.
- Deserialization restores the discriminator read from the wire back into the referenced property.

```csharp
[GenerateSerializer]
public partial class Scene
{
    public byte EntityType { get; set; } // Mirror of the wire discriminator on deserialize

    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.SingleTypeId,
        TypeIdProperty = nameof(EntityType),
        TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(Player))]
    [PolymorphicOption((byte)2, typeof(Monster), isDefault: true)]
    public IReadOnlyCollection<Entity> Entities { get; set; } = new List<Entity>();
}
```

In that example:

- A stale `EntityType` value is ignored during serialization.
- The discriminator written to the wire comes from the actual runtime type stored in `Entities`.
- On deserialize, the wire discriminator is assigned back to `EntityType`.
- The default option is used when the collection is empty or null.

## Null and nested-reference behavior

- Strings and dynamic-count collections keep canonical null semantics: `null` writes as empty/zero-count and reads back as empty.
- Nested generated reference members remain required on the wire.
- If you need nullable nested-object encoding, use a custom serializer that owns that wire format explicitly.

## IEnumerable and stream behavior

- Pure `IEnumerable<T>` inputs are replayable-only across separate `GetPacketSize(obj)` and `Serialize(obj, ...)` calls.
- Each generated method enumerates a pure enumerable at most once internally.
- For counted or `SingleTypeId` enumerable collections, the generator may materialize items once inside a method so stream serialization still works on non-seekable streams.

## Custom serializers inside polymorphic collections

- Custom serializers must implement the full `ISerializer<T>` contract.
- Stream generation calls `Serialize(T, Stream)` and `Deserialize(Stream)` directly.
- The generator does not bridge stream paths through the span members of a custom serializer.

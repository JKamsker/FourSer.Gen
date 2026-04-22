using System;
using System.Collections.Generic;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases
{
    public class SingleTypeIdTests
    {
        [Fact]
        public void RunTest()
        {
            var original = new SingleTypeIdTest
            {
                AnimalType = 1, // Cats
                Animals = new List<AnimalBase>
                {
                    new CatBase { Age = 10, Name = "Felix" },
                    new CatBase { Age = 2, Name = "Garfield" }
                }
            };

            var size = SingleTypeIdTest.GetPacketSize(original);
            var buffer = new byte[size];
            var span = new Span<byte>(buffer);
            SingleTypeIdTest.Serialize(original, span);

            var deserialized = SingleTypeIdTest.Deserialize(buffer);
            Assert.Equal(original.Animals.Count, deserialized.Animals.Count);

            for (int i = 0; i < original.Animals.Count; i++)
            {
                var originalAnimal = Assert.IsType<CatBase>(original.Animals[i]);
                var deserializedAnimal = Assert.IsType<CatBase>(deserialized.Animals[i]);

                Assert.Equal(originalAnimal.GetType(), deserializedAnimal.GetType());
                Assert.Equal(originalAnimal.Age, deserializedAnimal.Age);
                Assert.Equal(originalAnimal.Name, deserializedAnimal.Name);
            }
        }

        [Fact]
        public void SerializeAfterChangingCollectionType_ShouldIgnoreStaleTypeIdProperty()
        {
            var original = new SingleTypeIdTest
            {
                AnimalType = 1,
                Animals =
                [
                    new CatBase { Age = 10, Name = "Felix" },
                    new CatBase { Age = 2, Name = "Garfield" }
                ]
            };

            var initialBuffer = new byte[SingleTypeIdTest.GetPacketSize(original)];
            SingleTypeIdTest.Serialize(original, initialBuffer);

            var deserialized = SingleTypeIdTest.Deserialize(initialBuffer);
            Assert.Equal(1, deserialized.AnimalType);

            deserialized.Animals =
            [
                new DogBase { Age = 4, Weight = 18 },
                new DogBase { Age = 7, Weight = 22 }
            ];

            var mutatedBuffer = new byte[SingleTypeIdTest.GetPacketSize(deserialized)];
            SingleTypeIdTest.Serialize(deserialized, mutatedBuffer);

            var roundTripped = SingleTypeIdTest.Deserialize(mutatedBuffer);
            Assert.Equal(2, roundTripped.AnimalType);
            Assert.Equal(deserialized.Animals.Count, roundTripped.Animals.Count);

            for (int i = 0; i < deserialized.Animals.Count; i++)
            {
                var originalAnimal = Assert.IsType<DogBase>(deserialized.Animals[i]);
                var deserializedAnimal = Assert.IsType<DogBase>(roundTripped.Animals[i]);

                Assert.Equal(originalAnimal.Age, deserializedAnimal.Age);
                Assert.Equal(originalAnimal.Weight, deserializedAnimal.Weight);
            }
        }

        [Fact]
        public void EmptyCollection_ShouldWriteFirstConfiguredTypeIdEvenWhenPropertyIsSet()
        {
            var original = new SingleTypeIdTest
            {
                AnimalType = 2,
                Animals = new List<AnimalBase>()
            };

            var buffer = new byte[SingleTypeIdTest.GetPacketSize(original)];
            SingleTypeIdTest.Serialize(original, buffer);

            var deserialized = SingleTypeIdTest.Deserialize(buffer);
            Assert.Equal(1L, deserialized.AnimalType);
            Assert.Empty(deserialized.Animals);
        }
    }
}

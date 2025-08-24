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
    }
}

using System;
using System.Collections.Generic;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases
{
    public class IndividualTypeIdsTests
    {
        [Fact]
        public void RunTest()
        {
            var original = new IndividualTypeIdsTest
            {
                Animals = new List<Animal>
                {
                    new Cat { Age = 10, Name = "Felix" },
                    new Dog { Age = 5, Weight = 15 },
                    new Cat { Age = 2, Name = "Garfield" }
                }
            };

            var size = IndividualTypeIdsTest.GetPacketSize(original);
            var buffer = new byte[size];
            var span = new Span<byte>(buffer);
            IndividualTypeIdsTest.Serialize(original, span);

            var deserialized = IndividualTypeIdsTest.Deserialize(buffer);
            Assert.Equal(original.Animals.Count, deserialized.Animals.Count);

            for (int i = 0; i < original.Animals.Count; i++)
            {
                var originalAnimal = original.Animals[i];
                var deserializedAnimal = deserialized.Animals[i];

                Assert.Equal(originalAnimal.GetType(), deserializedAnimal.GetType());
                Assert.Equal(originalAnimal.Age, deserializedAnimal.Age);

                if (originalAnimal is Cat originalCat)
                {
                    var deserializedCat = Assert.IsType<Cat>(deserializedAnimal);
                    Assert.Equal(originalCat.Name, deserializedCat.Name);
                }
                else if (originalAnimal is Dog originalDog)
                {
                    var deserializedDog = Assert.IsType<Dog>(deserializedAnimal);
                    Assert.Equal(originalDog.Weight, deserializedDog.Weight);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace FourSer.Consumer.UseCases
{
    public static class SingleTypeIdTestRunner
    {
        public static void RunTest()
        {
            Console.WriteLine("Running SingleTypeIdTest...");

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

            var deserialized = SingleTypeIdTest.Deserialize(buffer, out var bytesRead);

            Assert.AreEqual(size, bytesRead, "Bytes read should match original size.");
            Assert.AreEqual(original.Animals.Count, deserialized.Animals.Count, "Animal counts should match.");

            for (int i = 0; i < original.Animals.Count; i++)
            {
                var originalAnimal = (CatBase)original.Animals[i];
                var deserializedAnimal = (CatBase)deserialized.Animals[i];

                Assert.AreEqual(originalAnimal.GetType(), deserializedAnimal.GetType(), $"Animal {i} types should match.");
                Assert.AreEqual(originalAnimal.Age, deserializedAnimal.Age, $"Animal {i} ages should match.");
                Assert.AreEqual(originalAnimal.Name, deserializedAnimal.Name, $"Cat {i} names should match.");
            }

            Console.WriteLine("SingleTypeIdTest passed.");
        }
    }
}

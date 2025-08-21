namespace FourSer.Consumer.UseCases
{
    public static class IndividualTypeIdsTestRunner
    {
        public static void RunTest()
        {
            Console.WriteLine("Running IndividualTypeIdsTest...");

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
            Assert.AreEqual(original.Animals.Count, deserialized.Animals.Count, "Animal counts should match.");

            for (int i = 0; i < original.Animals.Count; i++)
            {
                var originalAnimal = original.Animals[i];
                var deserializedAnimal = deserialized.Animals[i];

                Assert.AreEqual(originalAnimal.GetType(), deserializedAnimal.GetType(), $"Animal {i} types should match.");
                Assert.AreEqual(originalAnimal.Age, deserializedAnimal.Age, $"Animal {i} ages should match.");

                if (originalAnimal is Cat originalCat)
                {
                    var deserializedCat = (Cat)deserializedAnimal;
                    Assert.AreEqual(originalCat.Name, deserializedCat.Name, $"Cat {i} names should match.");
                }
                else if (originalAnimal is Dog originalDog)
                {
                    var deserializedDog = (Dog)deserializedAnimal;
                    Assert.AreEqual(originalDog.Weight, deserializedDog.Weight, $"Dog {i} weights should match.");
                }
            }

            Console.WriteLine("IndividualTypeIdsTest passed.");
        }
    }
}

// using System.Diagnostics.CodeAnalysis;
// using FourSer.Contracts;
//
// namespace FourSer.Consumer.UseCases.PolymorphicInferedType;
//
// [GenerateSerializer]
// public partial class PolymorphicInferedType
// {
//     /*
//      Implicit serialization order: Count, TypeId, Animals
//      */
//     
//     
//     [SuppressMessage("Usage", "FS0014:Missing TypeIdProperty")] // should be allowed in future
//     [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
//     [PolymorphicOption(AnimalType.CatType, typeof(Cat))]
//     [PolymorphicOption(AnimalType.DogType, typeof(Dog))]
//     public IEnumerable<Animal> Animals { get; set; }
// }
//
// public enum AnimalType
// {
//     CatType = 1,
//     DogType = 2
// }
//
// [GenerateSerializer]
// public partial class Cat : Animal
// {
//     public string Name { get; set; } = string.Empty;
// }
//
// [GenerateSerializer]
// public partial class Dog : Animal
// {
//     public string Breed { get; set; } = string.Empty;
// }
//
// [GenerateSerializer]
// public partial class Animal
// {
//     public int Id { get; set; }
// }
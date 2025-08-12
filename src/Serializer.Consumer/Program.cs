using Serializer.Consumer;
using System;
using System.Linq;
using System.Reflection;
using Serializer.Consumer.UseCases;

public class TestRunner
{
    public static void Main(string[] args)
    {
        var testRunner = new TestRunner();
        testRunner.RunTests();
    }

    public void RunTests()
    {
        // Run polymorphic tests first
        PolymorphicTest.RunTest();
        PolymorphicImplicitTypeIdTest.RunTest();
        PolymorphicComparison.RunComparison();
        PolymorphicApproachComparison.RunComparison();
        
        var testClasses = new[]
        {
            typeof(MyPacketTest),
            typeof(TestPacket1Test),
            typeof(TestWithCountTypeTest),
            typeof(TestWithCountSizeReferenceTest),
            typeof(MixedFieldsAndPropsTest),
            typeof(TestWithListOfReferenceTypesTest),
            typeof(TestWithListOfNestedReferenceTypesTest),
            typeof(NestedObjectTest),
            typeof(BackwardCompatibilityTest)
        };

        foreach (var testClass in testClasses)
        {
            var testMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.EndsWith("Test"));

            foreach (var testMethod in testMethods)
            {
                try
                {
                    Console.WriteLine($"Running test: {testClass.Name}.{testMethod.Name}");
                    var testInstance = Activator.CreateInstance(testClass);
                    testMethod.Invoke(testInstance, null);
                    Console.WriteLine($"Test passed: {testClass.Name}.{testMethod.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Test failed: {testClass.Name}.{testMethod.Name}");
                    Console.WriteLine(ex.InnerException?.Message);
                }
                Console.WriteLine();
            }
        }
    }
}

public static class Assert
{
    public static void AreEqual(object expected, object actual, string message = "")
    {
        if (!Equals(expected, actual))
        {
            throw new Exception($"Assert.AreEqual failed. Expected: {expected}, Actual: {actual}. {message}");
        }
    }
}

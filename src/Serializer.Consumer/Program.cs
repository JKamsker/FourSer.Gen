using Serializer.Consumer;
using System;
using System.Linq;
using System.Reflection;

public class TestRunner
{
    public static void Main(string[] args)
    {
        var testRunner = new TestRunner();
        testRunner.RunTests();
    }

    public void RunTests()
    {
        var testClass = typeof(TestCases);
        var testMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Test"));

        foreach (var testMethod in testMethods)
        {
            try
            {
                Console.WriteLine($"Running test: {testMethod.Name}");
                var testInstance = Activator.CreateInstance(testClass);
                testMethod.Invoke(testInstance, null);
                Console.WriteLine($"Test passed: {testMethod.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {testMethod.Name}");
                Console.WriteLine(ex.InnerException?.Message);
            }
            Console.WriteLine();
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

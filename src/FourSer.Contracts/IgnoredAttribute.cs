namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class IgnoredAttribute : Attribute
{
}

using System;

namespace FourSer.Tests.Behavioural.Demo
{
    /// <summary>
    /// Associates a generated serializer root type with a specific .tcd resource entry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TcdResourceAttribute : Attribute
    {
        public TcdResourceAttribute(string entryName)
        {
            EntryName = entryName ?? throw new ArgumentNullException(nameof(entryName));
        }

        /// <summary>
        /// The name of the .tcd file to map to this serializer root type.
        /// The comparison is case-insensitive and only considers the file name portion.
        /// </summary>
        public string EntryName { get; }
    }
}


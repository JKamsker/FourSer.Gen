using System.IO;
using Xunit;

namespace FourSer.Tests.Behavioural.Demo
{
    public class TcdRoundTripTests
    {
        [Theory]
        [MemberData(nameof(TcdResourceTheoryData.AllCoverage), MemberType = typeof(TcdResourceTheoryData))]
        public void File_Has_Serializer(string entryName, bool hasSerializer)
        {
            Assert.False(string.IsNullOrWhiteSpace(entryName));
            Assert.True(hasSerializer, $"Resource '{entryName}' is not mapped to a serializer.");
        }

        [Theory]
        [MemberData(nameof(TcdResourceTheoryData.BoundCases), MemberType = typeof(TcdResourceTheoryData))]
        public void RoundTripBinaryResources(TcdResourceCase testCase)
        {
            Assert.NotNull(testCase);

            var binding = Assert.IsType<TcdSerializerBinding>(testCase.Binding);

            Assert.True(testCase.Payload.Length > 0, $"Resource '{testCase.EntryName}' is empty.");

            using var originalStream = testCase.OpenStream();
            var deserialized = binding.Deserialize(originalStream);
            Assert.NotNull(deserialized);

            using var roundTripStream = new MemoryStream();
            binding.Serialize(deserialized, roundTripStream);
            var roundTripBytes = roundTripStream.ToArray();
            Assert.NotEmpty(roundTripBytes);

            using var secondStream = new MemoryStream(roundTripBytes, writable: false);
            var secondDeserialized = binding.Deserialize(secondStream);
            Assert.NotNull(secondDeserialized);

            using var verificationStream = new MemoryStream();
            binding.Serialize(secondDeserialized, verificationStream);
            var verificationBytes = verificationStream.ToArray();

            Assert.Equal(roundTripBytes.Length, verificationBytes.Length);
            Assert.Equal(roundTripBytes, verificationBytes);
        }
    }
}

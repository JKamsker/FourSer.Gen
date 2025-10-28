using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;

namespace FourSer.Tests.Behavioural.Demo
{
    public sealed class TcdResourceCase
    {
        private readonly byte[] _payload;

        public TcdResourceCase(string entryName, byte[] payload, TcdSerializerBinding? binding)
        {
            EntryName = entryName ?? throw new ArgumentNullException(nameof(entryName));
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Binding = binding;
        }

        public string EntryName { get; }

        public ReadOnlyMemory<byte> Payload => _payload;

        public TcdSerializerBinding? Binding { get; }

        public MemoryStream OpenStream() => new(_payload, writable: false);
    }

    public sealed class TcdSerializerBinding
    {
        private readonly Type _modelType;
        private readonly MethodInfo _deserialize;
        private readonly MethodInfo _serialize;

        public TcdSerializerBinding(Type modelType, MethodInfo deserialize, MethodInfo serialize)
        {
            _modelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
            _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
            _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        }

        public Type ModelType => _modelType;

        public object Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var result = _deserialize.Invoke(null, new object[] { stream });
            if (result == null)
            {
                throw new InvalidOperationException($"Deserializer for {_modelType.FullName} returned null.");
            }

            return result;
        }

        public void Serialize(object value, Stream stream)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!_modelType.IsInstanceOfType(value))
            {
                throw new ArgumentException($"Value must be assignable to {_modelType.FullName}.", nameof(value));
            }

            _serialize.Invoke(null, new[] { value, stream });
        }
    }

    public static class TcdResourceTheoryData
    {
        private const string DownloadUri = "https://github.com/JKamsker/FourSer.Gen/raw/refs/heads/testfiles/4Story/4StoryCC/Tcd.zip";
        private const string ArchiveFileName = "Tcd.zip";

        private static readonly Lazy<string> CachedArchivePath = new(EnsureArchive, isThreadSafe: true);
        private static readonly Lazy<IReadOnlyList<TcdResourceCase>> CachedCases = new(LoadCases, isThreadSafe: true);
        private static readonly Lazy<IReadOnlyList<(string EntryName, bool HasSerializer)>> CachedCoverage
            = new(LoadCoverage, isThreadSafe: true);

        public static IEnumerable<object[]> AllCases()
        {
            foreach (var testCase in CachedCases.Value)
            {
                yield return new object[] { testCase };
            }
        }

        public static IEnumerable<object[]> AllCoverage()
        {
            foreach (var coverage in CachedCoverage.Value)
            {
                yield return new object[] { coverage.EntryName, coverage.HasSerializer };
            }
        }

        public static IEnumerable<object[]> BoundCases()
        {
            foreach (var testCase in CachedCases.Value.Where(static c => c.Binding is not null))
            {
                yield return new object[] { testCase };
            }
        }

        private static IReadOnlyList<TcdResourceCase> LoadCases()
        {
            return LoadPayloads().Select(payload =>
            {
                TcdSerializerRegistry.Instance.TryGetBinding(payload.NormalizedName, out var binding);
                return new TcdResourceCase(payload.DisplayName, payload.Data, binding);
            }).ToArray();
        }

        private static IReadOnlyList<(string EntryName, bool HasSerializer)> LoadCoverage()
        {
            return LoadPayloads().Select(payload =>
            {
                var hasBinding = TcdSerializerRegistry.Instance.TryGetBinding(payload.NormalizedName, out _);
                return (payload.DisplayName, hasBinding);
            }).ToArray();
        }

        private static IReadOnlyList<PayloadEntry> LoadPayloads()
        {
            var archivePath = CachedArchivePath.Value;
            using var stream = File.OpenRead(archivePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entries = new List<PayloadEntry>();

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".tcd", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);

                var displayName = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = entry.FullName;
                }

                var normalized = NormalizeEntryName(displayName);
                entries.Add(new PayloadEntry(displayName, normalized, buffer.ToArray()));
            }

            if (entries.Count == 0)
            {
                throw new InvalidOperationException($"No .tcd entries discovered in '{archivePath}'.");
            }

            return entries;
        }

        private static string EnsureArchive()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "FourSer.Gen", "TestFiles");
            Directory.CreateDirectory(tempRoot);

            var archivePath = Path.Combine(tempRoot, ArchiveFileName);
            if (File.Exists(archivePath))
            {
                return archivePath;
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FourSerTests", "1.0"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/JKamsker/FourSer.Gen)"));
            using var response = httpClient.GetAsync(DownloadUri).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            using var responseStream = response.Content.ReadAsStream();
            using (var fileStream = File.Create(archivePath))
            {
                responseStream.CopyTo(fileStream);
            }

            return archivePath;
        }

        internal static string NormalizeEntryName(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return string.Empty;
            }

            var trimmed = entryName.Trim();
            var normalizedSeparators = trimmed.Replace('\\', '/');
            var fileName = Path.GetFileName(normalizedSeparators);
            return fileName;
        }

        private sealed record PayloadEntry(string DisplayName, string NormalizedName, byte[] Data);

        private sealed class TcdSerializerRegistry
        {
            internal static TcdSerializerRegistry Instance { get; } = new();

            private readonly Dictionary<string, TcdSerializerBinding> _bindings;

            private TcdSerializerRegistry()
            {
                _bindings = BuildBindings();
            }

            public bool TryGetBinding(string normalizedEntryName, out TcdSerializerBinding? binding)
                => _bindings.TryGetValue(normalizedEntryName, out binding);

            private static Dictionary<string, TcdSerializerBinding> BuildBindings()
            {
                var assembly = typeof(TcdResourceTheoryData).Assembly;
                var bindings = new Dictionary<string, TcdSerializerBinding>(StringComparer.OrdinalIgnoreCase);

                foreach (var type in assembly.GetTypes())
                {
                    var attributes = type.GetCustomAttributes<TcdResourceAttribute>(inherit: false).ToArray();
                    if (attributes.Length == 0)
                    {
                        continue;
                    }

                    var deserialize = type.GetMethod(
                        "Deserialize",
                        BindingFlags.Public | BindingFlags.Static,
                        binder: null,
                        types: new[] { typeof(Stream) },
                        modifiers: null);

                    var serialize = type.GetMethod(
                        "Serialize",
                        BindingFlags.Public | BindingFlags.Static,
                        binder: null,
                        types: new[] { type, typeof(Stream) },
                        modifiers: null);

                    if (deserialize == null || serialize == null)
                    {
                        continue;
                    }

                    var binding = new TcdSerializerBinding(type, deserialize, serialize);

                    foreach (var attribute in attributes)
                    {
                        var key = NormalizeEntryName(attribute.EntryName);
                        if (string.IsNullOrEmpty(key))
                        {
                            continue;
                        }

                        bindings[key] = binding;
                    }
                }

                return bindings;
            }
        }
    }
}

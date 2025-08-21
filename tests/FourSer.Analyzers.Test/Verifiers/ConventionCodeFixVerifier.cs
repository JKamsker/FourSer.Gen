#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using Xunit;

namespace TestHelper
{
    public abstract class ConventionCodeFixVerifier : CodeFixVerifier
    {
        protected ConventionCodeFixVerifier()
        {
            var t = GetType();
            DataSourcePath = Path.Combine("../../../DataSource", t.Name);
        }

        private string DataSourcePath { get; }

        protected async Task VerifyCSharpByConvention([CallerMemberName]string? testName = null)
        {
            if (testName == null) throw new System.ArgumentNullException(nameof(testName));
            await VerifyCSharpByConventionV2(testName);
        }

        private async Task VerifyCSharpByConventionV2(string testName)
        {
            var sources = ReadSources(testName);
            var expectedResults = ReadDiagnosticResultsFromFolder(testName);
            var expectedSources = ReadExpectedSources(testName);

            await VerifyCSharp(sources, expectedResults.ToArray(), expectedSources.ToArray());
        }

        private IEnumerable<DiagnosticResult> ReadDiagnosticResultsFromFolder(string testName)
        {
            var diagnosticPath = Path.Combine(DataSourcePath, testName, "Diagnostic");

            if (!Directory.Exists(diagnosticPath))
                return System.Array.Empty<DiagnosticResult>();

            var results = ReadResultsFromFolder(diagnosticPath);

            return GetDiagnosticResult(results);
        }

        private IEnumerable<Result> ReadResultsFromFolder(string diagnosticPath)
        {
            foreach (var file in Directory.GetFiles(diagnosticPath, "*.json"))
            {
                if (file.EndsWith("action.json", System.StringComparison.InvariantCultureIgnoreCase))
                    continue;

                foreach (var r in ReadResults(file))
                {
                    yield return r;
                }
            }
        }


        private Dictionary<string, string> ReadSources(string testName)
        {
            var sourcePath = Path.Combine(DataSourcePath, testName, "Source");
            return ReadFiles(sourcePath);
        }

        private IEnumerable<FixResult> ReadExpectedSources(string testName)
        {
            var testPath = Path.Combine(DataSourcePath, testName);

            var exprectedFolders = Directory.GetDirectories(testPath, "Expected*");

            foreach (var expectedPath in exprectedFolders)
            {
                var m = System.Text.RegularExpressions.Regex.Match(expectedPath, @"\d+$");
                var index = m.Success ? int.Parse(m.Value) : 0;

                yield return new FixResult(index, ReadFiles(expectedPath));
            }
        }

        private static Dictionary<string, string> ReadFiles(string sourcePath)
        {
            if (!Directory.Exists(sourcePath))
                return new Dictionary<string, string>();

            var sources = new Dictionary<string, string>();

            foreach (var file in Directory.GetFiles(sourcePath, "*.csx"))
            {
                var code = File.ReadAllText(file);
                var name = Path.GetFileName(file);
                sources.Add(name, code);
            }

            return sources;
        }

        private class FixResult
        {
            public int Index { get; }
            public Dictionary<string, string> ExpectedSources { get; }

            public FixResult(int index, Dictionary<string, string> expectedSources)
            {
                Index = index;
                ExpectedSources = expectedSources;
            }
        }

        private async Task VerifyCSharp(Dictionary<string, string> sources, DiagnosticResult[] expectedResults, params FixResult[] fixResults)
        {
            var analyzer = GetCSharpDiagnosticAnalyzer();
            var fix = GetCSharpCodeFixProvider();
            if (analyzer == null || fix == null) return;

            var originalProject = CreateProject(sources);
            if (originalProject == null) return;

            var diagnostics = await GetDiagnostics(originalProject, analyzer);
            VerifyDiagnosticResults(diagnostics, analyzer, expectedResults);

            foreach (var fixResult in fixResults)
            {
                var project = await ApplyFix(originalProject, analyzer, fix, fixResult.Index);
                if (project == null) continue;

                var expectedSources = fixResult.ExpectedSources;

                if (expectedSources == null || !expectedSources.Any())
                    return;

                var actualSources = new Dictionary<string, string>();

                foreach (var doc in project.Documents)
                {
                    var code = await GetStringFromDocument(doc);
                    actualSources.Add(doc.Name, code);
                }

                Assert.True(actualSources.Keys.SequenceEqual(expectedSources.Keys));

                foreach (var item in actualSources)
                {
                    var actual = item.Value;
                    var newSource = expectedSources[item.Key];
                    Assert.Equal(newSource, actual);
                }
            }
        }

        private static async Task<Project?> ApplyFix(Project project, DiagnosticAnalyzer analyzer, CodeFixProvider fix, int fixIndex)
        {
            var diagnostics = await GetDiagnostics(project, analyzer);
            var fixableDiagnostics = diagnostics.Where(d => fix.FixableDiagnosticIds.Contains(d.Id)).ToArray();

            var attempts = fixableDiagnostics.Length;

            for (int i = 0; i < attempts; i++)
            {
                var diag = fixableDiagnostics.First();
                var doc = project.Documents.FirstOrDefault(d => d.Name == diag.Location.SourceTree?.FilePath);

                if (doc == null)
                {
                    fixableDiagnostics = fixableDiagnostics.Skip(1).ToArray();
                    continue;
                }

                var actions = new List<CodeAction>();
                var fixContex = new CodeFixContext(doc, diag, (a, d) => actions.Add(a), CancellationToken.None);
                await fix.RegisterCodeFixesAsync(fixContex);

                if (!actions.Any())
                {
                    break;
                }

                var codeAction = actions[fixIndex];

                var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
                var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
                project = solution.GetProject(project.Id)!;

                fixableDiagnostics = (await GetDiagnostics(project, analyzer))
                    .Where(d => fix.FixableDiagnosticIds.Contains(d.Id)).ToArray();

                if (!fixableDiagnostics.Any()) break;
            }

            return project;
        }

        private static async Task<Diagnostic[]> GetDiagnostics(Project project, DiagnosticAnalyzer analyzer)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) return System.Array.Empty<Diagnostic>();

            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
            return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToArray();
        }

        protected virtual IEnumerable<MetadataReference> References
        {
            get
            {
                yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                yield return MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
                yield return MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
                yield return MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);
            }
        }

        protected virtual CSharpCompilationOptions CompilationOptions => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        protected Project? CreateProject(Dictionary<string, string> sources)
        {
            string fileNamePrefix = DefaultFilePathPrefix;
            string fileExt = CSharpDefaultFileExt;

            var projectId = ProjectId.CreateNewId(debugName: TestProjectName);

            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, LanguageNames.CSharp);

            foreach (var reference in References)
            {
                solution = solution.AddMetadataReference(projectId, reference);
            }

            foreach (var source in sources)
            {
                var newFileName = source.Key;
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, SourceText.From(source.Value));
            }

            return solution.GetProject(projectId)?.WithCompilationOptions(CompilationOptions);
        }

        #region read expected results from JSON file

        private IEnumerable<DiagnosticResult> GetDiagnosticResult(IEnumerable<Result> results)
        {
            var analyzer = GetCSharpDiagnosticAnalyzer();
            if (analyzer == null) yield break;

            var supportedDiagnostics = analyzer.SupportedDiagnostics;
            var analyzers = supportedDiagnostics.ToDictionary(x => x.Id);

            foreach (var r in results)
            {
                if (!analyzers.TryGetValue(r.Id, out var diag)) continue;

                yield return new DiagnosticResult
                {
                    Id = r.Id,
                    Message = r.MessageArgs == null ? diag.MessageFormat.ToString() : string.Format(diag.MessageFormat.ToString(), (object[])r.MessageArgs),
                    Severity = r.Sevirity,
                    Locations = new[] { new DiagnosticResultLocation(r.Path ?? "Source.cs", r.Line, r.Column) },
                };
            }
        }

        private IEnumerable<Result> ReadResults(string path)
        {
            if (!File.Exists(path)) return System.Array.Empty<Result>();

            try
            {
                var result = JsonConvert.DeserializeObject<Result>(File.ReadAllText(path));
                if (result != null) return new[] { result };
            }
            catch { }

            try
            {
                var results = JsonConvert.DeserializeObject<Result[]>(File.ReadAllText(path));
                if (results != null) return results;
            }
            catch { }

            return System.Array.Empty<Result>();
        }

        private class Result
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; } = "";

            [JsonProperty(PropertyName = "sevirity")]
            public DiagnosticSeverity Sevirity { get; set; }

            [JsonProperty(PropertyName = "line")]
            public int Line { get; set; }

            [JsonProperty(PropertyName = "column")]
            public int Column { get; set; }

            [JsonProperty(PropertyName = "path")]
            public string? Path { get; set; }

            [JsonProperty(PropertyName = "message-args")]
            public string[]? MessageArgs { get; set; }
        }

        #endregion
    }
}
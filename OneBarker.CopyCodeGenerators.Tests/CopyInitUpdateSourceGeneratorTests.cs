using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Abstractions;

namespace OneBarker.CopyCodeGenerators.Tests;

public partial class CopyInitUpdateSourceGeneratorTests
{
    
    #region Test Data
    private static readonly CopyTestData[] AllTestData;
    
    public static IEnumerable<CopyTestData> GetTestData()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        dir = Path.Join(dir, "TestData");
        if (!Directory.Exists(dir)) yield break;

        foreach (var subdir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
        {
            var name       = Path.GetFileName(subdir);
            if (string.IsNullOrEmpty(name)) continue;
            var sourceFile = Path.Join(subdir, $"{name}.cs");
            if (File.Exists(sourceFile))
            {
                var data = new CopyTestData()
                           {
                               Name   = name,
                               Source = File.ReadAllText(sourceFile),
                           };
                var resultDir = Path.Join(subdir, "Copy");
                if (Directory.Exists(resultDir))
                {
                    foreach (var resultFile in Directory.GetFiles(resultDir, "*.cs", SearchOption.TopDirectoryOnly))
                    {
                        var resultName = Path.GetFileName(resultFile);
                        data.AddCopy(resultName, File.ReadAllText(resultFile));
                    }
                }
                resultDir = Path.Join(subdir, "Init");
                if (Directory.Exists(resultDir))
                {
                    foreach (var resultFile in Directory.GetFiles(resultDir, "*.cs", SearchOption.TopDirectoryOnly))
                    {
                        var resultName = Path.GetFileName(resultFile);
                        data.AddInit(resultName, File.ReadAllText(resultFile));
                    }
                }
                resultDir = Path.Join(subdir, "Update");
                if (Directory.Exists(resultDir))
                {
                    foreach (var resultFile in Directory.GetFiles(resultDir, "*.cs", SearchOption.TopDirectoryOnly))
                    {
                        var resultName = Path.GetFileName(resultFile);
                        data.AddUpdate(resultName, File.ReadAllText(resultFile));
                    }
                }
                resultDir = Path.Join(subdir, "CopyTo");
                if (Directory.Exists(resultDir))
                {
                    foreach (var resultFile in Directory.GetFiles(resultDir, "*.cs", SearchOption.TopDirectoryOnly))
                    {
                        var resultName = Path.GetFileName(resultFile);
                        data.AddCopyTo(resultName, File.ReadAllText(resultFile));
                    }
                }
                resultDir = Path.Join(subdir, "UpdateTo");
                if (Directory.Exists(resultDir))
                {
                    foreach (var resultFile in Directory.GetFiles(resultDir, "*.cs", SearchOption.TopDirectoryOnly))
                    {
                        var resultName = Path.GetFileName(resultFile);
                        data.AddUpdateTo(resultName, File.ReadAllText(resultFile));
                    }
                }
                
                yield return data;
            }
        }
    }

    public static IEnumerable<object[]> GetCopyData()
    {
        foreach (var item in AllTestData)
        {
            if (item.CopyResults.Any()) yield return [item];
        }
    }
    
    public static IEnumerable<object[]> GetInitData()
    {
        foreach (var item in AllTestData)
        {
            if (item.InitResults.Any()) yield return [item];
        }
    }
    
    public static IEnumerable<object[]> GetUpdateData()
    {
        foreach (var item in AllTestData)
        {
            if (item.UpdateResults.Any()) yield return [item];
        }
    }

    public static IEnumerable<object[]> GetCopyToData()
    {
        foreach (var item in AllTestData)
        {
            if (item.CopyToResults.Any()) yield return [item];
        }
    }

    public static IEnumerable<object[]> GetUpdateToData()
    {
        foreach (var item in AllTestData)
        {
            if (item.UpdateToResults.Any()) yield return [item];
        }
    }
    
    #endregion
    
    static CopyInitUpdateSourceGeneratorTests()
    {
        AllTestData = GetTestData().ToArray();
    }

    private readonly ITestOutputHelper _output;

    public CopyInitUpdateSourceGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static MetadataReference[] GetMetadataReferences() 
        => new []
           {
               typeof(object).Assembly.Location,
               typeof(string).Assembly.Location,
               typeof(Attribute).Assembly.Location,
               typeof(System.Numerics.Vector2).Assembly.Location,
           }
           .Where(x => !string.IsNullOrEmpty(x))
           .Distinct()
           .Select(x => (MetadataReference) MetadataReference.CreateFromFile(x))
           .ToArray();

    
    [GeneratedRegex(@"^\s*/\*")]
    private static partial Regex CommentStart();

    [GeneratedRegex(@"\*/\s*$")]
    private static partial Regex CommentEnd();

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex NewLine();
    
    private void OutputComments(CopyTestData data)
    {
        var start     = CommentStart();
        var end       = CommentEnd();
        var dataLines = NewLine().Split(data.Source);
        var write     = false;
        foreach (var line in dataLines)
        {
            if (write)
            {
                _output.WriteLine(line);
                if (end.IsMatch(line))
                {
                    write = false;
                }
            }
            else if (start.IsMatch(line))
            {
                _output.WriteLine(line);
                write = true;
            }
        }
    }
    
    [Theory]
    [MemberData(nameof(GetCopyData))]
    public void GenerateCopyFromMethod(CopyTestData data)
    {
        OutputComments(data);
        
        // Create an instance of the source generator.
        var generator = new CopyFromSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(
            nameof(CopyInitUpdateSourceGeneratorTests),
            new[] { CSharpSyntaxTree.ParseText(data.Source) },
            GetMetadataReferences()
        );

        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // All generated files can be found in 'RunResults.GeneratedTrees'.
        foreach (var (name, source) in data.CopyResults)
        {
            _output.WriteLine($"Checking '{name}'...");
            var generatedFile = runResult.GeneratedTrees.Single(
                t => t.FilePath.EndsWith(name)
            );
            var actual = generatedFile.GetText().ToString();
            Assert.Equal(source, actual, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }
    
    [Theory]
    [MemberData(nameof(GetCopyToData))]
    public void GenerateCopyToMethod(CopyTestData data)
    {
        OutputComments(data);
        
        // Create an instance of the source generator.
        var generator = new CopyToSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(
            nameof(CopyInitUpdateSourceGeneratorTests),
            new[] { CSharpSyntaxTree.ParseText(data.Source) },
            GetMetadataReferences()
        );

        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // All generated files can be found in 'RunResults.GeneratedTrees'.
        foreach (var (name, source) in data.CopyToResults)
        {
            _output.WriteLine($"Checking '{name}'...");
            var generatedFile = runResult.GeneratedTrees.Single(
                t => t.FilePath.EndsWith(name)
            );
            var actual = generatedFile.GetText().ToString();
            Assert.Equal(source, actual, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }
    
    [Theory]
    [MemberData(nameof(GetInitData))]
    public void GenerateInitFromMethod(CopyTestData data)
    {
        OutputComments(data);
        
        // Create an instance of the source generator.
        var generator = new InitFromSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(
            nameof(CopyInitUpdateSourceGeneratorTests),
            new[] { CSharpSyntaxTree.ParseText(data.Source) },
            
            GetMetadataReferences()
        );

        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // All generated files can be found in 'RunResults.GeneratedTrees'.
        foreach (var (name, source) in data.InitResults)
        {
            _output.WriteLine($"Checking '{name}'...");
            var generatedFile = runResult.GeneratedTrees.Single(
                t => t.FilePath.EndsWith(name)
            );
            var actual = generatedFile.GetText().ToString();
            Assert.Equal(source, actual, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }
    
    [Theory]
    [MemberData(nameof(GetUpdateData))]
    public void GenerateUpdateFromMethod(CopyTestData data)
    {
        OutputComments(data);
        
        // Create an instance of the source generator.
        var generator = new UpdateFromSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(
            nameof(CopyInitUpdateSourceGeneratorTests),
            new[] { CSharpSyntaxTree.ParseText(data.Source) },
            GetMetadataReferences()
        );

        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // All generated files can be found in 'RunResults.GeneratedTrees'.
        foreach (var (name, source) in data.UpdateResults)
        {
            _output.WriteLine($"Checking '{name}'...");
            var generatedFile = runResult.GeneratedTrees.Single(
                t => t.FilePath.EndsWith(name)
            );
            var actual = generatedFile.GetText().ToString();
            Assert.Equal(source, actual, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }

    [Theory]
    [MemberData(nameof(GetUpdateToData))]
    public void GenerateUpdateTargetMethod(CopyTestData data)
    {
        OutputComments(data);
        
        // Create an instance of the source generator.
        var generator = new UpdateTargetSourceGenerator();

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(
            nameof(CopyInitUpdateSourceGeneratorTests),
            new[] { CSharpSyntaxTree.ParseText(data.Source) },
            GetMetadataReferences()
        );

        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // All generated files can be found in 'RunResults.GeneratedTrees'.
        foreach (var (name, source) in data.UpdateToResults)
        {
            _output.WriteLine($"Checking '{name}'...");
            var generatedFile = runResult.GeneratedTrees.Single(
                t => t.FilePath.EndsWith(name)
            );
            var actual = generatedFile.GetText().ToString();
            Assert.Equal(source, actual, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }
    
}

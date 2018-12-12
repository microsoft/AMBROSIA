using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Mono.Options;
using Microsoft.CodeAnalysis.CSharp;

namespace Ambrosia
{
    class Program
    {
        private static AmbrosiaCSRuntimeModes _runtimeMode;
        private static List<string> _assemblyNames;
        private static string _outputAssemblyName;
        private static string _targetFramework;
        private static string _binPath;

        static void Main(string[] args)
        {
            ParseAndValidateOptions(args);

            switch (_runtimeMode)
            {
                case AmbrosiaCSRuntimeModes.CodeGen:
                    RunCodeGen();
                    break;
                default:
                    throw new NotSupportedException($"Runtime mode: {_runtimeMode} not supported.");
            }
        }

        private static void RunCodeGen()
        {
            var directoryName = @"latest";

            var generatedDirectory = "GeneratedSourceFiles";
            if (!Directory.Exists(generatedDirectory))
            {
                Directory.CreateDirectory(generatedDirectory); // let any exceptions bleed through
            }
            var projectDirectory = Path.Combine(generatedDirectory, _outputAssemblyName);
            if (!Directory.Exists(projectDirectory))
            {
                Directory.CreateDirectory(projectDirectory); // let any exceptions bleed through
            }
            var directoryPath = Path.Combine(projectDirectory, directoryName);
            if (Directory.Exists(directoryPath))
            {
                var oldDirectoryPath = Path.Combine(projectDirectory, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
                Directory.Move(directoryPath, oldDirectoryPath);
            }
            Directory.CreateDirectory(directoryPath); // let any exceptions bleed through

            // Add references for assemblies referenced by the input assembly
            var sourceFiles = new List<SourceFile>();
            var generatedProxyNames = new List<string>();
            var generatedProxyNamespaces = new List<string>();

            foreach (var assemblyName in _assemblyNames)
            {
                var assembly = Assembly.LoadFrom(assemblyName);

                foreach (var t in assembly.DefinedTypes)
                {
                    if (t.IsInterface)
                    {
                        var internalTypeRepresentation = Utilities.GetTypeDefinitionInformation(t);

                        var proxyInterfacesSource = new ProxyInterfaceGenerator(internalTypeRepresentation).TransformText();
                        sourceFiles.Add(new SourceFile() { FileName = $"ProxyInterfaces_{t.Name}.cs", SourceCode = proxyInterfacesSource, });

                        var immortalSource = new DispatcherGenerator(internalTypeRepresentation).TransformText();
                        sourceFiles.Add(new SourceFile() { FileName = $"Dispatcher_{t.Name}.cs", SourceCode = immortalSource, });

                        var instanceProxySource = new ProxyGenerator(internalTypeRepresentation, internalTypeRepresentation.Name + "Proxy").TransformText();
                        sourceFiles.Add(new SourceFile() { FileName = $"Proxy_{t.Name}.cs", SourceCode = instanceProxySource, });

                        generatedProxyNames.Add($"{internalTypeRepresentation.Name}Proxy_Implementation");
                        generatedProxyNamespaces.Add(internalTypeRepresentation.Namespace);
                    }
                    else if (t.IsValueType)
                    {
                        // This code creates a generated source file holding the definition of any
                        // struct that was defined in the input assembly.
                        //
                        // The short-term reason for this code is to support cases like
                        // PerformanceTestInterruptible's IJob.cs where the IDL interface definition
                        // relies on custom structs.
                        //
                        // The long-term reason for this code is that structs are supported by many
                        // different languages and serialization formats, so once we have a true
                        // cross-language IDL, it makes sense to have structs be a part of that IDL.

                        // very silly hack!
                        var sb = new StringBuilder();

                        sb.AppendLine($"using System;");
                        sb.AppendLine($"using System.Runtime.Serialization;");

                        sb.AppendLine($"namespace {t.Namespace} {{");

                        foreach (var customAttribute in t.CustomAttributes)
                        {
                            sb.AppendLine($"[{customAttribute.AttributeType.Name}]");
                        }
                        sb.AppendLine($"public struct {t.Name}");
                        sb.AppendLine($"{{");
                        foreach (var field in t.GetFields())
                        {
                            if (field.CustomAttributes.Count() > 0)
                            {
                                foreach (var ca in field.CustomAttributes)
                                {
                                    sb.AppendLine($"    [{ca.AttributeType.Name}]");
                                }
                            }
                            sb.AppendLine($"    public {field.FieldType.Name} {field.Name};");
                        }
                        sb.AppendLine($"}}");
                        sb.AppendLine($"}}");
                        sourceFiles.Add(new SourceFile() { FileName = $"{t.Name}.cs", SourceCode = sb.ToString(), });
                    }
                }
            }

            var immortalSerializerSource = new ImmortalSerializerGenerator(generatedProxyNames, generatedProxyNamespaces).TransformText();
            sourceFiles.Add(new SourceFile { FileName = $"ImmortalSerializer.cs", SourceCode = immortalSerializerSource, });

            var referenceLocations = new Dictionary<string, string>();
            var assemblyFileNames = _assemblyNames.Select(Path.GetFileName).ToList();
            foreach (var fileName in Directory.GetFiles(_binPath, "*.dll", SearchOption.TopDirectoryOnly)
                .Union(Directory.GetFiles(_binPath, "*.exe", SearchOption.TopDirectoryOnly)))
            {
                var assemblyPath = Path.GetFullPath(fileName);
                if (assemblyFileNames.Contains(Path.GetFileName(assemblyPath)))
                {
                    continue;
                }

                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFile(assemblyPath);
                }
                catch (Exception)
                {
                    continue;
                }
                var assemblyName = assembly.GetName().Name;
                var assemblyLocation = assembly.Location;

                var assemblyLocationUri = new Uri(assemblyLocation);
                var assemblyLocationRelativePath = new Uri(Path.GetFullPath(directoryPath)).MakeRelativeUri(assemblyLocationUri).ToString();
                referenceLocations.Add(assemblyName, assemblyLocationRelativePath);
            }

            var conditionToPackageInfo = new Dictionary<string, List<Tuple<string, string, string>>>();

            var projFile = $@"{Assembly.GetExecutingAssembly().GetName().Name}.csproj";
            var doc = XDocument.Load(projFile);

            foreach (var itemGroup in doc.Descendants("ItemGroup"))
            {
                var itemGroupCondition = itemGroup.Attributes().FirstOrDefault(a => a.Name == "Condition");
                var condition = itemGroupCondition == null ? string.Empty : itemGroupCondition.Value;

                foreach (var packageReference in itemGroup.Descendants("PackageReference"))
                {
                    var elements = packageReference.Elements();
                    var attributes = packageReference.Attributes().ToList();
                    var packageIncludeAttribute = attributes.FirstOrDefault(a => a.Name == "Include");
                    var packageUpdateAttribute = attributes.FirstOrDefault(a => a.Name == "Update");
                    if (packageIncludeAttribute == null && packageUpdateAttribute == null) continue;

                    var packageNameAttribute = packageIncludeAttribute ?? packageUpdateAttribute;
                    var packageName = packageNameAttribute.Value;
                    var packageMode = packageNameAttribute.Name.ToString();

                    var versionAttribute = attributes.FirstOrDefault(a => a.Name == "Version");

                    string packageVersion;
                    if (versionAttribute == null)
                    {
                        var packageVersionElement = elements.FirstOrDefault(e => e.Name == "Version");
                        if (packageVersionElement == null) continue;
                        packageVersion = packageVersionElement.Value;
                    }
                    else
                    {
                        packageVersion = versionAttribute.Value;
                    }

                    if (!conditionToPackageInfo.ContainsKey(condition))
                    {
                        conditionToPackageInfo.Add(condition, new List<Tuple<string, string, string>>());
                    }
                    conditionToPackageInfo[condition].Add(new Tuple<string, string, string>(packageMode, packageName, packageVersion));
                }
            }

            var conditionalPackageReferences = new List<string>();
            foreach (var cpi in conditionToPackageInfo)
            {
                var packageReferences = new List<string>();
                foreach (var pi in cpi.Value)
                {
                    packageReferences.Add(
$@"     <PackageReference {pi.Item1}=""{pi.Item2}"" Version=""{pi.Item3}"" />");
                }

                if (cpi.Key == String.Empty || cpi.Key == _targetFramework)
                {
                    conditionalPackageReferences.Add(
$@" <ItemGroup>
{string.Join("\n", packageReferences)}
    </ItemGroup>
");
                }
            }

            var references = new List<string>();
            foreach (var rl in referenceLocations)
            {
                references.Add(
                    $@"     <Reference Include=""{rl.Key}"">
            <HintPath>{rl.Value}</HintPath>
        </Reference>");
            }

            var referencesItemGroup =
                $@" <ItemGroup>
{string.Join("\n", references)}
    </ItemGroup>
";

            var projectFileSource =
$@" <Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>{_targetFramework}</TargetFramework>
    </PropertyGroup>
{referencesItemGroup}{string.Join(string.Empty, conditionalPackageReferences)}</Project>";
            var projectSourceFile =
                new SourceFile() { FileName = $"{_outputAssemblyName}.csproj", SourceCode = projectFileSource };
            sourceFiles.Add(projectSourceFile);

            var trees = sourceFiles
                .Select(s => CSharpSyntaxTree.ParseText(
                    s.SourceCode,
                    path: Path.Combine(directoryPath, s.FileName),
                    encoding: Encoding.GetEncoding(0)
                ));

            string directory = null;
            foreach (var tree in trees)
            {
                if (directory == null)
                {
                    directory = Path.GetDirectoryName(tree.FilePath);
                }
                var sourceFile = tree.FilePath;
                File.WriteAllText(sourceFile, tree.GetRoot().ToFullString());
            }
        }

        private static void ParseAndValidateOptions(string[] args)
        {
            var options = ParseOptions(args, out var shouldShowHelp);
            ValidateOptions(options, shouldShowHelp);
        }

        private static OptionSet ParseOptions(string[] args, out bool shouldShowHelp)
        {
            var showHelp = false;
            var assemblyNames = new List<string>();
            var codeGenOptions = new OptionSet {
                { "a|assembly=", "An input assembly name. [REQUIRED]", assemblyName => assemblyNames.Add(Path.GetFullPath(assemblyName)) },
                { "o|outputAssemblyName=", "An output assembly name. [REQUIRED]", outputAssemblyName => _outputAssemblyName = outputAssemblyName },
                { "f|targetFramework=", "The output assembly target framework. [REQUIRED]", f => _targetFramework = f },
                { "b|binPath=", "The bin path containing the output assembly dependencies.", b => _binPath = b },
                { "h|help", "show this message and exit", h => showHelp = h != null },
            };

            var runtimeModeToOptionSet = new Dictionary<AmbrosiaCSRuntimeModes, OptionSet>
            {
                { AmbrosiaCSRuntimeModes.CodeGen, codeGenOptions},
            };

            _runtimeMode = default(AmbrosiaCSRuntimeModes);
            if (args.Length < 1 || !Enum.TryParse(args[0], true, out _runtimeMode))
            {
                Console.WriteLine("Missing or illegal runtime mode.");
                ShowHelp(runtimeModeToOptionSet);
                Environment.Exit(1);
            }

            var options = runtimeModeToOptionSet[_runtimeMode];
            try
            {
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Invalid arguments: " + e.Message);
                ShowHelp(options, _runtimeMode);
                Environment.Exit(1);
            }

            shouldShowHelp = showHelp;
            _assemblyNames = assemblyNames;

            return codeGenOptions;
        }

        private static void ValidateOptions(OptionSet options, bool shouldShowHelp)
        {
            var errorMessage = string.Empty;
            if (_assemblyNames.Count == 0) errorMessage += "At least one input assembly is required.";
            if (_outputAssemblyName == null) errorMessage += "Output assembly name is required.";
            if (_targetFramework == null) errorMessage += "Target framework is required.";

            var assemblyFilesNotFound = _assemblyNames.Where(an => !File.Exists(an)).ToList();
            if (assemblyFilesNotFound.Count > 0)
                errorMessage += $"Unable to find the following assembly files:\n{string.Join("\n", assemblyFilesNotFound)}";

            if (!Directory.Exists(_binPath))
                errorMessage += $"Unable to find the dependencies bin path: {_binPath}";

            if (errorMessage != string.Empty)
            {
                Console.WriteLine(errorMessage);
                ShowHelp(options, _runtimeMode);
                Environment.Exit(1);
            }

            if (shouldShowHelp) ShowHelp(options, _runtimeMode);
        }

        private static void ShowHelp(OptionSet options, AmbrosiaCSRuntimeModes mode)
        {
            var name = typeof(Program).Assembly.GetName().Name;
#if NETCORE
            Console.WriteLine($"Usage: dotnet {name}.dll {mode} [OPTIONS]\nOptions:");
#else
            Console.WriteLine($"Usage: {name}.exe {mode} [OPTIONS]\nOptions:");
#endif
            options.WriteOptionDescriptions(Console.Out);
        }

        private static void ShowHelp(Dictionary<AmbrosiaCSRuntimeModes, OptionSet> modeToOptions)
        {
            foreach (var modeToOption in modeToOptions)
            {
                ShowHelp(modeToOption.Value, modeToOption.Key);
            }
        }
    }

    internal class SourceFile
    {
        public string FileName;
        public string SourceCode;
    }

    public enum AmbrosiaCSRuntimeModes
    {
        CodeGen
    }
}

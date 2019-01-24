using Microsoft.CodeAnalysis.CSharp;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Ambrosia
{
    class Program
    {
        private static AmbrosiaCSRuntimeModes _runtimeMode;
        private static List<string> _assemblyNames;
        private static List<string> _projectFiles;
        private static string _outputAssemblyName;
        private static List<string> _targetFrameworks = new List<string>();

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

            var conditionToPackageInfo = new Dictionary<string, Dictionary<string, HashSet<PackageReferenceInfo>>>();
            var conditionToProjectReference = new Dictionary<string, HashSet<string>>();

            var execAssembly = Assembly.GetExecutingAssembly();
            var projFile = Path.Combine(Path.GetDirectoryName(execAssembly.Location), $@"{execAssembly.GetName().Name}.csproj");
            _projectFiles.Add(projFile);

            var defaultConditionString = string.Empty;
            foreach (var projectFile in _projectFiles)
            {
                var doc = XDocument.Load(projectFile);

                foreach (var itemGroup in doc.Descendants("ItemGroup"))
                {
                    var itemGroupCondition = itemGroup.Attributes().FirstOrDefault(a => a.Name == "Condition");
                    var condition = itemGroupCondition == null ? defaultConditionString : itemGroupCondition.Value;

                    foreach (var packageReference in itemGroup.Descendants("PackageReference"))
                    {
                        var elements = packageReference.Elements();
                        var attributes = packageReference.Attributes().ToList();
                        var packageIncludeAttribute = attributes.FirstOrDefault(a => a.Name == "Include");
                        var packageUpdateAttribute = attributes.FirstOrDefault(a => a.Name == "Update");
                        if (packageIncludeAttribute == null && packageUpdateAttribute == null) continue;

                        var packageNameAttribute = packageIncludeAttribute ?? packageUpdateAttribute;
                        var packageName = packageNameAttribute.Value;

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
                            conditionToPackageInfo.Add(condition, new Dictionary<string, HashSet<PackageReferenceInfo>>());
                        }

                        var packageReferenceInfo = new PackageReferenceInfo(packageName, packageVersion, packageReference.ToString());
                        if (!conditionToPackageInfo[condition].ContainsKey(packageName))
                        {
                            conditionToPackageInfo[condition].Add(packageName, new HashSet<PackageReferenceInfo>());
                        }

                        conditionToPackageInfo[condition][packageName].Add(packageReferenceInfo);
                    }

                    foreach (var projectReference in itemGroup.Descendants("ProjectReference"))
                    {
                        var attributes = projectReference.Attributes().ToList();
                        var projectIncludeAttribute = attributes.FirstOrDefault(a => a.Name == "Include");
                        var projectPath = projectIncludeAttribute.Value;
                        var formerBasePath = new Uri(new FileInfo(projectFile).Directory.FullName);
                        var currentBasePath = new Uri(new DirectoryInfo(directoryPath).FullName);
                        var projectPathUri = new Uri(formerBasePath, projectPath);
                        var newRelativePath = currentBasePath.MakeRelativeUri(projectPathUri);

                        if (!conditionToProjectReference.ContainsKey(condition))
                        {
                            conditionToProjectReference.Add(condition, new HashSet<string>());
                        }

                        conditionToProjectReference[condition].Add(projectReference.ToString().Replace(projectPath.ToString(), newRelativePath.ToString()));
                    }
                }
            }

            var defaultConditionInfo = conditionToPackageInfo.ContainsKey(defaultConditionString) ? conditionToPackageInfo[defaultConditionString] : null;

            foreach (var cp in conditionToPackageInfo)
            {
                foreach (var nameToInfo in cp.Value)
                {
                    var packageInfos = new HashSet<PackageReferenceInfo>(nameToInfo.Value.Union(
                        defaultConditionInfo == null || !defaultConditionInfo.ContainsKey(nameToInfo.Key)
                            ? new List<PackageReferenceInfo>() : defaultConditionInfo[nameToInfo.Key].ToList()));

                    if (packageInfos.Count > 1)
                    {
                        Console.WriteLine($"WARNING: Detected multiple versions of package {nameToInfo.Key} : {string.Join(",", packageInfos.Select(pi => pi.PackageVersion))}");
                    }
                }                
            }

            var conditionalPackageReferences = new List<string>();
            foreach (var cpi in conditionToPackageInfo)
            {
                conditionalPackageReferences.Add($"<ItemGroup{(cpi.Key != string.Empty ? $" Condition=\"{cpi.Key}\"" : string.Empty)}>{string.Join("\n", cpi.Value.SelectMany(v => v.Value).Select(pri => pri.ReferenceString))}</ItemGroup>");
            }

            var conditionalProjectReferences = new List<string>();
            foreach (var cpi in conditionToProjectReference)
            {
                conditionalProjectReferences.Add($"<ItemGroup{(cpi.Key != string.Empty ? $" Condition=\"{cpi.Key}\"" : string.Empty)}>{string.Join("\n", cpi.Value)}</ItemGroup>");
            }

                var projectFileSource =
$@" <Project Sdk=""Microsoft.NET.Sdk"">
        <PropertyGroup>
            <TargetFrameworks>{string.Join(";", _targetFrameworks)}</TargetFrameworks>
        </PropertyGroup>
        {string.Join(string.Empty, conditionalPackageReferences)}
        {string.Join(string.Empty, conditionalProjectReferences)}
    </Project>";

            var projectFileXml = XDocument.Parse(projectFileSource);

            var projectSourceFile = new SourceFile { FileName = $"{_outputAssemblyName}.csproj", SourceCode = projectFileXml.ToString() };
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
            var projectFiles = new List<string>();
            var codeGenOptions = new OptionSet {
                { "a|assembly=", "An input assembly file location. [REQUIRED]", a => assemblyNames.Add(Path.GetFullPath(a)) },
                { "o|outputAssemblyName=", "An output assembly name. [REQUIRED]", outputAssemblyName => _outputAssemblyName = outputAssemblyName },
                { "f|targetFramework=", "The output assembly target framework. [> 1 REQUIRED]", f => _targetFrameworks.Add(f) },
                { "p|project=", "An input project file location for reference resolution. ", p => projectFiles.Add(Path.GetFullPath(p)) },
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
            _projectFiles = projectFiles;

            return codeGenOptions;
        }

        private static void ValidateOptions(OptionSet options, bool shouldShowHelp)
        {
            var errorMessage = string.Empty;
            if (_assemblyNames.Count == 0) errorMessage += "At least one input assembly is required.";
            if (_outputAssemblyName == null) errorMessage += "Output assembly name is required.";
            if (_targetFrameworks.Count == 0) errorMessage += "At least one target framework is required.";

            var assemblyFilesNotFound = _assemblyNames.Where(an => !File.Exists(an)).ToList();
            if (assemblyFilesNotFound.Count > 0)
                errorMessage += $"Unable to find the following assembly files:\n{string.Join("\n", assemblyFilesNotFound)}";

            var projectFilesNotFound = _projectFiles.Where(pf => !File.Exists(pf)).ToList();
            if (_projectFiles.Count > 0 && projectFilesNotFound.Count > 0)
                errorMessage += $"Unable to find the following project files:\n{string.Join("\n", projectFilesNotFound)}";

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

    public class PackageReferenceInfo : IEquatable<PackageReferenceInfo>
    {
        public string PackageName { get; }

        public string PackageVersion { get; }

        public string ReferenceString { get; }

        public PackageReferenceInfo(string packageName, string packageVersion, string referenceString)
        {
            this.PackageName = packageName;
            this.PackageVersion = packageVersion;
            this.ReferenceString = referenceString;
        }

        public bool Equals(PackageReferenceInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(PackageName, other.PackageName) && string.Equals(PackageVersion, other.PackageVersion);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PackageReferenceInfo) obj);
        }

        public override int GetHashCode()
        {
            return (PackageName + PackageVersion).GetHashCode();
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Ambrosia;
using Mono.Options;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ambrosia
{
    class Program
    {
        private static AmbrosiaCSRuntimeModes _runtimeMode;
        private static List<string> _assemblyNames;
        private static string _outputAssemblyName;

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
            sourceFiles.Add(new SourceFile() { FileName = $"ImmortalSerializer.cs", SourceCode = immortalSerializerSource, });


            var projectFileSource =
@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net46;netcoreapp2.0</TargetFrameworks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\..\..\..\Clients\CSharp\AmbrosiaLibCS\AmbrosiaLibCS.csproj"" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Update=""Microsoft.NETCore.App"" Version=""2.0.0"" />
  </ItemGroup>
</Project>
";

            var projectSourceFile =
                new SourceFile() { FileName = $"{_outputAssemblyName}.csproj", SourceCode = projectFileSource };
            sourceFiles.Add(projectSourceFile);

            var directoryName = "latest";

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
                { "a|assembly=", "An input assembly name. [REQUIRED]", assemblyName => assemblyNames.Add(assemblyName) },
                { "o|outputAssemblyName=", "An output assembly name. [REQUIRED]", outputAssemblyName => _outputAssemblyName = outputAssemblyName },
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

            var assemblyFilesNotFound = _assemblyNames.Where(an => !File.Exists(an)).ToList();
            if (assemblyFilesNotFound.Count > 0)
                errorMessage += $"Unable to find the following assembly files:\n{string.Join("\n", assemblyFilesNotFound)}";

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

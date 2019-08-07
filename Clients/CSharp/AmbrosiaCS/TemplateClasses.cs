using System.Collections.Generic;
using System.Linq;
using Ambrosia;

namespace Ambrosia
{
    internal sealed class TypeDefinitionInformation
    {
        /// <summary>
        /// A string that is a valid C# source string for referring to the type
        /// </summary>
        public string Name;
        public string Namespace;
        public IEnumerable<MethodInformation> Methods;
        public bool IsByteArray;
    }
    internal sealed class MethodInformation
    {
        /// <summary>
        /// A string that is a valid C# source string for referring to the method
        /// </summary>
        readonly public string Name;
        readonly public uint idNumber;
        readonly public IEnumerable<ParameterInformation> Parameters;
        readonly public TypeDefinitionInformation ReturnType;
        readonly public bool voidMethod;
        readonly public bool isImpulseHandler;

        public MethodInformation(string name, uint i, TypeDefinitionInformation returnType, bool isVoidMethod, bool isImpulseHandler, IEnumerable<ParameterInformation> parameters)
        {
            this.idNumber = i + 1; // this way 0 is always left open to use as the id for OnFirstStart
            this.Name = name;
            this.Parameters = parameters; // mi.GetParameters().ToArray();
            this.ReturnType = returnType; // Utilities.GetCSharpSourceSyntax(mi.ReturnType);
            this.isImpulseHandler = isImpulseHandler;
            this.voidMethod = isVoidMethod; // mi.ReturnType.Equals(typeof(void));
        }

    }
    internal sealed class ParameterInformation
    {
        public string Name;
        public string TypeName;
        public int Position;
        public TypeDefinitionInformation ParameterType;
    }

    internal partial class ProxyGenerator
    {
        readonly TypeDefinitionInformation interfaceType;
        private readonly string generatedClientInterfaceName;
        readonly string className;
        readonly IEnumerable<MethodInformation> methods;

        internal ProxyGenerator(TypeDefinitionInformation originalInterface, string generatedClientInterfaceName)
        {
            this.generatedClientInterfaceName = generatedClientInterfaceName;
            this.interfaceType = originalInterface;
            this.className = generatedClientInterfaceName + "_Implementation";
            this.methods = originalInterface.Methods;
        }
    }
    internal partial class ProxyInterfaceGenerator
    {
        readonly TypeDefinitionInformation interfaceType;

        internal ProxyInterfaceGenerator(TypeDefinitionInformation interfaceType)
        {
            this.interfaceType = interfaceType;
        }
    }
    internal partial class DispatcherGenerator
    {
        /// <summary>
        /// A string that is a valid C# source string for referring to the type
        /// </summary>
        readonly TypeDefinitionInformation interfaceType;
        readonly string className;
        readonly IEnumerable<MethodInformation> methods;

        internal DispatcherGenerator(TypeDefinitionInformation interfaceType)
        {
            this.interfaceType = interfaceType;
            this.className = Utilities.CleanUpIdentifierName(interfaceType.Name) + "_Dispatcher_Implementation";
            this.methods = interfaceType.Methods;
        }
    }

    internal partial class ImmortalSerializerGenerator
    {
        /// <summary>
        /// A string that is a valid C# source string for referring to the type
        /// </summary>
        private readonly string[] classNamespaces;
        private readonly string[] typeNames;

        internal ImmortalSerializerGenerator(IEnumerable<string> classNames, IEnumerable<string> classNamespaces)
        {
            this.typeNames = classNames.ToArray();
            this.classNamespaces = new HashSet<string>(classNamespaces).ToArray(); // uniquify the list.
        }
    }
}
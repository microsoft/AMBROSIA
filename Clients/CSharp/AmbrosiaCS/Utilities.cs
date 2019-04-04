using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Ambrosia;

namespace Ambrosia
{
    internal static class Utilities
    {

        public static bool CanHandleType(Type t)
        {
            if (t.IsArray && t.GetElementType().Equals(typeof(byte))) return true;
            if (t.GetCustomAttribute<DataContractAttribute>() != null) return true;
            if (t.GetCustomAttribute<SerializableAttribute>() != null) return true;
            return false;
        }
        public static string ComputeArgumentSize(Type t, int position)
        {
            if (!CanHandleType(t))
            {
                throw new NotImplementedException("Need to handle the type: " + GetCSharpSourceSyntax(t));
            }
            return ComputeArgumentSize(GetTypeDefinitionInformation(t), position);
        }
        public static string ComputeArgumentSize(TypeDefinitionInformation t, int position)
        {
            var sb = new StringBuilder();
            if (t.IsByteArray)
            {
                sb.AppendLine($"arg{position}Bytes = p_{position};");
                sb.AppendLine($"arg{position}Size = IntSize(arg{position}Bytes.Length) + arg{position}Bytes.Length;");
            }
            else
            {
                sb.AppendLine($"arg{position}Bytes = Ambrosia.BinarySerializer.Serialize<{t.Namespace + "." + t.Name}>(p_{position});");
                sb.AppendLine($"arg{position}Size = IntSize(arg{position}Bytes.Length) + arg{position}Bytes.Length;");
            }
            return sb.ToString();
        }

        public static string ComputeExceptionSize()
        {
            return ComputeExceptionSize("Ex");
        }

        public static string ComputeExceptionSize(string identifier)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"var arg{identifier}Object = this.exceptionSerializer.Serialize(curr{identifier});");
            sb.AppendLine($"arg{identifier}Bytes = Ambrosia.BinarySerializer.Serialize(arg{identifier}Object);");
            sb.AppendLine($"arg{identifier}Size = IntSize(arg{identifier}Bytes.Length) + arg{identifier}Bytes.Length;");
            return sb.ToString();
        }

        public static string ParameterDeclarationString(MethodInfo m)
        {
            return String.Join((string) ",",
                (IEnumerable<string>) m
                    .GetParameters()
                    .Select(p => String.Format("{0} p_{1}", GetCSharpSourceSyntax(p.ParameterType), p.Position.ToString())));
        }

        public static string ParameterDeclarationString(MethodInformation m)
        {
            return String.Join(",",
                m
                    .Parameters
                    .Select(p => String.Format("{0} p_{1}", p.TypeName, p.Position.ToString())));
        }

        public static string DeserializeValue(Type typeOfValue, string variableToAssignTo)
        {

            return DeserializeValue(GetTypeDefinitionInformation(typeOfValue), variableToAssignTo);
        }

        /// <summary>
        /// Assumes that either the type of the value is a byte array or else has either is marked [DataContract] or [Serializable]
        /// </summary>
        /// <param name="typeName">
        /// Must be a valid C# source syntax for the type name.
        /// </param>
        /// <param name="isByteArray"></param>
        /// <param name="variableToAssignTo"></param>
        /// <returns></returns>
        public static string DeserializeValue(TypeDefinitionInformation typeOfValue, string variableToAssignTo)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"var {variableToAssignTo}_ValueLength = buffer.ReadBufferedInt(cursor);");
            sb.AppendLine($"cursor += IntSize({variableToAssignTo}_ValueLength);");
            sb.AppendLine($"var {variableToAssignTo}_ValueBuffer = new byte[{variableToAssignTo}_ValueLength];");
            sb.AppendLine($"Buffer.BlockCopy(buffer, cursor, {variableToAssignTo}_ValueBuffer, 0, {variableToAssignTo}_ValueLength);");
            sb.AppendLine($"cursor += {variableToAssignTo}_ValueLength;");
            if (typeOfValue.IsByteArray)
            {
                sb.AppendLine($"var {variableToAssignTo} = {variableToAssignTo}_ValueBuffer;");
            }
            else
            {
                sb.AppendLine($"var {variableToAssignTo} = Ambrosia.BinarySerializer.Deserialize<{typeOfValue.Namespace + "." + typeOfValue.Name}>({variableToAssignTo}_ValueBuffer);");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="typeName">
        /// Must be a valid C# source syntax for the type name.
        /// </param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static string SerializeValue(int position)
        {
            return SerializeValue(position.ToString());
        }
        public static string SerializeException()
        {
            return SerializeValue("Ex");
        }

        public static string SerializeValue(string identifier)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"wp.curLength += wp.PageBytes.WriteInt(wp.curLength, arg{identifier}Bytes.Length);");
            sb.AppendLine($"Buffer.BlockCopy(arg{identifier}Bytes, 0, wp.PageBytes, wp.curLength, arg{identifier}Bytes.Length);");
            sb.AppendLine($"wp.curLength += arg{identifier}Bytes.Length;");
            return sb.ToString();
        }

        public static string GetCSharpSourceSyntax(this Type t)
        {
            var list = new List<string>();
            string ret = TurnTypeIntoCSharpSource(t, ref list);
            return ret;
        }

        private static string TurnTypeIntoCSharpSource(Type t, ref List<string> introducedGenericTypeParameters)
        {
            Contract.Requires(t != null);
            Contract.Requires(introducedGenericTypeParameters != null);

            if (t.Equals(typeof(void))) return "void";
            var typeName = t.FullName.Replace('#', '_').Replace('+', '.');
            if (IsAnonymousTypeName(t))
            {
                var newGenericTypeParameter = CleanUpIdentifierName(t.FullName); // "A" + introducedGenericTypeParameters.Count.ToString(CultureInfo.InvariantCulture);
                introducedGenericTypeParameters.Add(newGenericTypeParameter);
                return newGenericTypeParameter;
            }

            if (!t.GetTypeInfo().IsGenericType) // need to test after anonymous because deserialized anonymous types are *not* generic (but unserialized anonymous types *are* generic)
                return typeName;
            var isDynamic = t.GetTypeInfo().Assembly.IsDynamic;
            var sb = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(t.Namespace))
            {
                sb.Append(t.Namespace);
                sb.Append(".");
            }

            var genericArgs = new List<string>();
            foreach (var genericArgument in t.GenericTypeArguments)
            {
                string gaName = TurnTypeIntoCSharpSource(genericArgument, ref introducedGenericTypeParameters);
                genericArgs.Add(gaName);
            }

            // Need to handle nested types, e.g., T1<X,Y>.T2<A,B,C> in C#
            // The names look like this: T1`2.T2`3 where there are 5 generic arguments
            // The generic arguments go into different places in the source version of the type name.
            var indexIntoGenericArguments = 0;
            var listOfTypes = new List<Type>();
            var t2 = t;
            while (t2 != null)
            {
                listOfTypes.Add(t2);
                t2 = t2.DeclaringType;
            }

            listOfTypes.Reverse(); // start at the root of the nested type chain
            for (int typeIndex = 0; typeIndex < listOfTypes.Count; typeIndex++)
            {
                var currentType = listOfTypes[typeIndex];
                var currentName = currentType.Name;
                var indexOfBackTick = currentType.Name.IndexOf('`');
                if (0 < typeIndex)
                {
                    sb.Append(".");
                }

                sb.Append(0 < indexOfBackTick ? currentName.Substring(0, indexOfBackTick) : currentName);
                if (0 < indexOfBackTick)
                {
                    var j = indexOfBackTick + 1;
                    while (j < currentName.Length && Char.IsDigit(currentName[j])) j++;
                    var numberOfGenerics = int.Parse(currentName.Substring(indexOfBackTick + 1, j - (indexOfBackTick + 1)));
                    sb.Append("<");
                    if (!isDynamic)
                    {
                        for (int i = 0; i < numberOfGenerics; i++)
                        {
                            if (0 < i) { sb.Append(", "); }

                            sb.Append(genericArgs[indexIntoGenericArguments]);
                            indexIntoGenericArguments++;
                        }
                    }
                    else
                    {
                        indexIntoGenericArguments += numberOfGenerics;
                    }

                    sb.Append(">");
                }
            }

            typeName = sb.ToString();
            return typeName;
        }

        private static bool IsAnonymousTypeName(this Type type)
        {
            Contract.Requires(type != null);

            return type.GetTypeInfo().IsClass
                   && type.GetTypeInfo().IsDefined(typeof(CompilerGeneratedAttribute))
                   && !type.IsNested
                   && type.Name.StartsWith("<>", StringComparison.Ordinal)
                   && type.Name.Contains("__Anonymous");
        }

        internal static string CleanUpIdentifierName(this string s)
        {
            Contract.Requires(s != null);

            return s.Replace('`', '_').Replace('.', '_').Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace(' ', '_').Replace('`', '_').Replace('[', '_').Replace(']', '_').Replace('=', '_').Replace('+', '_');
        }

        public static TypeDefinitionInformation GetTypeDefinitionInformation(Type t)
        {
            //if (!CanHandleType(t))
            //{
            //    throw new NotImplementedException($"Need to handle the type: {GetCSharpSourceSyntax(t)}");
            //}

            var iTypeInfo = new TypeDefinitionInformation()
            {
                //Name = Utilities.CleanUpIdentifierName(GetCSharpSourceSyntax(t)),
                Name = GetTypeDefinitionInformationName(t),
                Namespace = t.Namespace,
                Methods = t
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Select((m, i) => GetMethodInformation(m, (uint)i)),
                IsByteArray = t.IsArray && t.GetElementType().Equals(typeof(byte)),
            };
            return iTypeInfo;

        }

        public static string GetTypeDefinitionInformationName(Type t)
        {
            if (t.Equals(typeof(void)))
            {
                return "void";
            }
            else if (t.GetGenericArguments().Length > 0)
            {
                StringBuilder sb = new StringBuilder();
                string baseName = t.Name.Substring(0, t.Name.IndexOf('`'));
                sb.Append(baseName);
                sb.Append("<");
                for (int i = 0; i < t.GetGenericArguments().Length; i++)
                {
                    Type genericType = t.GetGenericArguments()[i];
                    var genericTypeDefinitionInfo = GetTypeDefinitionInformation(genericType);
                    sb.Append(genericTypeDefinitionInfo.Namespace + "." + genericTypeDefinitionInfo.Name);
                    if (i < t.GetGenericArguments().Length - 1)
                    {
                        sb.Append(",");
                    }
                }
                sb.Append(">");
                return sb.ToString();
            }
            else
            {
                return t.Name;
            }
        }

        public static MethodInformation GetMethodInformation(MethodInfo m, uint i)
        {
            return new MethodInformation(m.Name, i, GetTypeDefinitionInformation(m.ReturnType),
                m.ReturnType.Equals(typeof(void)), Enumerable.Any<Attribute>(m.GetCustomAttributes(typeof(ImpulseHandlerAttribute))),
                m.GetParameters().Select(p => GetParameterInformation(p)));
        }

        public static ParameterInformation GetParameterInformation(ParameterInfo p)
        {
            return new ParameterInformation()
            {
                Name = p.Name,
                TypeName = Utilities.GetCSharpSourceSyntax(p.ParameterType),
                Position = p.Position,
                ParameterType = GetTypeDefinitionInformation(p.ParameterType),
            };
        }
    }
}
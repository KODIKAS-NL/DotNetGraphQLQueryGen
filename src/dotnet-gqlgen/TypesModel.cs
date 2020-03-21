using System.Collections.Generic;
using dotnet_gqlgen;

namespace dotnet_gqlgen
{
    public class TypesModel
    {
        public string SchemaFile;
        public string Namespace;
        public Dictionary<string, TypeInfo> Types;
        public TypeInfo Mutation;
        public string CmdArgs;
    }
}
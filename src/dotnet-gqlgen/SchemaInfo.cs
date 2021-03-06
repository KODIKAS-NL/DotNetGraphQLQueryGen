using System;
using System.Collections.Generic;
using System.Linq;

namespace dotnet_gqlgen
{
    public class SchemaInfo
    {
        public bool UnknownTypesAsString { get; set; }

        public static string[] Nullables = new string[] { "int", "bool", "float", "datetime", "double", "char" };
        public Dictionary<string, string> typeMappings = new Dictionary<string, string> {
            {"String", "string"},
            {"ID", "string"},
            {"Int", "int"},
            {"Float", "double"},
            {"Boolean", "bool"},
            {"Date", "DateTime"}
        };

        public SchemaInfo(Dictionary<string, string> typeMappings)
        {
            if (typeMappings != null)
            {
                foreach (var item in typeMappings)
                {
                    // overrides
                    this.typeMappings[item.Key] = item.Value;
                }
            }
            Schema = new List<Field>();
            Types = new Dictionary<string, TypeInfo>();
            Inputs = new Dictionary<string, TypeInfo>();
            Scalars = new List<string>();
        }

        public List<Field> Schema { get; }
        /// <summary>
        /// Return the query type info.
        /// </summary>
        public TypeInfo Query => Types[Schema.First(f => f.Name == "query").TypeName];
        /// <summary>
        /// Return the mutation type info.
        /// </summary>
        public TypeInfo Mutation
        {
            get
            {
                var typeName = Schema.First(f => f.Name == "mutation")?.TypeName;
                if (typeName != null)
                    return Types[typeName];
                return null;
            }
        }
        public List<EnumItem> Enums { get; } = new List<EnumItem>();
        public Dictionary<string, TypeInfo> Types { get; }
        public Dictionary<string, TypeInfo> Inputs { get; }
        public List<string> Scalars { get; }

        internal bool HasDotNetType(string typeName)
        {
            return typeMappings.ContainsKey(typeName) || Types.ContainsKey(typeName) || Inputs.ContainsKey(typeName) || Enums.Any(e=>e.Name == typeName);
        }

        internal string GetDotNetType(string typeName, bool required = true)
        {
            if (typeMappings.ContainsKey(typeName))
            {
                var type = typeMappings[typeName];
                return type + (!required && Nullables.Contains(type, StringComparer.OrdinalIgnoreCase) ? "?" : "");
            }
            if (Types.ContainsKey(typeName))
                return Types[typeName].Name;
            if (Enums.Any(e=>e.Name == typeName))
                return typeName + (required ? "" : "?");
            return Inputs[typeName].Name;
        }
    }

    public class TypeInfo
    {
        public TypeInfo(IEnumerable<Field> fields, string name, string description, bool isInput = false)
        {
            Fields = fields.ToList();
            Name = name;
            Description = description;
            IsInput = isInput;
        }

        public List<Field> Fields { get; }
        public string Name { get; }
        public string Description { get; }
        public bool IsInput { get; }
    }

    public class Field
    {
        private readonly SchemaInfo schemaInfo;

        public Field(SchemaInfo schemaInfo)
        {
            Args = new List<Arg>();
            this.schemaInfo = schemaInfo;
        }
        public bool Required { get; set; } = true;
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsArray { get; set; }
        public List<Arg> Args { get; set; }
        public string Description { get; set; }
        public string Default { get; set; }
        public string DotNetName => Name[0].ToString().ToUpper() + string.Join("", Name.Skip(1));
        public string DotNetType
        {
            get
            {
                return IsArray ? $"{dotNetTypeSingle(false)}[]" : DotNetTypeSingle;
            }
        }
        public string DotNetTypeSingle
        {
            get => dotNetTypeSingle();
        }

        private string dotNetTypeSingle(bool checkRequired = true){
            if (!schemaInfo.HasDotNetType(TypeName))
            {
                if (schemaInfo.UnknownTypesAsString)
                {
                    Console.WriteLine($"Unknown type '{TypeName}' returning String");
                    return "string";
                }
                throw new SchemaException($"Unknown dotnet type for schema type '{TypeName}'. Please provide a mapping for any custom scalar types defined in the schema");
            }
            return schemaInfo.GetDotNetType(TypeName, checkRequired ? Required : true);
        }

        public bool ShouldBeProperty
        {
            get
            {
                return (Args.Count == 0 && !schemaInfo.Types.ContainsKey(TypeName) && !schemaInfo.Inputs.ContainsKey(TypeName)) || schemaInfo.Scalars.Contains(TypeName);
            }
        }

        public string ArgsOutput()
        {
            if (!Args.Any())
                return "";
            return string.Join(", ", Args.Select(a => $"{a.DotNetType} {a.Name}{(!String.IsNullOrWhiteSpace(a.Default) ? " = " + a.Default : "")}"));
        }

        public override string ToString()
        {
            return $"{Name}:{(IsArray ? '[' + TypeName + ']' : TypeName)}";
        }
    }

    public class Arg : Field
    {
        public Arg(SchemaInfo schemaInfo) : base(schemaInfo)
        {
        }
    }

    public class EnumEntry
    {
        public EnumEntry(string name, string comment)
        {
            Name = name;
            Comment = comment;
        }
        public string Name { get; }
        public string Comment { get; }
    }

    public class EnumItem : EnumEntry
    {
        public EnumItem(string name, string comment, List<EnumEntry> entries) : base(name, comment)
        {
            this.Entries = entries;
        }
        public List<EnumEntry> Entries { get; }
    }
}
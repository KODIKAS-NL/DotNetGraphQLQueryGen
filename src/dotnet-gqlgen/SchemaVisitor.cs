using System;
using System.Collections.Generic;
using System.Linq;
using GraphQLSchema.Grammer;

namespace dotnet_gqlgen
{
    internal class SchemaVisitor : GraphQLSchemaBaseVisitor<object>
    {
        private readonly SchemaInfo schemaInfo;
        private List<Field> addFieldsTo;

        public SchemaInfo SchemaInfo => schemaInfo;

        public SchemaVisitor(Dictionary<string, string> typeMappings, bool unknownTypesAsString = false)
        {
            this.schemaInfo = new SchemaInfo(typeMappings) { UnknownTypesAsString = unknownTypesAsString };
        }

        private static string[] reservedWords = new string[]{
            "abstract","as","base","bool","break","byte","case","catch",
            "char","checked","class","const","continue","decimal","default",
            "delegate","do","double","else","enum","event","explicit",
            "extern","false","finally","fixed","float","for","foreach",
            "goto","if","implicit","in","int","interface","internal",
            "is","lock","long","namespace","new","null","object","operator",
            "out","override","params","private","protected","public",
            "readonly","ref","return","sbyte","sealed","short","sizeof",
            "stackalloc","static","string","struct","switch","this","throw",
            "true","try","typeof","uint","ulong","unchecked","unsafe",
            "ushort","using","using static","virtual","void","volatile","while"
        };
        private string EscapeReserved(string name) => reservedWords.Contains(name) ? $"@{name}" : name;

        public override object VisitFieldDef(GraphQLSchemaParser.FieldDefContext context)
        {
            var docComment = context.comment().LastOrDefault();
            var desc = docComment != null ? (string)VisitComment(docComment) : null;
            var name = context.name.Text;
            var args = (List<Arg>)VisitArguments(context.args);
            var type = context.type.GetText();
            var isArray = type[0] == '[';
            type = type.Trim('[', ']');
            return new Field(this.schemaInfo)
            {
                Name = EscapeReserved(name),
                TypeName = type,
                IsArray = isArray,
                Args = args,
                Description = desc,
                Required = context.required != null,
                //Default = context.value?.Text
            };

        }

        public override object VisitArguments(GraphQLSchemaParser.ArgumentsContext context)
        {
            var args = new List<Arg>();
            if (context != null)
            {
                foreach (var arg in context.argument())
                {
                    var type = arg.dataType().GetText();
                    var isArray = type[0] == '[';
                    type = type.Trim('[', ']');
                    args.Add(new Arg(this.schemaInfo)
                    {
                        Name = EscapeReserved(arg.NAME().FirstOrDefault()?.GetText()),
                        TypeName = type,
                        Required = arg.required != null,
                        IsArray = isArray,
                        //Default = arg.value?.Text
                    });
                }
            }
            return args;
        }

        internal void SetFieldConsumer(List<Field> item)
        {
            this.addFieldsTo = item;
        }

        public override object VisitComment(GraphQLSchemaParser.CommentContext context)
        {
            return context.GetText().Trim('"', ' ', '\t', '\n', '\r');
        }

        public override object VisitSchemaDef(GraphQLSchemaParser.SchemaDefContext context)
        {
            var fields = context.objectDef().fieldDef().Select(f => VisitFieldDef(f)).Cast<Field>().ToList();
            schemaInfo.Schema.AddRange(fields);
            return null;
        }
        public override object VisitEnumDef(GraphQLSchemaParser.EnumDefContext context)
        {
            var docComment = context.comment().LastOrDefault();
            var desc = docComment != null ? (string)VisitComment(docComment) : null;
            var enumItems = context.enumItem().Select(i => VisitEnumItem(i)).Cast<string>().Select(e => new EnumEntry(e, "")).ToList();
            this.schemaInfo.Enums.Add(new EnumItem(context.typeName.Text, desc, enumItems));
            return null;
        }

        public override object VisitEnumItem(GraphQLSchemaParser.EnumItemContext context)
        {
            var name = context.name.Text;
            return EscapeReserved(name);
        }

        public override object VisitInputDef(GraphQLSchemaParser.InputDefContext context)
        {
            var docComment = context.comment().LastOrDefault();
            var desc = docComment != null ? (string)VisitComment(docComment) : null;
            var fields = context.objectDef().fieldDef().Select(f => VisitFieldDef(f)).Cast<Field>().ToList();
            schemaInfo.Inputs.Add(context.typeName.Text, new TypeInfo(fields, context.typeName.Text, desc, isInput: true));
            return null;
        }
        public override object VisitTypeDef(GraphQLSchemaParser.TypeDefContext context)
        {
            var docComment = context.comment().LastOrDefault();
            var desc = docComment != null ? (string)VisitComment(docComment) : null;

            var fields = context.objectDef().fieldDef().Select(f => VisitFieldDef(f)).Cast<Field>().ToList();
            schemaInfo.Types.Add(context.typeName.Text, new TypeInfo(fields, context.typeName.Text, desc));
            return null;
        }
        public override object VisitScalarDef(GraphQLSchemaParser.ScalarDefContext context)
        {
            var result = base.VisitScalarDef(context);
            schemaInfo.Scalars.Add(context.typeName.Text);
            return result;
        }
    }
}
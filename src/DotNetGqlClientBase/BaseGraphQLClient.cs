using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace DotNetGqlClient
{
    public abstract class BaseGraphQLClient
    {
        protected virtual string MakeQuery<TSchema, TQuery>(Expression<Func<TSchema, TQuery>> query, bool mutation = false)
        {
            var gql = new StringBuilder();
            gql.AppendLine($"{(mutation ? "mutation" : "query")} BaseGraphQLClient {{");

            if (query.NodeType != ExpressionType.Lambda)
                throw new ArgumentException($"Must provide a LambdaExpression", "query");
            var lambda = (LambdaExpression)query;

            if (lambda.Body.NodeType != ExpressionType.New && lambda.Body.NodeType != ExpressionType.MemberInit)
                throw new ArgumentException($"LambdaExpression must return a NewExpression or MemberInitExpression");

            GetObjectSelection(gql, lambda.Body, 1);

            gql.AppendLine(@"}");
            return gql.ToString();
        }

        private static void GetObjectSelection(StringBuilder gql, Expression exp, int depth)
        {
            if (exp.NodeType == ExpressionType.New)
            {
                var newExp = (NewExpression)exp;
                for (int i = 0; i < newExp.Arguments.Count; i++)
                {
                    var fieldVal = newExp.Arguments[i];
                    var fieldProp = newExp.Members[i];
                    gql.AppendLine($"{String.Join("", Enumerable.Range(0, depth).Select(_ => "  "))}{fieldProp.Name}: {GetFieldSelection(fieldVal,depth)}");
                }
            }
            else
            {
                var mi = (MemberInitExpression)exp;
                for (int i = 0; i < mi.Bindings.Count; i++)
                {
                    var valExp = ((MemberAssignment)mi.Bindings[i]).Expression;
                    var fieldVal = mi.Bindings[i].Member;
                    gql.AppendLine($"{String.Join("", Enumerable.Range(0, depth).Select(_ => "  "))}{mi.Bindings[i].Member.Name}: {GetFieldSelection(valExp,depth)}");
                }
            }
        }

        private static string GetFieldSelection(Expression field, int depth)
        {
            if (field.NodeType == ExpressionType.MemberAccess)
            {
                var member = ((MemberExpression)field).Member;
                var attribute = member.GetCustomAttributes(typeof(GqlFieldNameAttribute)).Cast<GqlFieldNameAttribute>().FirstOrDefault();
                if (attribute != null)
                    return attribute.Name;
                return member.Name;
            }
            else if (field.NodeType == ExpressionType.Call)
            {
                var call = (MethodCallExpression)field;
                return GetSelectionFromMethod(call,depth);
            }
            else if (field.NodeType == ExpressionType.Quote)
            {
                return GetFieldSelection(((UnaryExpression)field).Operand,depth);
            }
            else
            {
                throw new ArgumentException($"Field expression should be a call or member access expression", "field");
            }
        }

        private static string GetSelectionFromMethod(MethodCallExpression call, int depth)
        {
            var select = new StringBuilder();

            var attribute = call.Method.GetCustomAttributes(typeof(GqlFieldNameAttribute)).Cast<GqlFieldNameAttribute>().FirstOrDefault();
            if (attribute != null)
                select.Append(attribute.Name);
            else
                select.Append(call.Method.Name);

            if (call.Arguments.Count > 1)
            {
                var argVals = new List<object>();
                for (int i = 0; i < call.Arguments.Count - 1; i++)
                {
                    var arg = call.Arguments.ElementAt(i);
                    var param = call.Method.GetParameters().ElementAt(i);
                    var paramName = param.Name.Replace("@", "");
                    Type argType = null;
                    object argVal = null;
                    if (arg.NodeType == ExpressionType.Convert)
                    {
                        arg = ((UnaryExpression)arg).Operand;
                    }

                    if (arg.NodeType == ExpressionType.Constant)
                    {
                        var constArg = (ConstantExpression)arg;
                        argType = constArg.Type;
                        argVal = constArg.Value;
                    }
                    else if (arg.NodeType == ExpressionType.MemberAccess)
                    {
                        ConstantExpression ce = null;
                        var mex = new List<MemberExpression> { (MemberExpression)arg };
                        while (ce == null)
                        {
                            var ma = mex.First();
                            if ((ce = ma.Expression as ConstantExpression) == null)
                                mex.Insert(0, (MemberExpression)ma.Expression);
                        }
                        argType = arg.Type;
                        argVal = ce.Value;
                        foreach (var ma in mex)
                        {
                            if (ma.Member.MemberType == MemberTypes.Field)
                            {
                                argVal = ((FieldInfo)ma.Member).GetValue(argVal);
                            }
                            else
                            {
                                argVal = ((PropertyInfo)ma.Member).GetValue(argVal);
                            }
                        }
                    }
                    else if (arg.NodeType == ExpressionType.New)
                    {
                        argVal = Expression.Lambda(arg).Compile().DynamicInvoke();
                        argType = argVal.GetType();
                    }

                    else
                    {
                        throw new Exception($"Unsupported argument type {arg.NodeType}");
                    }
                    if (argVal == null)
                        continue;


                    if (argType == typeof(string) || argType == typeof(Guid) || argType == typeof(Guid?))
                    {
                        argVals.Add($"{paramName}: \"{argVal}\"");
                    }
                    else if (argType == typeof(DateTime) || argType == typeof(DateTime?))
                    {
                        argVals.Add($"{paramName}: \"{((DateTime)argVal).ToString("o")}\"");
                    }
                    else if (argType == typeof(bool))
                    {
                        argVals.Add($"{paramName}:{(((bool)argVal) ? "true" : "false")}");
                    }
                    else if (typeof(Array).IsAssignableFrom(argType))
                    {
                        var arrayVal = JsonConvert.SerializeObject((Array)argVal);
                        argVals.Add($"{paramName}: {arrayVal}");
                    }
                    else
                    {
                        argVals.Add($"{paramName}: {argVal}");
                    }
                };
                if (argVals.Any())
                    select.Append($"({string.Join(", ", argVals)})");
            }
            select.Append(" {\n");
            if (call.Arguments.Count == 0)
            {
                if (call.Method.ReturnType.GetInterfaces().Select(i => i.GetTypeInfo().GetGenericTypeDefinition()).Contains(typeof(IEnumerable<>)))
                {
                    select.Append(GetDefaultSelection(call.Method.ReturnType.GetGenericArguments().First(),depth+1));
                }
                else
                {
                    select.Append(GetDefaultSelection(call.Method.ReturnType,depth+1));
                }
            }
            else
            {
                var exp = call.Arguments.Last();
                if (exp.NodeType == ExpressionType.Quote)
                    exp = ((UnaryExpression)exp).Operand;
                if (exp.NodeType == ExpressionType.Lambda)
                    exp = ((LambdaExpression)exp).Body;
                GetObjectSelection(select, exp, depth+1);
            }
            select.Append(String.Join("", Enumerable.Range(0, depth).Select(_ => "  ")));
            select.Append("}");
            return select.ToString();
        }

        private static string GetDefaultSelection(Type returnType, int depth)
        {
            var select = new StringBuilder();
            foreach (var field in returnType.GetProperties())
            {
                var name = field.Name;
                var attribute = field.GetCustomAttributes(typeof(GqlFieldNameAttribute)).Cast<GqlFieldNameAttribute>().FirstOrDefault();
                if (attribute != null)
                    name = attribute.Name;

                select.AppendLine($"{String.Join("", Enumerable.Range(0, depth).Select(_ => "  "))}{field.Name}: {name}");
            }
            return select.ToString();
        }
    }
}
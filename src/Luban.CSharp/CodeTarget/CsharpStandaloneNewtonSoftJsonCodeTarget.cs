// Copyright 2025 Code Philosophy

using Luban.CodeTarget;
using Luban.CSharp.TemplateExtensions;
using Luban.Diagnostics;
using Luban.Types;
using Scriban;
using Scriban.Runtime;

namespace Luban.CSharp.CodeTarget;

[CodeTarget("cs-standalone-newtonsoft-json")]
public class CsharpStandaloneNewtonSoftJsonCodeTarget : CsharpNewtonSoftJsonCodeTarget
{
    protected override void ValidationTypeNames(GenerationContext ctx)
    {
        base.ValidationTypeNames(ctx);
        foreach (var bean in ctx.ExportBeans.Where(bean => bean.IsValueType))
        {
            foreach (var field in bean.ExportFields)
            {
                string name = CodeStyle.FormatProperty(field.Name);
                if (name is "__LubanResolvedRefs" or "__lubanResolvedRefs")
                {
                    throw new LubanException(bean.Source, "error.codegen.standalone_json_member_conflict", bean.FullName, field.Name, name);
                }
            }
        }
    }

    protected override void OnCreateTemplateContext(TemplateContext ctx)
    {
        base.OnCreateTemplateContext(ctx);
        ctx.PushGlobal(new StandaloneTemplateExtension());
    }

    private sealed class StandaloneTemplateExtension : ScriptObject
    {
        public static string StandaloneToPrettyString(string fieldName, TType type)
        {
            return type switch
            {
                TArray or TList or TSet => $"(\"[\" + global::System.String.Join<{CsharpTemplateExtension.DeclaringTypeName(type.ElementType)}>(\",\", {fieldName}) + \"]\")",
                TMap => $"(\"{{\" + global::System.String.Concat(global::System.Linq.Enumerable.Select({fieldName}, _entry => _entry.Key + \":\" + _entry.Value + \",\")) + \"}}\")",
                _ => fieldName,
            };
        }

        public static bool StandaloneBeanNeedsResolveRef(TType type)
        {
            return type is TBean { DefBean.TypeMappers: null };
        }

        public static string StandaloneResolveRefCall(TBean type)
        {
            return type.DefBean.IsValueType && !type.IsNullable ? ".ResolveRef(tables);" : "?.ResolveRef(tables);";
        }
    }
}

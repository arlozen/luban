// Copyright 2025 Code Philosophy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Luban.CodeTarget;
using Luban.CSharp.TemplateExtensions;
using Luban.Defs;
using Luban.Diagnostics;
using Luban.Types;
using Scriban;
using Scriban.Runtime;

namespace Luban.CSharp.CodeTarget;

[CodeTarget("cs-et-json")]
public class CsharpEtJsonCodeTarget : CsharpCodeTargetBase
{
    protected override bool NeedGenerateTables => false;

    protected override string GetTypeOutputPath(DefTypeBase type)
    {
        string typePath = base.GetTypeOutputPath(type);
        if (!EnvManager.Current.TryGetOption(Name, "schemaPackagesRoot", false, out string packagesRoot)
            || string.IsNullOrEmpty(type.Source?.File))
        {
            return typePath;
        }

        string sourcePath = Path.GetFullPath(type.Source.File, GenerationContext.GlobalConf.ConfigDir);
        string relativePath = Path.GetRelativePath(packagesRoot, sourcePath);
        int separator = relativePath.IndexOf(Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relativePath) || separator < 0 || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return typePath;
        }

        return $"{relativePath[..separator]}/{typePath}";
    }


    protected override void ValidationTypeNames(GenerationContext ctx)
    {
        base.ValidationTypeNames(ctx);
        foreach (var bean in ctx.ExportBeans)
        {
            foreach (var field in bean.ExportFields)
            {
                string name = CodeStyle.FormatProperty(field.Name);
                if (name == "OnAfterDeserialize" ||
                    (bean.IsValueType && name is "__LubanResolvedRefs" or "__lubanResolvedRefs"))
                {
                    throw new LubanException(bean.Source, "error.codegen.et_json_member_conflict", bean.FullName, field.Name, name);
                }
            }
        }
    }

    protected override void OnCreateTemplateContext(TemplateContext ctx)
    {
        base.OnCreateTemplateContext(ctx);
        ctx.PushGlobal(new CsharpSimpleJsonTemplateExtension());
        ctx.PushGlobal(new EtReferenceTemplateExtension());
    }

    private sealed class EtReferenceTemplateExtension : ScriptObject
    {
        public static string EtStringLiteral(string value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value);
        }

        public static bool EtBeanNeedsResolveRef(TType type)
        {
            return type is TBean { DefBean.TypeMappers: null };
        }

        public static string EtResolveRefCall(TBean type)
        {
            return type.DefBean.IsValueType && !type.IsNullable ? ".ResolveRef();" : "?.ResolveRef();";
        }
    }
}

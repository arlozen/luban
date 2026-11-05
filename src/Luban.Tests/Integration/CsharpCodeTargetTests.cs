// Copyright 2025 Code Philosophy

using System.Collections;
using System.Reflection;
using Luban.Diagnostics;
using Luban.Tests.Helpers;
using Xunit;

namespace Luban.Tests.Integration;

[Collection("LubanPipeline")]
public class CsharpCodeTargetTests
{
    [Theory]
    [InlineData("cs-dotnet-json", "json")]
    [InlineData("cs-simple-json", "json")]
    [InlineData("cs-newtonsoft-json", "json")]
    [InlineData("cs-bin", "bin")]
    public void Core_CodeTarget_CompilesAndLoads(string codeTarget, string dataTarget)
    {
        using var ws = new TempWorkspace();
        LubanRunner.Run(
            TestPaths.FixtureConf("core"),
            codeTargets: new[] { codeTarget },
            dataTargets: new[] { dataTarget },
            outputCodeDir: ws.CodeDir,
            outputDataDir: ws.DataDir);

        var tables = CsharpCompileHarness.LoadTables(ws.CodeDir, ws.DataDir, codeTarget);
        var tbItem = CsharpCompileHarness.GetProp<object>(tables, "TbItem");
        var item = CsharpCompileHarness.Invoke(tbItem, "Get", 1);
        Assert.NotNull(item);
        Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(item, "Name"));

        var global = CsharpCompileHarness.GetProp<object>(tables, "TbGlobal");
        Assert.Equal(3, CsharpCompileHarness.GetProp<int>(global, "Version"));
        Assert.Equal("demo", CsharpCompileHarness.GetProp<string>(global, "Title"));
    }

    [Fact]
    public void Core_CsEditorJson_GeneratesFiles()
    {
        using var ws = new TempWorkspace();
        LubanRunner.Run(
            TestPaths.FixtureConf("core"),
            codeTargets: new[] { "cs-editor-json" },
            outputCodeDir: ws.CodeDir);
        Assert.True(Directory.GetFiles(ws.CodeDir, "*.cs", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public void Core_EtJson_GeneratesProviderBasedConfigs()
    {
        using var ws = new TempWorkspace();
        LubanRunner.Run(
            TestPaths.FixtureConf("core"),
            codeTargets: new[] { "cs-et-json" },
            dataTargets: new[] { "json" },
            outputCodeDir: ws.CodeDir,
            outputDataDir: ws.DataDir);

        Assert.Empty(Directory.GetFiles(ws.CodeDir, "Tables.cs", SearchOption.AllDirectories));
        WriteEtPartialExtensions(ws.CodeDir);

        var categories = CsharpCompileHarness.LoadEtConfigCategories(ws.CodeDir, ws.DataDir, "cs-et-json");
        var tbItem = categories["TbItem"];
        var tbItemType = tbItem.GetType();
        var asm = tbItemType.Assembly;

        var ctor = tbItemType.GetConstructors().Single();
        var ctorParam = ctor.GetParameters().Single();
        Assert.Equal("IConfigJsonDataProvider", ctorParam.ParameterType.Name);
        Assert.Equal("ET", ctorParam.ParameterType.Namespace);

        var categoryInterface = asm.GetType("ET.IConfigCategory");
        Assert.NotNull(categoryInterface);
        Assert.True(categoryInterface.IsAssignableFrom(tbItemType));
        var configTypeProperty = categoryInterface.GetProperty("ConfigType");
        Assert.NotNull(configTypeProperty);
        Assert.Equal(asm.GetType("cfg.Item"), configTypeProperty.GetValue(tbItem));

        var item = CsharpCompileHarness.Invoke(tbItem, "Get", 1);
        var itemType = item.GetType();
        var configInterface = asm.GetType("ET.IConfig");
        Assert.NotNull(configInterface);
        Assert.True(configInterface.IsAssignableFrom(itemType));
        Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(item, "Name"));

        var shape = CsharpCompileHarness.GetProp<object>(item, "Shape");
        Assert.Equal("Circle", shape.GetType().Name);
        Assert.Equal(1.5f, CsharpCompileHarness.GetProp<float>(shape, "Radius"), 3);

        var tbGlobal = categories["TbGlobal"];
        Assert.Equal(3, CsharpCompileHarness.GetProp<int>(tbGlobal, "Version"));
        Assert.Equal("demo", CsharpCompileHarness.GetProp<string>(tbGlobal, "Title"));

        var tbScore = categories["TbScore"];
        var score = CsharpCompileHarness.Invoke(tbScore, "Get", 1, 2);
        Assert.Equal(200, CsharpCompileHarness.GetProp<int>(score, "Score"));

        var callsField = tbItemType.GetField("AfterDeserializeCalls", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(callsField);
        Assert.Equal(1, (int)callsField.GetValue(null));

        var calledField = itemType.GetField("AfterDeserializeCalled", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(calledField);
        Assert.True((bool)calledField.GetValue(null));
    }

    [Fact]
    public void EtJson_ResolvesValueTypeRefsThroughSingleton()
    {
        using var ws = new TempWorkspace();
        LubanRunner.Run(
            TestPaths.FixtureConf("et-json"),
            codeTargets: new[] { "cs-et-json" },
            dataTargets: new[] { "json" },
            outputCodeDir: ws.CodeDir,
            outputDataDir: ws.DataDir);

        Assert.Empty(Directory.GetFiles(ws.CodeDir, "Tables.cs", SearchOption.AllDirectories));

        var categories = CsharpCompileHarness.LoadEtConfigCategories(ws.CodeDir, ws.DataDir, "cs-et-json");
        var tbReward = categories["TbReward"];
        var reward = CsharpCompileHarness.Invoke(tbReward, "Get", 100);
        Assert.True(reward.GetType().IsValueType);

        var settings = categories["TbEtSettings"];
        Assert.Equal("settings", CsharpCompileHarness.GetProp<string>(settings, "ConfigType"));
        var nestedReward = CsharpCompileHarness.GetProp<object>(settings, "Reward");
        var nestedRewards = CsharpCompileHarness.GetProp<IEnumerable>(settings, "Rewards");
        foreach (var row in new[] { reward, nestedReward, Assert.Single(nestedRewards) })
        {
            var refItem = CsharpCompileHarness.GetProp<object>(row, "ItemId_Ref");
            Assert.NotNull(refItem);
            Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(refItem, "Name"));
            Assert.Same(refItem, Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(row, "ItemIds_Ref")));
        }
    }

    [Fact]
    public void EtJson_ReportsAfterDeserializeHookNameConflict()
    {
        using var ws = new TempWorkspace();
        var confPath = Path.Combine(ws.Root, "luban.conf");
        File.Copy(TestPaths.FixtureConf("et-json"), confPath);
        var schemaDir = Directory.CreateDirectory(Path.Combine(ws.Root, "Defines"));
        var dataDir = Directory.CreateDirectory(Path.Combine(ws.Root, "Data"));
        File.WriteAllText(Path.Combine(dataDir.FullName, "settings.json"), """{"OnAfterDeserialize":"settings"}""");
        File.WriteAllText(Path.Combine(schemaDir.FullName, "schema.xml"), """
            <module name="">
                <bean name="Settings">
                    <var name="OnAfterDeserialize" type="string"/>
                </bean>
                <table name="TbSettings" value="Settings" mode="one" input="settings.json"/>
            </module>
            """);

        var result = LubanRunner.Run(confPath,
            codeTargets: new[] { "cs-et-json" },
            outputCodeDir: ws.CodeDir,
            throwOnError: false);

        var failure = result.Exception is AggregateException aggregate
            ? Assert.Single(aggregate.Flatten().InnerExceptions)
            : result.Exception;
        var error = Assert.IsType<LubanException>(failure);
        Assert.Equal("error.codegen.et_json_member_conflict", error.MessageKey);
    }

    private static void WriteEtPartialExtensions(string codeDir)
    {
        File.WriteAllText(Path.Combine(codeDir, "EtPartialExtensions.cs"), """
            namespace cfg
            {
                public partial class TbItem
                {
                    public static int AfterDeserializeCalls;
                    partial void OnAfterDeserialize() => AfterDeserializeCalls++;
                }

                public partial class Item
                {
                    public static bool AfterDeserializeCalled;
                    partial void OnAfterDeserialize() => AfterDeserializeCalled = true;
                }
            }
            """);
    }
}

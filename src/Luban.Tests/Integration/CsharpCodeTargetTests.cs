// Copyright 2025 Code Philosophy

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Luban.Defs;
using Luban.Diagnostics;
using Luban.RawDefs;
using Luban.Schema;
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
    public void EtJson_AggregatedTablesUsePackageIdWhileTypesUseSchemaPackage()
    {
        using var ws = new TempWorkspace();
        var packages = Path.Combine(ws.Root, "Packages");
        var aggregate = Directory.CreateDirectory(Path.Combine(packages, "cn.etetet.luban")).FullName;
        var common = Directory.CreateDirectory(Path.Combine(packages, "cn.etetet.common")).FullName;
        var schema = XDocument.Load(TestPaths.Fixture("core/Defines/schema.xml"));
        var tables = new XElement("module", new XAttribute("name", ""));
        foreach (var table in schema.Root.Elements("table").ToList())
        {
            table.SetAttributeValue("packageId", "cn.etetet.business");
            table.Remove();
            tables.Add(table);
        }

        var tablePath = Path.Combine(aggregate, "tables.xml");
        var typePath = Path.Combine(common, "types.xml");
        new XDocument(tables).Save(tablePath);
        schema.Save(typePath);
        var config = JsonNode.Parse(File.ReadAllText(TestPaths.FixtureConf("core")));
        config["schemaFiles"] = new JsonArray(
            new JsonObject { ["fileName"] = tablePath, ["type"] = "" },
            new JsonObject { ["fileName"] = typePath, ["type"] = "" });
        config["dataDir"] = TestPaths.Fixture("core/Data");
        var configPath = Path.Combine(aggregate, "luban.conf");
        File.WriteAllText(configPath, config.ToJsonString());

        LubanRunner.Run(
            configPath,
            codeTargets: new[] { "cs-et-json" },
            outputCodeDir: ws.CodeDir,
            extraOptions: new Dictionary<string, string> { ["cs-et-json.schemaPackagesRoot"] = packages });

        Assert.True(File.Exists(Path.Combine(ws.CodeDir, "cn.etetet.business", "TbItem.cs")));
        Assert.True(File.Exists(Path.Combine(ws.CodeDir, "cn.etetet.common", "Item.cs")));
        Assert.True(File.Exists(Path.Combine(ws.CodeDir, "cn.etetet.common", "Quality.cs")));
        Assert.False(Directory.Exists(Path.Combine(ws.CodeDir, "cn.etetet.luban")));
    }

    [Theory]
    [InlineData("package-tables.xlsx", true)]
    [InlineData("legacy-tables.xlsx", false)]
    public void EtJson_LoadsPackagedAndUnpackagedTablesFromOneGeneration(string workbook, bool hasPackageColumn)
    {
        using var ws = new TempWorkspace();
        var schema = XDocument.Load(TestPaths.Fixture("core/Defines/schema.xml"));
        foreach (var table in schema.Root.Elements("table").ToList())
        {
            if ((string)table.Attribute("name") == "TbScore")
            {
                table.SetAttributeValue("packageId", "com.example.score");
                table.SetAttributeValue("output", @"ranked\scores");
            }
            else
            {
                table.Remove();
            }
        }
        schema.Save(Path.Combine(ws.Root, "schema.xml"));
        File.Copy(TestPaths.Fixture($"core/{workbook}"), Path.Combine(ws.Root, "tables.xlsx"));

        var config = JsonNode.Parse(File.ReadAllText(TestPaths.FixtureConf("core")));
        config["schemaFiles"] = JsonNode.Parse("""
            [
              { "fileName": "schema.xml", "type": "" },
              { "fileName": "tables.xlsx", "type": "table" }
            ]
            """);
        config["dataDir"] = TestPaths.Fixture("core/Data");
        var configPath = Path.Combine(ws.Root, "luban.conf");
        File.WriteAllText(configPath, config.ToJsonString());

        LubanRunner.Run(
            configPath,
            codeTargets: new[] { "cs-et-json" },
            dataTargets: new[] { "json" },
            outputCodeDir: ws.CodeDir,
            outputDataDir: ws.DataDir,
            variants: new Dictionary<string, string> { ["TbItem"] = "battle" });

        var itemDir = hasPackageColumn ? Path.Combine(ws.DataDir, "com.example.battle") : ws.DataDir;
        Assert.True(File.Exists(Path.Combine(itemDir, "tbitem.json")));
        Assert.True(File.Exists(Path.Combine(ws.DataDir, "com.example.score", "ranked", "scores.json")));
        Assert.True(File.Exists(Path.Combine(ws.DataDir, "tbglobal.json")));
        Assert.False(Directory.Exists(Path.Combine(ws.DataDir, "com.example.fallback")));
        var itemCall = hasPackageColumn
            ? """provider.GetJsonElement("com.example.battle", "tbitem")"""
            : """provider.GetJsonElement("tbitem")""";
        Assert.Contains(itemCall, File.ReadAllText(Path.Combine(ws.CodeDir, "TbItem.cs")));
        Assert.Contains("""provider.GetJsonElement("com.example.score", "ranked/scores")""",
            File.ReadAllText(Path.Combine(ws.CodeDir, "TbScore.cs")));
        Assert.Contains("""provider.GetJsonElement("tbglobal")""",
            File.ReadAllText(Path.Combine(ws.CodeDir, "TbGlobal.cs")));

        var categories = CsharpCompileHarness.LoadEtConfigCategories(ws.CodeDir, ws.DataDir, "cs-et-json");
        var item = CsharpCompileHarness.Invoke(categories["TbItem"], "Get", 1);
        Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(item, "Name"));
        var score = CsharpCompileHarness.Invoke(categories["TbScore"], "Get", 1, 2);
        Assert.Equal(200, CsharpCompileHarness.GetProp<int>(score, "Score"));
        Assert.Equal(3, CsharpCompileHarness.GetProp<int>(categories["TbGlobal"], "Version"));
    }

    [Theory]
    [InlineData("../battle", null, "error.def.table.invalid_package_id")]
    [InlineData("NUL", null, "error.def.table.invalid_package_id")]
    [InlineData("battle", "../skill", "error.def.table.invalid_package_output")]
    [InlineData("battle", @"C:\skill", "error.def.table.invalid_package_output")]
    [InlineData("battle", "/skill", "error.def.table.invalid_package_output")]
    public void PackagedTable_RejectsInvalidPaths(string packageId, string output, string messageKey)
    {
        var source = SchemaSource.Create("tables.xlsx", "Tables");
        var table = new RawTable { Name = "TbSkill", Source = source, PackageId = packageId, OutputFile = output };

        var exception = Assert.Throws<LubanException>(() => new DefTable(table));
        Assert.Equal(messageKey, exception.MessageKey);
        Assert.Same(source, exception.SchemaOrigin);
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
        Assert.DoesNotContain(asm.GetTypes(), type => type.Namespace?.StartsWith("Luban", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(asm.GetReferencedAssemblies(), name => name.Name.StartsWith("Luban", StringComparison.Ordinal));

        var ctor = tbItemType.GetConstructor(new[] { asm.GetType("ET.IConfigJsonDataProvider") });
        Assert.NotNull(ctor);
        var ctorParam = ctor.GetParameters().Single();
        Assert.Equal("IConfigJsonDataProvider", ctorParam.ParameterType.Name);
        Assert.Equal("ET", ctorParam.ParameterType.Namespace);
        var providerMethods = ctorParam.ParameterType.GetMethods();
        Assert.Equal(2, providerMethods.Length);
        Assert.All(providerMethods, method =>
        {
            Assert.Equal("GetJsonElement", method.Name);
            Assert.Equal(typeof(JsonElement), method.ReturnType);
        });

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
        Assert.Equal(typeof(object), itemType.BaseType);
        Assert.True(itemType.GetField("Name").IsInitOnly);
        Assert.Equal(itemType.GetField("__ID__").GetRawConstantValue(), CsharpCompileHarness.Invoke(item, "GetTypeId"));
        Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(item, "Name"));
        Assert.Equal("GREEN", CsharpCompileHarness.GetProp<object>(item, "Quality").ToString());
        Assert.Contains("tags:[weapon,melee],", item.ToString());
        Assert.Contains("attrs:{atk:10,def:2,},", item.ToString());
        Assert.Contains("flags:[1,2],", item.ToString());

        var shape = CsharpCompileHarness.GetProp<object>(item, "Shape");
        Assert.Equal("Circle", shape.GetType().Name);
        Assert.Equal(1.5f, CsharpCompileHarness.GetProp<float>(shape, "Radius"), 3);
        var shapeType = asm.GetType("cfg.Shape");
        Assert.Equal(typeof(object), shapeType.BaseType);
        var getTypeId = shapeType.GetMethod("GetTypeId");
        Assert.True(getTypeId.IsAbstract);
        Assert.Equal(shape.GetType().GetField("__ID__").GetRawConstantValue(), getTypeId.Invoke(shape, null));

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
        Assert.True(CsharpCompileHarness.GetProp<bool>(tbItem, "EndRefCalled"));
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
        var rewardArray = CsharpCompileHarness.GetProp<IEnumerable>(settings, "RewardArray");
        var rewardMap = CsharpCompileHarness.GetProp<IDictionary>(settings, "RewardMap");
        var settingsData = CsharpCompileHarness.GetProp<object>(settings, "Data");
        var rewardIndex = CsharpCompileHarness.GetProp<IDictionary>(settingsData, "IndexedRewards_Index");
        var listReward = CsharpCompileHarness.Invoke(categories["TbRewardList"], "GetById", 100);
        foreach (var row in new[] { reward, nestedReward, Assert.Single(nestedRewards), Assert.Single(rewardArray), rewardMap["first"], rewardIndex[100], listReward })
        {
            var refItem = CsharpCompileHarness.GetProp<object>(row, "ItemId_Ref");
            Assert.NotNull(refItem);
            Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(refItem, "Name"));
            Assert.Same(refItem, Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(row, "ItemIds_Ref")));
        }
    }

    [Fact]
    public void EtJson_DeserializesNullableFieldsAndArraysWithoutRetainingDocument()
    {
        using var ws = new TempWorkspace();
        LubanRunner.Run(
            TestPaths.FixtureConf("et-json"),
            codeTargets: new[] { "cs-et-json" },
            dataTargets: new[] { "json" },
            outputCodeDir: ws.CodeDir,
            outputDataDir: ws.DataDir);

        var categories = CsharpCompileHarness.LoadEtConfigCategories(ws.CodeDir, ws.DataDir, "cs-et-json");
        var data = CsharpCompileHarness.GetProp<object>(categories["TbEtSettings"], "Data");
        var deserialize = data.GetType().GetMethod("DeserializeEtSettings");
        Assert.NotNull(deserialize);
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(ws.DataDir, "tbetsettings.json")))[0];
        var optionalFields = new[] { "optional_number", "optional_text", "optional_item", "optional_reward", "optional_item_id" };
        var optionalMembers = new[] { "OptionalNumber", "OptionalText", "OptionalItem", "OptionalReward", "OptionalItemId" };

        // Exercise omitted properties as well as explicit JSON nulls.
        foreach (var explicitNull in new[] { false, true })
        {
            foreach (var field in optionalFields)
            {
                if (explicitNull)
                {
                    json[field] = null;
                }
                else
                {
                    json.AsObject().Remove(field);
                }
            }

            object row;
            using (var document = JsonDocument.Parse(json.ToJsonString()))
            {
                row = deserialize.Invoke(null, new object[] { document.RootElement });
            }

            foreach (var member in optionalMembers)
            {
                Assert.Null(CsharpCompileHarness.GetProp<object>(row, member));
            }
            CsharpCompileHarness.Invoke(row, "ResolveRef");
            Assert.Null(CsharpCompileHarness.GetProp<object>(row, "OptionalItemId_Ref"));
            Assert.Equal(new[] { 3, 5 }, CsharpCompileHarness.GetProp<int[]>(row, "Numbers"));
            var matrix = CsharpCompileHarness.GetProp<int[][]>(row, "Matrix");
            Assert.Equal(new[] { 1, 2 }, matrix[0]);
            Assert.Equal(new[] { 3 }, matrix[1]);
            Assert.Contains("numbers:[3,5],", row.ToString());
        }

        json["optional_number"] = 42;
        json["optional_text"] = "hello";
        json["optional_item"] = JsonNode.Parse("""{"id":2,"name":"shield"}""");
        json["optional_reward"] = json["reward"].DeepClone();
        json["optional_item_id"] = 1;
        object populated;
        using (var document = JsonDocument.Parse(json.ToJsonString()))
        {
            populated = deserialize.Invoke(null, new object[] { document.RootElement });
        }
        CsharpCompileHarness.Invoke(populated, "ResolveRef");
        Assert.Equal(42, CsharpCompileHarness.GetProp<int?>(populated, "OptionalNumber"));
        Assert.Equal("hello", CsharpCompileHarness.GetProp<string>(populated, "OptionalText"));
        Assert.Equal("shield", CsharpCompileHarness.GetProp<string>(CsharpCompileHarness.GetProp<object>(populated, "OptionalItem"), "Name"));
        var expectedItem = CsharpCompileHarness.Invoke(categories["TbItem"], "Get", 1);
        Assert.Same(expectedItem, CsharpCompileHarness.GetProp<object>(populated, "OptionalItemId_Ref"));
        var optionalReward = CsharpCompileHarness.GetProp<object>(populated, "OptionalReward");
        Assert.Same(expectedItem, CsharpCompileHarness.GetProp<object>(optionalReward, "ItemId_Ref"));
    }

    [Fact]
    public void EtJson_UsesStandardJsonExceptions()
    {
        using var ws = new TempWorkspace();
        LubanRunner.Run(
            TestPaths.FixtureConf("core"),
            codeTargets: new[] { "cs-et-json" },
            dataTargets: new[] { "json" },
            outputCodeDir: ws.CodeDir,
            outputDataDir: ws.DataDir);

        var categories = CsharpCompileHarness.LoadEtConfigCategories(ws.CodeDir, ws.DataDir, "cs-et-json");
        var asm = categories["TbGlobal"].GetType().Assembly;
        using var document = JsonDocument.Parse("""{"$type":"MissingShape"}""");
        var deserialize = asm.GetType("cfg.Shape").GetMethod("DeserializeShape");
        var unknownType = Assert.Throws<TargetInvocationException>(() => deserialize.Invoke(null, new object[] { document.RootElement }));
        Assert.IsType<JsonException>(unknownType.InnerException);

        var provider = Activator.CreateInstance(asm.GetType("ET.FileConfigJsonDataProvider"), new object[] { ws.DataDir });
        foreach (var rows in new[] { "[]", "[{},{}]" })
        {
            File.WriteAllText(Path.Combine(ws.DataDir, "tbglobal.json"), rows);
            var invalidSize = Assert.Throws<TargetInvocationException>(() => Activator.CreateInstance(categories["TbGlobal"].GetType(), new[] { provider }));
            Assert.IsType<JsonException>(invalidSize.InnerException);
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
                    public bool EndRefCalled;
                    partial void OnAfterDeserialize() => AfterDeserializeCalls++;
                    partial void EndRef() => EndRefCalled = true;
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

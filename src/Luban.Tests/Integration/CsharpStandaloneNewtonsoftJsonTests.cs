// Copyright 2025 Code Philosophy

using System.Collections;
using System.Reflection;
using System.Xml.Linq;
using Luban.Diagnostics;
using Luban.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Luban.Tests.Integration;

[Collection("LubanPipeline")]
public class CsharpStandaloneNewtonsoftJsonTests
{
    private const string CodeTarget = "cs-standalone-newtonsoft-json";

    [Fact]
    public void Core_LoadsWithOnlyNewtonsoftAndPreservesPublicContracts()
    {
        using var ws = new TempWorkspace();
        var tables = GenerateAndLoad(ws, "core");
        var asm = tables.GetType().Assembly;
        Assert.DoesNotContain(asm.GetTypes(), t => t.Namespace?.StartsWith("Luban", StringComparison.Ordinal) == true || t.Namespace == "ET");
        Assert.DoesNotContain(asm.GetReferencedAssemblies(), a => a.Name.StartsWith("Luban", StringComparison.Ordinal) || a.Name is "ET" or "SimpleJSON" or "System.Text.Json");
        Assert.Contains(asm.GetReferencedAssemblies(), a => a.Name == "Newtonsoft.Json");

        var requestedNames = new List<string>();
        var inputs = new List<JArray>();
        Func<string, JArray> loader = name =>
        {
            Assert.False(Path.HasExtension(name));
            requestedNames.Add(name);
            var json = ParseArray(File.ReadAllText(Path.Combine(ws.DataDir, name + ".json")));
            inputs.Add(json);
            return json;
        };
        tables = Activator.CreateInstance(tables.GetType(), loader);
        Assert.Equal(new[] { "tbglobal", "tbitem", "tbscore" }, requestedNames.OrderBy(n => n));
        foreach (var input in inputs)
        {
            input.Clear();
        }

        var tbItem = CsharpCompileHarness.GetProp<object>(tables, "TbItem");
        var item = CsharpCompileHarness.Invoke(tbItem, "Get", 1);
        Assert.Equal(typeof(object), item.GetType().BaseType);
        Assert.True(item.GetType().GetField("Name").IsInitOnly);
        Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(item, "Name"));
        Assert.Equal("GREEN", CsharpCompileHarness.GetProp<object>(item, "Quality").ToString());
        Assert.Equal(item.GetType().GetField("__ID__").GetRawConstantValue(), CsharpCompileHarness.Invoke(item, "GetTypeId"));
        Assert.Contains("tags:[weapon,melee],", item.ToString());
        Assert.Contains("attrs:{atk:10,def:2,},", item.ToString());
        Assert.Contains("flags:[1,2],", item.ToString());
        Assert.Null(CsharpCompileHarness.Invoke(tbItem, "GetOrDefault", -1));

        var shape = CsharpCompileHarness.GetProp<object>(item, "Shape");
        Assert.Equal("Circle", shape.GetType().Name);
        Assert.Equal(1.5f, CsharpCompileHarness.GetProp<float>(shape, "Radius"), 3);
        var shapeType = asm.GetType("cfg.Shape");
        Assert.Equal(typeof(object), shapeType.BaseType);
        Assert.True(shapeType.GetMethod("GetTypeId").IsAbstract);
        Assert.Equal(shape.GetType().GetField("__ID__").GetRawConstantValue(), CsharpCompileHarness.Invoke(shape, "GetTypeId"));

        var global = CsharpCompileHarness.GetProp<object>(tables, "TbGlobal");
        Assert.Equal(3, CsharpCompileHarness.GetProp<int>(global, "Version"));
        Assert.Equal("demo", CsharpCompileHarness.GetProp<string>(global, "Title"));
        var score = CsharpCompileHarness.Invoke(CsharpCompileHarness.GetProp<object>(tables, "TbScore"), "Get", 1, 2);
        Assert.Equal(200, CsharpCompileHarness.GetProp<int>(score, "Score"));

        var unknownType = Assert.Throws<TargetInvocationException>(() =>
            shapeType.GetMethod("DeserializeShape").Invoke(null, new object[] { JObject.Parse("""{"$type":"MissingShape"}""") }));
        Assert.IsType<JsonException>(unknownType.InnerException);
        foreach (var rows in new[] { "[]", "[{},{}]" })
        {
            var invalidSize = Assert.Throws<TargetInvocationException>(() =>
                Activator.CreateInstance(global.GetType(), JArray.Parse(rows)));
            Assert.IsType<JsonException>(invalidSize.InnerException);
        }
    }

    [Fact]
    public void Structs_ResolveReferencesAcrossCopiesAndDeserializeNullableFields()
    {
        using var ws = new TempWorkspace();
        var tables = GenerateAndLoad(ws, "et-json");
        var expectedItem = CsharpCompileHarness.Invoke(CsharpCompileHarness.GetProp<object>(tables, "TbItem"), "Get", 1);
        var rewardsTable = CsharpCompileHarness.GetProp<object>(tables, "TbReward");
        var reward = CsharpCompileHarness.Invoke(rewardsTable, "Get", 100);
        Assert.True(reward.GetType().IsValueType);
        Assert.Null(reward.GetType().GetField("__ID__"));
        var settings = CsharpCompileHarness.GetProp<object>(tables, "TbEtSettings");
        var data = CsharpCompileHarness.GetProp<object>(settings, "Data");
        var refs = new[]
        {
            reward,
            Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(rewardsTable, "DataList")),
            CsharpCompileHarness.GetProp<IDictionary>(rewardsTable, "DataMap")[100],
            CsharpCompileHarness.GetProp<object>(data, "Reward"),
            Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(data, "Rewards")),
            Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(data, "RewardArray")),
            CsharpCompileHarness.GetProp<IDictionary>(data, "RewardMap")["first"],
            CsharpCompileHarness.GetProp<IDictionary>(data, "IndexedRewards_Index")[100],
            CsharpCompileHarness.Invoke(CsharpCompileHarness.GetProp<object>(tables, "TbRewardList"), "GetById", 100),
        };
        foreach (var row in refs)
        {
            Assert.Same(expectedItem, CsharpCompileHarness.GetProp<object>(row, "ItemId_Ref"));
            Assert.Same(expectedItem, Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(row, "ItemIds_Ref")));
        }
        var defaultReward = Activator.CreateInstance(reward.GetType());
        CsharpCompileHarness.Invoke(defaultReward, "ResolveRef", tables);
        Assert.Null(CsharpCompileHarness.GetProp<object>(defaultReward, "ItemId_Ref"));

        var deserialize = data.GetType().GetMethod("DeserializeEtSettings");
        var json = (JObject)ParseArray(File.ReadAllText(Path.Combine(ws.DataDir, "tbetsettings.json")))[0];
        var optionalFields = new[] { "optional_number", "optional_text", "optional_item", "optional_reward", "optional_item_id" };
        var optionalMembers = new[] { "OptionalNumber", "OptionalText", "OptionalItem", "OptionalReward", "OptionalItemId" };
        foreach (var explicitNull in new[] { false, true })
        {
            foreach (var field in optionalFields)
            {
                if (explicitNull) json[field] = JValue.CreateNull();
                else json.Remove(field);
            }
            var row = deserialize.Invoke(null, new object[] { json });
            CsharpCompileHarness.Invoke(row, "ResolveRef", tables);
            foreach (var member in optionalMembers)
            {
                Assert.Null(CsharpCompileHarness.GetProp<object>(row, member));
            }
            Assert.Null(CsharpCompileHarness.GetProp<object>(row, "OptionalItemId_Ref"));
            Assert.Equal(new[] { 3, 5 }, CsharpCompileHarness.GetProp<int[]>(row, "Numbers"));
            var matrix = CsharpCompileHarness.GetProp<int[][]>(row, "Matrix");
            Assert.Equal(new[] { 1, 2 }, matrix[0]);
            Assert.Equal(new[] { 3 }, matrix[1]);
            Assert.Contains("numbers:[3,5],", row.ToString());
        }

        json["optional_number"] = 42;
        json["optional_text"] = "2026-11-25T10:00:00Z";
        json["optional_item"] = JObject.Parse("""{"id":2,"name":"shield"}""");
        json["optional_reward"] = json["reward"].DeepClone();
        json["optional_item_id"] = 1;
        var populated = deserialize.Invoke(null, new object[] { json });
        CsharpCompileHarness.Invoke(populated, "ResolveRef", tables);
        json.RemoveAll();
        Assert.Equal(42, CsharpCompileHarness.GetProp<int?>(populated, "OptionalNumber"));
        Assert.Equal("2026-11-25T10:00:00Z", CsharpCompileHarness.GetProp<string>(populated, "OptionalText"));
        Assert.Equal("shield", CsharpCompileHarness.GetProp<string>(CsharpCompileHarness.GetProp<object>(populated, "OptionalItem"), "Name"));
        Assert.Same(expectedItem, CsharpCompileHarness.GetProp<object>(populated, "OptionalItemId_Ref"));
        Assert.Same(expectedItem, CsharpCompileHarness.GetProp<object>(CsharpCompileHarness.GetProp<object>(populated, "OptionalReward"), "ItemId_Ref"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StructFieldIndexes_CompileInCsharp9WithOrWithoutTopModule(bool emptyTopModule)
    {
        using var ws = new TempWorkspace();
        var schema = XDocument.Load(TestPaths.Fixture("et-json/Defines/schema.xml"));
        schema.Root.Elements("bean").Single(b => (string)b.Attribute("name") == "Reward").Add(
            new XElement("var", new XAttribute("name", "items"), new XAttribute("type", "(list#index=id),Item")),
            new XElement("var", new XAttribute("name", "item_array"), new XAttribute("type", "array,(int#ref=TbItem)")),
            new XElement("var", new XAttribute("name", "item_set"), new XAttribute("type", "set,(int#ref=TbItem)")),
            new XElement("var", new XAttribute("name", "item_map"), new XAttribute("type", "map,string,(int#ref=TbItem)")));
        var conf = JObject.Parse(File.ReadAllText(TestPaths.FixtureConf("et-json")));
        conf["schemaFiles"] = new JArray(new JObject { ["fileName"] = Path.Combine(ws.Root, "schema.xml"), ["type"] = "" });
        if (emptyTopModule)
        {
            foreach (var target in conf["targets"]) target["topModule"] = "";
        }
        schema.Save(Path.Combine(ws.Root, "schema.xml"));
        // No data generation is needed to verify constructor initialization and namespace lookup.
        var confPath = Path.Combine(ws.Root, "luban.conf");
        conf["dataDir"] = TestPaths.Fixture("et-json/Data");
        File.WriteAllText(confPath, conf.ToString());
        LubanRunner.Run(confPath, codeTargets: new[] { CodeTarget }, outputCodeDir: ws.CodeDir);
        File.WriteAllText(Path.Combine(ws.DataDir, "tbitem.json"), """[{"id":1,"name":"sword"}]""");
        File.WriteAllText(Path.Combine(ws.DataDir, "tbreward.json"), "[]");
        File.WriteAllText(Path.Combine(ws.DataDir, "tbrewardlist.json"), "[]");
        var settings = JObject.Parse(File.ReadAllText(TestPaths.Fixture("et-json/Data/settings.json")));
        foreach (var reward in settings.Descendants().OfType<JObject>().Where(o => o["item_ids"] != null).ToList())
        {
            reward["items"] = new JArray(JObject.Parse("""{"id":1,"name":"sword"}"""));
            reward["item_array"] = new JArray(1);
            reward["item_set"] = new JArray(1);
            reward["item_map"] = new JArray { new JArray("first", 1) };
        }
        File.WriteAllText(Path.Combine(ws.DataDir, "tbetsettings.json"), new JArray(settings).ToString());
        var tables = CsharpCompileHarness.LoadTables(ws.CodeDir, ws.DataDir, CodeTarget);
        var data = CsharpCompileHarness.GetProp<object>(CsharpCompileHarness.GetProp<object>(tables, "TbEtSettings"), "Data");
        var nestedReward = CsharpCompileHarness.GetProp<object>(data, "Reward");
        var item = CsharpCompileHarness.GetProp<IDictionary>(nestedReward, "Items_Index")[1];
        Assert.Equal("sword", CsharpCompileHarness.GetProp<string>(item, "Name"));
        var expectedItem = CsharpCompileHarness.Invoke(CsharpCompileHarness.GetProp<object>(tables, "TbItem"), "Get", 1);
        Assert.Same(expectedItem, Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(nestedReward, "ItemArray_Ref")));
        Assert.Same(expectedItem, Assert.Single(CsharpCompileHarness.GetProp<IEnumerable>(nestedReward, "ItemSet_Ref")));
        Assert.Same(expectedItem, CsharpCompileHarness.GetProp<IDictionary>(nestedReward, "ItemMap_Ref")["first"]);
    }

    [Theory]
    [InlineData("__LubanResolvedRefs", "none", true)]
    [InlineData("__lubanResolvedRefs", "none", true)]
    [InlineData("__LubanResolvedRefs", "csharp-default", false)]
    [InlineData("__lubanResolvedRefs", "csharp-default", false)]
    public void StructInternalMemberConflicts_UseFormattedNames(string fieldName, string codeStyle, bool conflicts)
    {
        using var ws = new TempWorkspace();
        var conf = JObject.Parse(File.ReadAllText(TestPaths.FixtureConf("et-json")));
        var schema = XDocument.Load(TestPaths.Fixture("et-json/Defines/schema.xml"));
        schema.Root.Elements("bean").Single(b => (string)b.Attribute("name") == "Reward").Add(
            new XElement("var", new XAttribute("name", fieldName), new XAttribute("type", "string")));
        schema.Save(Path.Combine(ws.Root, "schema.xml"));
        conf["schemaFiles"] = new JArray(new JObject { ["fileName"] = Path.Combine(ws.Root, "schema.xml"), ["type"] = "" });
        conf["dataDir"] = TestPaths.Fixture("et-json/Data");
        var confPath = Path.Combine(ws.Root, "luban.conf");
        File.WriteAllText(confPath, conf.ToString());
        var result = LubanRunner.Run(confPath, codeTargets: new[] { CodeTarget }, outputCodeDir: ws.CodeDir, throwOnError: false,
            extraOptions: new Dictionary<string, string> { [$"{CodeTarget}.codeStyle"] = codeStyle });
        if (!conflicts)
        {
            Assert.True(result.Succeeded, result.Exception?.ToString());
            return;
        }
        var error = result.Exception is AggregateException aggregate ? Assert.Single(aggregate.Flatten().InnerExceptions) : result.Exception;
        Assert.Equal("error.codegen.standalone_json_member_conflict", Assert.IsType<LubanException>(error).MessageKey);
    }

    private static object GenerateAndLoad(TempWorkspace ws, string fixture)
    {
        LubanRunner.Run(TestPaths.FixtureConf(fixture), codeTargets: new[] { CodeTarget }, dataTargets: new[] { "json" },
            outputCodeDir: ws.CodeDir, outputDataDir: ws.DataDir);
        return CsharpCompileHarness.LoadTables(ws.CodeDir, ws.DataDir, CodeTarget);
    }

    private static JArray ParseArray(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JArray.Load(reader);
    }
}

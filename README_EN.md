# Luban

![icon](docs/images/logo.png)

[![license](http://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT) ![star](https://img.shields.io/github/stars/focus-creative-games/luban?style=flat-square)

[中文](./README.md) | **English**

luban is a powerful, easy-to-use, elegant, and stable game configuration solution. It is designed to meet the needs of simple to complex game configuration workflows from small to very large game projects.

luban can handle a variety of file types, supports popular languages, can generate multiple export formats, supports rich data inspection functions, has good cross-platform capabilities, and generates extremely fast.
Luban has a clear and elegant generation pipeline design, supports good modularization and plug-in, and is convenient for developers to carry out secondary development. Developers can easily adapt luban to their own configuration format, and customize powerful configuration tools that meet project requirements.

Luban standardizes the game configuration development workflow, which can greatly improve the efficiency of planning and programming.

Since **5.0.0**, Luban is **AI Native**: official Agent Skills, machine-readable schema and diagnostics, `Luban.Agent` CLI, and an MCP Server—so agents can reliably add tables, inspect schema, validate, and generate.

## Core features

- Rich source data format. Support excel family (csv, tsv, xls, xlsx, xlsm), json, xml, yaml, lua, etc.
- Rich export formats. Support generating binary, json, bson, xml, lua, yaml and other format data
- Enhanced excel format. Simple configurations such as simple lists, substructures, structured lists, and arbitrarily complex deep nested structures can be concisely configured
- Complete type system. Not only can it express common specification line lists, but it can flexibly and elegantly express complex GamePlay data such as behavior trees, skills, plots, and dungeons because **supports OOP type inheritance**
- Support multiple languages. Supports generating language codes such as c#, java, go, cpp, lua, python, javascript, typescript, php, rust, godot, etc.
- Support popular message schemes. protobuf(schema + binary + json), flatbuffers(schema + json), msgpack(binary)
- Powerful data verification capability. ref reference check, path resource path, range range check, etc.
- Perfect localization support
- **AI Native (5.0.0+)**. Official Agent Skills, `-c schema-json`, `--errorFormat json`, `Luban.Agent` (validate / list / describe), and `Luban.Mcp` (IDE tool calling)
- Supports all major game engines and platforms. Support Unity, Unreal, Cocos2x, Godot, WeChat games, etc.
- Good cross-platform capability. It can run well on Win, Linux, and Mac platforms.
- Support all mainstream hot update solutions. hybridclr, ilruntime, {x,t,s}lua, puerts, etc.
- The generated code makes no reflection API calls, ensuring compatibility with common code obfuscation and hardening tools such as [Obfuz](https://github.com/focus-creative-games/obfuz), Obfuscator, Confuser, and .NET Refactor.
- Clear and elegant generation pipeline, it is easy to carry out secondary development on the basis of luban, and customize a configuration tool suitable for your own project style.

## Documentation

- [Official Documentation](https://www.datable.cn/en/docs/intro)
- [Quick Start](https://www.datable.cn/en/docs/guide/install)
- [AI support (Skills / Agent / MCP / schema-json)](https://www.datable.cn/docs/ai/overview)
- [What's new in 5.x (vs 4.x)](https://www.datable.cn/docs/other/whats-new-5.x)
- **Example Project** ([github](https://github.com/focus-creative-games/luban_examples)) ([gitee](https://gitee.com/focus-creative-games/luban_examples))

AI assets in this repo: [`ai/`](./ai/README.md).

## ET JSON loading

Generate ET configs with `-c cs-et-dotnet-json -d json` for `System.Text.Json`, or `-c cs-et-newtonsoft-json -d json` for `Newtonsoft.Json`. Both targets implement ET contracts without a Luban runtime or SimpleJSON dependency. Root beans implement `ET.IConfig`; tables inherit `ET.Singleton<T>` and implement `ET.IConfigCategory`.

Supply `ET.IConfigJsonDataProvider` with the methods matching the selected target:

```csharp
// cs-et-dotnet-json (bean constructors and deserializers accept JsonElement)
System.Text.Json.JsonElement GetJsonElement(string name);
System.Text.Json.JsonElement GetJsonElement(string packageId, string dataKey);

// cs-et-newtonsoft-json (bean constructors and deserializers accept JToken)
Newtonsoft.Json.Linq.JArray GetJsonArray(string name);
Newtonsoft.Json.Linq.JArray GetJsonArray(string packageId, string dataKey);
```

Names omit the `.json` suffix. Packaged tables use the two-argument overload. A returned `JsonElement` must remain valid throughout table construction; use `RootElement.Clone()` if the provider disposes its `JsonDocument`. For Newtonsoft, reference `Newtonsoft.Json` in the consumer and use `DateParseHandling.None` when parsing to preserve date-like strings. Generated beans do not retain input JSON nodes.

Construct and register all table singletons before calling `ResolveRef()` on each table. `OnAfterDeserialize()` runs during construction, before cross-table references are resolved. Unknown `$type` values and invalid single-row table sizes throw the selected library's `JsonException`.

The former `cs-et-json` target has been renamed to `cs-et-dotnet-json` and is no longer registered. Update command lines and option prefixes, including `cs-et-dotnet-json.schemaPackagesRoot`. The Newtonsoft target uses the `cs-et-newtonsoft-json` option prefix and requires the corresponding provider contract above.

## Developers: running tests

In-repo xUnit integration tests (no dependency on external `luban_examples`):

```bash
dotnet test src/Luban.Tests/Luban.Tests.csproj
```

- Fixtures and expected outputs: `tests/fixtures/`, `tests/golden/`
- Refresh goldens after export changes: `pwsh tests/scripts/update-goldens.ps1` or `bash tests/scripts/update-goldens.sh`
- Or set `LUBAN_UPDATE_GOLDEN=1` while running tests to rewrite goldens in place

## Support and contact

- QQ group: 692890842 (Luban development exchange group)
- discord: https://discord.gg/dGY4zzGMJ4
- Email: luban@code-philosophy.com


## license

Luban is licensed under the [MIT](https://github.com/focus-creative-games/luban/blob/main/LICENSE) license

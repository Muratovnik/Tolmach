using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using NUnit.Framework;

[TestFixture]
public sealed class InstalledApiContracts
{
    public static IEnumerable<TestCaseData> Cases()
    {
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "contracts.json"))))
        {
            foreach (var method in document.RootElement.GetProperty("methods").EnumerateArray())
                yield return new TestCaseData("method", method.GetRawText()).SetName("Method_" + method[0].GetString() + "_" + method[1].GetString());
            foreach (var field in document.RootElement.GetProperty("fields").EnumerateArray())
                yield return new TestCaseData("field", field.GetRawText()).SetName("Field_" + field[0].GetString() + "_" + field[1].GetString());
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void ExactInstalledMetadataMatchesContract(string kind, string json)
    {
        var path = typeof(InstalledApiContracts).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "GameAssembly").Value;
        Assert.That(File.Exists(path), Is.True, "Pass GamePath for the installed game; no Unity process is started.");
        using (var document = JsonDocument.Parse(json))
        {
            var row = document.RootElement;
            path = Path.Combine(Path.GetDirectoryName(path), row[kind == "field" ? 4 : 5].GetString());
            using (var assembly = AssemblyDefinition.ReadAssembly(path))
            {
            var type = assembly.MainModule.GetTypes().SingleOrDefault(t => t.FullName == row[0].GetString());
            Assert.That(type, Is.Not.Null, "Missing native type");
            if (kind == "field")
            {
                var field = type.Fields.SingleOrDefault(f => f.Name == row[1].GetString());
                Assert.That(field, Is.Not.Null, "Missing native field");
                Assert.That(field.FieldType.FullName, Is.EqualTo(row[2].GetString()));
                Assert.That(field.IsStatic, Is.EqualTo(row[3].GetBoolean()));
            }
            else
            {
                var parameters = row[2].EnumerateArray().Select(p => p.GetString()).ToArray();
                var matches = type.Methods.Where(m => m.Name == row[1].GetString() &&
                    m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters)).ToArray();
                Assert.That(matches.Length, Is.EqualTo(1), "Missing or ambiguous exact native method signature");
                Assert.That(matches[0].ReturnType.FullName, Is.EqualTo(row[3].GetString()));
                Assert.That(matches[0].IsStatic, Is.EqualTo(row[4].GetBoolean()));
                Assert.That(matches[0].HasBody, Is.True, "Patch target must have a managed body");
            }
            }
        }
        using (var stream = File.OpenRead(path))
            TestContext.Progress.WriteLine("Installed API SHA256: " + Convert.ToHexString(SHA256.HashData(stream)));
    }
}


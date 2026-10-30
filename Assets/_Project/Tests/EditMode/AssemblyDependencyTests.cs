using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AS.Tests
{
    /// <summary>
    /// Keeps ML-Agents out of the game: only AS.Agents may reference the ML-Agents package, and no game
    /// assembly may reference AS.Agents. Read from the .asmdef files, so it holds before AS.Agents has
    /// any scripts to compile.
    /// </summary>
    public class AssemblyDependencyTests
    {
        const string Scripts = "Assets/_Project/Scripts";
        const string Agents = "AS.Agents";
        const string MLAgents = "Unity.ML-Agents";

        [System.Serializable]
        class AsmDef
        {
            public string name;
            public string[] references;
        }

        static IEnumerable<AsmDef> AsmDefs() =>
            AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { Scripts })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>)
                .Select(a => JsonUtility.FromJson<AsmDef>(a.text));

        static IEnumerable<TestCaseData> GameAssemblies() =>
            AsmDefs().Where(a => a.name != Agents).Select(a => new TestCaseData(a.name, a.references).SetName($"{{m}}({a.name})"));

        [TestCaseSource(nameof(GameAssemblies))]
        public void GameAssembly_DoesNotDependOnAgents(string name, string[] references)
        {
            CollectionAssert.DoesNotContain(references, MLAgents);
            CollectionAssert.DoesNotContain(references, Agents);
        }

        [Test]
        public void AgentsAssembly_ReferencesMLAgents()
        {
            var agents = AsmDefs().SingleOrDefault(a => a.name == Agents);
            Assert.That(agents, Is.Not.Null, $"{Agents}.asmdef is missing under {Scripts}");
            CollectionAssert.Contains(agents.references, MLAgents);
        }
    }
}

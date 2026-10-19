using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Math;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public class AssemblyLayerTests
    {
        private const string GameAssembliesRoot = "Assets/Game";

        private static readonly Dictionary<string, int> Layers = new Dictionary<string, int>
        {
            ["Game.Core"] = 0,
            ["Game.Platform.Windows"] = 1,
            ["Game.Presentation"] = 1,
            ["Game.Net"] = 1,
            ["Game.Bootstrap"] = 2,
            ["Game.Editor"] = 3
        };

        [Serializable]
        private class AssemblyDefinition
        {
            public string name = string.Empty;
            public string[] references = Array.Empty<string>();
            public bool noEngineReferences;
        }

        private static IReadOnlyList<AssemblyDefinition> LoadGameAssemblies() =>
            Directory.GetFiles(GameAssembliesRoot, "*.asmdef", SearchOption.AllDirectories)
                .Select(path => JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path)))
                .ToList();

        [Test]
        public void EveryGameAssemblyHasAKnownLayer()
        {
            var names = LoadGameAssemblies().Select(assembly => assembly.name).ToList();

            Assert.That(names, Is.EquivalentTo(Layers.Keys));
        }

        [Test]
        public void GameAssembliesOnlyReferenceLowerLayers()
        {
            foreach (var assembly in LoadGameAssemblies())
            {
                foreach (var reference in assembly.references.Where(Layers.ContainsKey))
                {
                    Assert.That(Layers[reference], Is.LessThan(Layers[assembly.name]),
                        $"{assembly.name} must not reference {reference}");
                }
            }
        }

        [Test]
        public void CoreHasNoReferencesAndNoEngine()
        {
            var core = LoadGameAssemblies().Single(assembly => assembly.name == "Game.Core");

            Assert.That(core.references, Is.Empty);
            Assert.That(core.noEngineReferences, Is.True);
        }

        [Test]
        public void CompiledCoreDoesNotReferenceUnity()
        {
            var unityReferences = typeof(BigNumber).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("Unity", StringComparison.Ordinal));

            Assert.That(unityReferences, Is.Empty);
        }
    }
}

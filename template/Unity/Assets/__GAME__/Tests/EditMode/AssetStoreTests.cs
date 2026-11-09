using System.Collections.Generic;
using System.Linq;
using __GAME__.Editor;
using NUnit.Framework;

namespace __GAME__.Tests {
    public sealed class AssetStoreTests {
        // Every entry in Unity/AssetStore.json has what Editor/AssetStoreRestore needs to restore it.
        [Test]
        public void EveryEntryIsComplete() {
            List<string> problems = AssetStoreRestore.Load().Packages.SelectMany(AssetStoreRestore.Problems).ToList();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}

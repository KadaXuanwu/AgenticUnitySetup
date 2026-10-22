using NUnit.Framework;
using UnityEditor;

namespace __GAME__.Tests {
    public sealed class DataTests {
        // The Design Hub and the game read the one GameConfig under Content/Data.
        [Test]
        public void OneGameConfigInContentData() {
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:GameConfig", new[] { "Assets/__GAME__/Content/Data" }).Length);
        }
    }
}

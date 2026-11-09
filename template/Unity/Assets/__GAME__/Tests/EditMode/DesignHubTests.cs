using __GAME__.Editor;
using NUnit.Framework;

namespace __GAME__.Tests {
    public sealed class DesignHubTests {
        // Every GameConfig header, sheet section and config type is on a Design Hub page, not under Other
        // (Editor/DesignHub, Pages).
        [Test]
        public void EverythingIsOnAPage() {
            string[] unplaced = DesignHub.Unplaced();
            Assert.IsEmpty(unplaced, "Put these on a Design Hub page: " + string.Join("; ", unplaced));
        }
    }
}

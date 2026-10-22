using UnityEngine;

namespace __GAME__.Runtime {
    // The root balance config: every number the designer tunes, grouped by [Header]. Each header goes on a Design Hub
    // page (Editor/DesignHub, Pages). Wiring between assets goes above the first header, where the hub does not show it.
    [CreateAssetMenu(menuName = "__GAME__/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject {
    }
}

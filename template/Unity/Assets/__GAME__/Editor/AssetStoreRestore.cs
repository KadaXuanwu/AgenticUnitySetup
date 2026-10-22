using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Unity.Scripting.LifecycleManagement;
using Debug = UnityEngine.Debug;

namespace __GAME__.Editor {
    // Asset Store packages are not committed: AssetStore.json records them, and this imports any that are
    // missing from the local Asset Store download cache. After any import of a listed package, however
    // started, our committed patches go back on top.
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    internal static class AssetStoreRestore {
        private const string ManifestPath = "AssetStore.json";
        // The only Assets/ root entry git commits; must match the whitelist in Unity/.gitignore.
        private const string OwnRoot = "Assets/__GAME__";
        // Other Assets/ root entries that are expected: FMOD for Unity's settings (committed, whitelisted in
        // Unity/.gitignore) and its plugins (rebuilt on every machine).
        private static readonly string[] Expected = { OwnRoot, "Assets/Resources", "Assets/Plugins" };
        private const string CheckedKey = "__GAME__.AssetStoreRestore.Checked";

        [Serializable]
        private sealed class Manifest {
            public Entry[] Packages = Array.Empty<Entry>();
        }

        [Serializable]
        private sealed class Entry {
            public int Id;
            public string Title;
            public string Publisher;
            public string Version;
            public string Cache;
            public string Root;
            public string Probe;
        }

        static AssetStoreRestore() {
            AssetDatabase.importPackageCompleted += OnImported;
            if (SessionState.GetBool(CheckedKey, false)) {
                return;
            }
            SessionState.SetBool(CheckedKey, true);
            EditorApplication.delayCall += Restore;
        }

        [MenuItem("__GAME__/Asset Store/Restore Missing")]
        private static void Restore() {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            WarnUnlisted(manifest);

            foreach (Entry package in manifest.Packages.Where(p => !File.Exists(p.Probe))) {
                string cached = Path.Combine(CacheRoot(), package.Cache);
                if (!File.Exists(cached)) {
                    Debug.LogWarning($"Asset Store package '{package.Title}' ({package.Publisher}) {package.Version} is " +
                        $"missing. Import it from Package Manager > My Assets; our patches go back on top by themselves. " +
                        $"Store page: https://assetstore.unity.com/packages/slug/{package.Id}");
                    continue;
                }
                string cachedVersion = CachedVersion(cached);
                if (cachedVersion != package.Version) {
                    Debug.LogWarning($"'{package.Title}': AssetStore.json pins {package.Version}, the cache holds " +
                        $"{cachedVersion}. Importing it anyway; check our patches under {package.Root} still apply.");
                }
                UnityEditor.AssetPackage.Package.Import(cached, false);
            }
        }

        // Vendor files we changed are force-added to git; the import just overwrote them with the originals.
        private static void OnImported(string packageName) {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            Entry package = manifest.Packages.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p.Cache) == packageName);
            if (package == null) {
                return;
            }
            ProcessStartInfo start = new ProcessStartInfo("git", $"checkout -- \"{package.Root}\"") {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (Process git = Process.Start(start)) {
                // Exit code 1 ("did not match") only means nothing under the root is patched.
                git.WaitForExit();
            }
            AssetDatabase.Refresh();
            Debug.Log($"Imported Asset Store package '{package.Title}' and restored our patches under {package.Root}.");
        }

        // Anything at the Assets/ root that is neither ours nor listed is ignored by git and reaches no clone.
        private static void WarnUnlisted(Manifest manifest) {
            string[] known = manifest.Packages.Select(p => p.Root).Concat(Expected).ToArray();
            foreach (string entry in Directory.GetFileSystemEntries("Assets")) {
                string path = entry.Replace('\\', '/');
                if (path.EndsWith(".meta") || known.Contains(path)) {
                    continue;
                }
                Debug.LogWarning($"{path} is not committed: move it into {OwnRoot}, or add its Asset Store package " +
                    $"to {ManifestPath}.");
            }
        }

        private static string CacheRoot() {
            string custom = Environment.GetEnvironmentVariable("ASSETSTORE_CACHE_PATH");
            if (!string.IsNullOrEmpty(custom)) {
                return custom;
            }
            return Application.platform == RuntimePlatform.OSXEditor
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Unity/Asset Store-5.x")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unity/Asset Store-5.x");
        }

        // Asset Store downloads carry their metadata as JSON in the gzip header's extra field.
        private static string CachedVersion(string path) {
            byte[] header = new byte[4096];
            using (FileStream stream = File.OpenRead(path)) {
                stream.Read(header, 0, header.Length);
            }
            if ((header[3] & 4) == 0) {
                return "unknown";
            }
            int length = header[10] | header[11] << 8;
            string json = Encoding.UTF8.GetString(header, 16, Math.Min(length - 4, header.Length - 16));
            Match version = Regex.Match(json, "\"version\":\"([^\"]+)\"");
            return version.Success ? version.Groups[1].Value : "unknown";
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        // The other Assets/ root entry that is expected: Resources, for FMOD for Unity's settings (committed,
        // whitelisted in Unity/.gitignore). Only its FMOD folder is committed.
        private const string ResourcesRoot = "Assets/Resources";
        private const string FmodResources = "Assets/Resources/FMOD";
        private const string CheckedKey = "__GAME__.AssetStoreRestore.Checked";

        [Serializable]
        internal sealed class Manifest {
            public Entry[] Packages = Array.Empty<Entry>();
        }

        [Serializable]
        internal sealed class Entry {
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

        internal static Manifest Load() {
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest();
        }

        // What is wrong with one AssetStore.json entry; empty when nothing is. AssetStoreTests checks every entry.
        internal static IEnumerable<string> Problems(Entry package) {
            string name = string.IsNullOrEmpty(package.Title) ? $"entry {package.Id}" : $"'{package.Title}'";
            if (package.Id <= 0) {
                yield return $"{name}: Id must be the store's package id.";
            }
            if (string.IsNullOrEmpty(package.Title) || string.IsNullOrEmpty(package.Publisher)
                || string.IsNullOrEmpty(package.Version)) {
                yield return $"{name}: Title, Publisher and Version must be filled in.";
            }
            if (string.IsNullOrEmpty(package.Cache) || !package.Cache.EndsWith(".unitypackage")) {
                yield return $"{name}: Cache must be the .unitypackage file, relative to the download cache.";
            }
            if (string.IsNullOrEmpty(package.Root) || !package.Root.StartsWith("Assets/") || package.Root.Length <= 7
                || package.Root.IndexOf('/', 7) >= 0 || package.Root == OwnRoot || package.Root == ResourcesRoot) {
                yield return $"{name}: Root must be the package's own folder at the Assets/ root.";
            } else if (string.IsNullOrEmpty(package.Probe) || !package.Probe.StartsWith(package.Root + "/")) {
                yield return $"{name}: Probe must be a file under {package.Root}.";
            }
        }

        [MenuItem("__GAME__/Asset Store/Restore Missing")]
        private static void Restore() {
            Manifest manifest = Load();
            WarnUnlisted(manifest);

            foreach (Entry package in manifest.Packages) {
                foreach (string problem in Problems(package)) {
                    Debug.LogWarning($"{ManifestPath}: {problem}");
                }
            }

            foreach (Entry package in manifest.Packages.Where(p => !string.IsNullOrEmpty(p.Probe) && !File.Exists(p.Probe))) {
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
            Manifest manifest = Load();
            Entry package = manifest.Packages.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p.Cache) == packageName);
            if (package == null) {
                return;
            }
            ProcessStartInfo start = new ProcessStartInfo("git", $"checkout -- \"{package.Root}\"") {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
            string fix = $"Our patches under {package.Root} are not back: run `git checkout -- {package.Root}` in Unity/.";
            try {
                using (Process git = Process.Start(start)) {
                    string error = git.StandardError.ReadToEnd();
                    git.WaitForExit();
                    // "did not match any file" only means nothing under the root is patched.
                    if (git.ExitCode != 0 && !error.Contains("did not match")) {
                        Debug.LogError($"git checkout failed after the import of '{package.Title}': {error.Trim()} {fix}");
                        return;
                    }
                }
            } catch (Win32Exception) {
                Debug.LogError($"Unity cannot start git (not on the PATH Unity sees). {fix}");
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log($"Imported Asset Store package '{package.Title}' and restored our patches under {package.Root}.");
        }

        // Anything at the Assets/ root that is neither ours nor listed is ignored by git and reaches no clone. So is
        // anything in Assets/Resources except FMOD's folder.
        private static void WarnUnlisted(Manifest manifest) {
            string[] known = manifest.Packages.Select(p => p.Root).Concat(new[] { OwnRoot, ResourcesRoot }).ToArray();
            foreach (string path in Entries("Assets").Where(path => !known.Contains(path))) {
                Debug.LogWarning($"{path} is not committed: move it into {OwnRoot}, or add its Asset Store package " +
                    $"to {ManifestPath}.");
            }
            if (!Directory.Exists(ResourcesRoot)) {
                return;
            }
            foreach (string path in Entries(ResourcesRoot).Where(path => path != FmodResources)) {
                Debug.LogWarning($"{path} is not committed: only {FmodResources} is. Move it into a Resources folder " +
                    $"under {OwnRoot}.");
            }
        }

        private static IEnumerable<string> Entries(string folder) {
            return Directory.GetFileSystemEntries(folder)
                .Select(entry => entry.Replace('\\', '/'))
                .Where(path => !path.EndsWith(".meta"));
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

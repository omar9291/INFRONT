using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Infront.EditorTools
{
    /// <summary>Prueft echte Dateien, nicht nur die Namen bekannter Anbieter.
    /// Neue Pakete und ausgetauschte Dateien muessen ihren Katalogeintrag erhalten.</summary>
    public static class AssetCreditsAudit
    {
        public static readonly IReadOnlyList<string> ImportedRoots = Array.AsReadOnly(new[]
        {
            "Assets/_Project/Art/Models", "Assets/_Project/Art/Textures",
            "Assets/_Project/Art/Sky", "Assets/_Project/Art/Figures",
            "Assets/_Project/Audio", "Assets/ThirdParty",
        });

        public static IReadOnlyList<string> Validate(string projectRoot,
            IReadOnlyList<AssetCredit> entries, IReadOnlyList<string> roots = null)
        {
            var problems = new List<string>();
            var knownFiles = new Dictionary<string, AssetContentFingerprint>(StringComparer.Ordinal);
            var knownPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Author)
                    || string.IsNullOrWhiteSpace(entry.License) || string.IsNullOrWhiteSpace(entry.ProvenanceStatus)
                    || !IsWebUrl(entry.SourceUrl))
                    problems.Add(entry.Id + ": missing attribution or source URL.");
                if (entry.Evidence.Count == 0)
                    problems.Add(entry.Id + ": no retained evidence.");
                foreach (var path in entry.Paths)
                {
                    if (!IsRelativePath(path)) problems.Add(entry.Id + ": invalid path " + path);
                    else knownPaths.Add(path.TrimEnd('/'));
                }
                foreach (var proof in entry.Evidence)
                {
                    if (string.IsNullOrWhiteSpace(proof.Kind) || !IsWebUrl(proof.SourceUrl))
                        problems.Add(entry.Id + ": evidence kind/source missing.");
                    CheckFingerprint(projectRoot, proof.Path, proof.Sha256, false, problems);
                }
                foreach (var file in entry.Content)
                {
                    if (knownFiles.ContainsKey(file.Path))
                        problems.Add("Duplicate content attribution: " + file.Path);
                    else knownFiles.Add(file.Path, file);
                    if (!entry.Paths.Any(path => file.Path == path || file.Path.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal)))
                        problems.Add(entry.Id + ": content outside declared paths: " + file.Path);
                    CheckFingerprint(projectRoot, file.Path, file.Sha256, entry.Optional, problems);
                }
            }

            foreach (var root in roots ?? ImportedRoots)
            {
                string absoluteRoot = Path.Combine(projectRoot, root);
                if (!Directory.Exists(absoluteRoot)) continue;
                // Auch ein neues, noch leeres Paketverzeichnis braucht einen
                // Eintrag. Unterordner innerhalb eines Pakets prueft die Dateiliste.
                foreach (var folder in Directory.GetDirectories(absoluteRoot))
                {
                    string relative = Relative(projectRoot, folder);
                    if (!knownPaths.Any(path => path == relative || path.StartsWith(relative + "/", StringComparison.Ordinal)))
                        problems.Add("Uncredited asset folder: " + relative);
                }
                foreach (var absoluteFile in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
                {
                    if (!IsAssetFile(absoluteFile)) continue;
                    string relative = Relative(projectRoot, absoluteFile);
                    if (!knownFiles.ContainsKey(relative))
                        problems.Add("Uncredited asset file: " + relative);
                }
            }
            return problems.AsReadOnly();
        }

        static void CheckFingerprint(string root, string path, string expected, bool optional,
            List<string> problems)
        {
            if (!IsRelativePath(path))
            {
                problems.Add("Invalid fingerprint path: " + path);
                return;
            }
            if (expected == null || expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)))
            {
                problems.Add("Missing SHA-256 fingerprint: " + path);
                return;
            }
            string absolute = Path.Combine(root, path);
            if (!File.Exists(absolute))
            {
                if (!optional) problems.Add("Missing credited file: " + path);
                return;
            }
            using var stream = File.OpenRead(absolute);
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                problems.Add("Changed credited content: " + path);
        }

        static bool IsWebUrl(string url)
            => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
               && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp);

        static bool IsRelativePath(string path)
            => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path)
               && !path.Contains('\\') && !path.Split('/').Contains("..") && !path.Contains(':');

        static bool IsAssetFile(string path)
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith(".", StringComparison.Ordinal)) return false;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension != ".meta" && extension != ".txt" && extension != ".md";
        }

        static string Relative(string root, string path)
            => Path.GetRelativePath(root, path).Replace('\\', '/');
    }
}

// =============================================================================
// UpdateAsset.cs — Individual Asset in a LUPDATE Asset Manifest
// =============================================================================
//
// PURPOSE:
//   Represents a single downloadable file (asset) within an AppCast item's
//   asset manifest. When an AppCastItem has a populated Assets list, LUPDATE
//   downloads each file individually rather than a single package.
//
// USE CASE:
//   This enables granular/delta updates — instead of downloading the entire
//   application as one ZIP/MSI every time, the AppCast can specify only the
//   files that changed between versions. Each asset has its own download URL
//   and SHA256 hash for verification.
//
// APPCAST XML FORMAT:
//   <lupdate:assets xmlns:lupdate="http://lupdate.local/appcast">
//     <lupdate:asset path="SPYSCALP.exe"
//                    url="https://server.com/v3.1.0/SPYSCALP.exe"
//                    hash="abc123..." size="2048000" />
//     <lupdate:asset path="lib/Plugin.dll"
//                    url="https://server.com/v3.1.0/lib/Plugin.dll"
//                    hash="def456..." size="128000" />
//   </lupdate:assets>
//
// RELATED FILES:
//   - AppCastItem.cs           — Contains the Assets list property
//   - XMLAppCastGenerator.cs   — Parses <lupdate:assets> from XML
//   - AssetManifestInstaller.cs — Downloads and installs individual assets
//
// =============================================================================

using System.Text.Json.Serialization;

namespace LiveUpdate.Engine
{
    /// <summary>
    /// Represents a single downloadable asset (file) within an AppCast item's
    /// asset manifest. Used for granular/delta updates where individual files
    /// can be downloaded and replaced instead of a monolithic installer.
    /// </summary>
    public class UpdateAsset
    {
        // =====================================================================
        // PROPERTIES
        // =====================================================================

        /// <summary>
        /// Relative path within the target application directory where this
        /// asset should be placed. Uses forward slashes for cross-platform
        /// compatibility; converted to OS-native separators during installation.
        /// 
        /// Examples:
        ///   "SPYSCALP.exe"           — root of install directory
        ///   "lib/SomePlugin.dll"     — in a subdirectory
        ///   "resources/logo.png"     — nested resource file
        /// </summary>
        [JsonPropertyName("path")]
        public string RelativePath { get; set; }

        /// <summary>
        /// Full download URL for this asset file.
        /// 
        /// Example: "https://server.com/v3.1.0/SPYSCALP.exe"
        /// </summary>
        [JsonPropertyName("url")]
        public string DownloadUrl { get; set; }

        /// <summary>
        /// SHA256 hash of the file contents for integrity verification.
        /// Compared against the downloaded file after download completes.
        /// If empty or null, hash verification is skipped (not recommended
        /// for production).
        /// 
        /// Example: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
        /// </summary>
        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        /// <summary>
        /// File size in bytes. Used for progress reporting and pre-download
        /// validation. Set to 0 if unknown.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        // =====================================================================
        // DISPLAY / DEBUG
        // =====================================================================

        /// <summary>
        /// Returns a human-readable representation for logging/debugging.
        /// Example: "SPYSCALP.exe (2048000 bytes)"
        /// </summary>
        public override string ToString()
        {
            return string.Format("{0} ({1} bytes)", RelativePath ?? "(null)", Size);
        }
    }
}

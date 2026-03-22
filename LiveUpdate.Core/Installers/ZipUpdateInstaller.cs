// =============================================================================
// ZipUpdateInstaller.cs — ZIP Bundle Update Installer for LUPDATE
// =============================================================================
//
// PURPOSE:
//   Handles the extraction and installation of updates distributed as ZIP files.
//   This is an alternative to MSI/EXE installers — useful when your application 
//   is distributed as loose binaries (e.g., an EXE + DLLs) rather than a formal 
//   Windows installer package.
//
// HOW IT WORKS:
//   1. Receives a downloaded ZIP file and a target installation directory
//   2. Creates a backup of the current installation (for rollback on failure)
//   3. Extracts the ZIP contents directly into the target directory, overwriting
//   4. On success, cleans up the backup
//   5. On failure, rolls back from the backup
//
// INTEGRATION:
//   Called by SparkleUpdater.RunDownloadedInstaller() when the downloaded file
//   has a .zip extension. The sentinel value "LUPDATE_ZIP_EXTRACT" from
//   GetInstallerCommand() triggers this path.
//
// DEPENDENCIES:
//   - System.IO.Compression.ZipFile (built into .NET Framework 4.5+, no NuGet)
//
// =============================================================================

using System;
using System.IO;
using System.IO.Compression;
using LiveUpdate.Engine.Interfaces;

namespace LiveUpdate.Engine.Installers
{
    /// <summary>
    /// Handles installation of updates packaged as ZIP files.
    /// Extracts the ZIP contents to the target directory, replacing existing files.
    /// Provides automatic backup and rollback on failure.
    /// </summary>
    public static class ZipUpdateInstaller
    {
        // =====================================================================
        // CONSTANTS
        // =====================================================================

        /// <summary>
        /// Sentinel value returned by GetInstallerCommand() to indicate that
        /// the downloaded file is a ZIP and should be handled by this class
        /// rather than via a shell command.
        /// </summary>
        public const string ZipExtractSentinel = "LUPDATE_ZIP_EXTRACT";

        /// <summary>
        /// Suffix appended to the target directory name when creating a backup.
        /// Example: "C:\Program Files\MyApp" → "C:\Program Files\MyApp_lupdate_backup"
        /// </summary>
        private const string BackupSuffix = "_lupdate_backup";

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        /// <summary>
        /// Extracts a ZIP update to the target directory, replacing existing files.
        /// 
        /// The process is:
        ///   1. Validate inputs (ZIP exists, target dir exists)
        ///   2. Create backup of current installation
        ///   3. Extract ZIP to target directory (overwrite mode)
        ///   4. Clean up backup on success
        ///   5. Restore from backup on failure
        /// 
        /// This method is designed to be called from SparkleUpdater.RunDownloadedInstaller()
        /// when the downloaded file has a .zip extension.
        /// </summary>
        /// <param name="zipPath">
        ///   Full path to the downloaded ZIP file (temp directory).
        ///   Example: "C:\Users\user\AppData\Local\Temp\lupdate_abc123.zip"
        /// </param>
        /// <param name="targetDir">
        ///   Directory where the application is installed. All ZIP contents
        ///   will be extracted here, overwriting existing files.
        ///   Example: "C:\Program Files\SPYSCALP"
        /// </param>
        /// <param name="logger">
        ///   Optional ILogger for diagnostic output. Pass null to skip logging.
        /// </param>
        /// <returns>True if extraction succeeded; false if it failed (rollback attempted).</returns>
        public static bool ExtractAndReplace(string zipPath, string targetDir, ILogger logger = null)
        {
            // -----------------------------------------------------------------
            // Step 1: Validate inputs
            // -----------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                logger?.PrintMessage("ZipUpdateInstaller: ERROR — zipPath is null or empty");
                return false;
            }

            if (!File.Exists(zipPath))
            {
                logger?.PrintMessage("ZipUpdateInstaller: ERROR — ZIP file does not exist: {0}", zipPath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetDir))
            {
                logger?.PrintMessage("ZipUpdateInstaller: ERROR — targetDir is null or empty");
                return false;
            }

            if (!Directory.Exists(targetDir))
            {
                logger?.PrintMessage("ZipUpdateInstaller: Target directory does not exist, creating: {0}", targetDir);
                Directory.CreateDirectory(targetDir);
            }

            // -----------------------------------------------------------------
            // Step 2: Create backup of current installation
            // -----------------------------------------------------------------
            string backupDir = targetDir.TrimEnd('\\') + BackupSuffix;
            bool backupCreated = false;

            try
            {
                // Remove stale backup from a previous failed update, if any
                if (Directory.Exists(backupDir))
                {
                    logger?.PrintMessage("ZipUpdateInstaller: Removing stale backup directory: {0}", backupDir);
                    Directory.Delete(backupDir, true);
                }

                logger?.PrintMessage("ZipUpdateInstaller: Creating backup: {0} → {1}", targetDir, backupDir);
                CopyDirectory(targetDir, backupDir);
                backupCreated = true;
                logger?.PrintMessage("ZipUpdateInstaller: Backup created successfully");
            }
            catch (Exception ex)
            {
                logger?.PrintMessage("ZipUpdateInstaller: WARNING — Failed to create backup: {0}", ex.Message);
                logger?.PrintMessage("ZipUpdateInstaller: Proceeding without backup (no rollback possible)");
                // Continue anyway — the user chose to update, and we should try
            }

            // -----------------------------------------------------------------
            // Step 3: Extract ZIP to target directory (overwrite mode)
            // -----------------------------------------------------------------
            try
            {
                logger?.PrintMessage("ZipUpdateInstaller: Extracting ZIP to target directory...");
                logger?.PrintMessage("ZipUpdateInstaller:   ZIP:    {0}", zipPath);
                logger?.PrintMessage("ZipUpdateInstaller:   Target: {0}", targetDir);

                // ZipFile.ExtractToDirectory doesn't support overwrite in .NET Framework 4.x,
                // so we use the ZipArchive approach to extract file-by-file with overwrite.
                ExtractZipWithOverwrite(zipPath, targetDir, logger);

                logger?.PrintMessage("ZipUpdateInstaller: Extraction completed successfully");
            }
            catch (Exception ex)
            {
                logger?.PrintMessage("ZipUpdateInstaller: ERROR — Extraction failed: {0}", ex.Message);

                // Attempt rollback from backup
                if (backupCreated)
                {
                    logger?.PrintMessage("ZipUpdateInstaller: Attempting rollback from backup...");
                    try
                    {
                        CopyDirectory(backupDir, targetDir);
                        logger?.PrintMessage("ZipUpdateInstaller: Rollback successful");
                    }
                    catch (Exception rollbackEx)
                    {
                        logger?.PrintMessage("ZipUpdateInstaller: CRITICAL — Rollback also failed: {0}", rollbackEx.Message);
                    }
                }
                return false;
            }

            // -----------------------------------------------------------------
            // Step 4: Clean up
            // -----------------------------------------------------------------
            try
            {
                // Remove backup directory (no longer needed after successful extraction)
                if (backupCreated && Directory.Exists(backupDir))
                {
                    logger?.PrintMessage("ZipUpdateInstaller: Cleaning up backup directory");
                    Directory.Delete(backupDir, true);
                }

                // Remove the downloaded ZIP temp file
                if (File.Exists(zipPath))
                {
                    logger?.PrintMessage("ZipUpdateInstaller: Cleaning up downloaded ZIP");
                    File.Delete(zipPath);
                }
            }
            catch (Exception cleanupEx)
            {
                // Cleanup failure is non-critical — the update itself succeeded
                logger?.PrintMessage("ZipUpdateInstaller: WARNING — Cleanup failed (non-critical): {0}", cleanupEx.Message);
            }

            logger?.PrintMessage("ZipUpdateInstaller: Update installed successfully");
            return true;
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        /// <summary>
        /// Extracts a ZIP archive to a directory, overwriting existing files.
        /// 
        /// We can't use ZipFile.ExtractToDirectory() here because the .NET Framework
        /// 4.x version does NOT support an overwrite parameter (that was added in
        /// .NET Core 2.0+). Instead, we iterate through entries manually.
        /// </summary>
        private static void ExtractZipWithOverwrite(string zipPath, string targetDir, ILogger logger)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                int fileCount = 0;

                foreach (var entry in archive.Entries)
                {
                    // Skip directory entries (they have empty names ending with /)
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    // Build the full output path, preserving the ZIP's directory structure
                    string destinationPath = Path.Combine(targetDir, entry.FullName);

                    // Ensure the parent directory exists
                    string parentDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    // Extract the file, overwriting if it already exists
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    fileCount++;
                }

                logger?.PrintMessage("ZipUpdateInstaller: Extracted {0} files from ZIP", fileCount.ToString());
            }
        }

        /// <summary>
        /// Recursively copies all files and subdirectories from source to destination.
        /// Used for creating backups and performing rollbacks.
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            // Create the destination directory
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(destDir, fileName);
                File.Copy(filePath, destFilePath, overwrite: true);
            }

            // Recursively copy subdirectories
            foreach (string subDirPath in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDirPath);
                string destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(subDirPath, destSubDir);
            }
        }
    }
}

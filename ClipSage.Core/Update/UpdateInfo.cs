using System;
using System.Text.Json.Serialization;

namespace ClipSage.Core.Update
{
    /// <summary>
    /// Contains information about an available update
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// Gets or sets whether an update is available
        /// </summary>
        [JsonPropertyName("isUpdateAvailable")]
        public bool IsUpdateAvailable { get; set; }

        /// <summary>
        /// Gets or sets the version of the update
        /// </summary>
        [JsonPropertyName("version")]
        public string VersionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets the version as a Version object
        /// </summary>
        [JsonIgnore]
        public Version Version => Version.TryParse(VersionString, out var version) ? version : new Version(1, 0, 0, 0);

        /// <summary>
        /// Gets or sets the release notes for the update
        /// </summary>
        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the filename of the installer
        /// </summary>
        [JsonPropertyName("installerFileName")]
        public string InstallerFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the installer in bytes
        /// </summary>
        [JsonPropertyName("installerSizeBytes")]
        public long InstallerSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the release date of the update
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets whether the update is mandatory
        /// </summary>
        [JsonPropertyName("isMandatory")]
        public bool IsMandatory { get; set; }

        /// <summary>
        /// Gets or sets the original tag name from GitHub
        /// </summary>
        [JsonPropertyName("tagName")]
        public string TagName { get; set; } = string.Empty;
    }
}

using System;
using System.Text.RegularExpressions;
using StardewModdingAPI.Toolkit.Framework;

namespace StardewModdingAPI.Toolkit
{
    /// <summary>A semantic version with an optional release tag.</summary>
    /// <remarks>
    /// The implementation is defined by Semantic Version 2.0 (https://semver.org/), with a few deviations:
    /// - short-form "x.y" versions are supported (equivalent to "x.y.0");
    /// - hyphens are synonymous with dots in prerelease tags and build metadata (like "-unofficial.3-pathoschild");
    /// - and "-unofficial" in prerelease tags is always lower-precedence (e.g. "1.0-beta" is newer than "1.0-unofficial").
    ///
    /// This optionally also supports four-part versions, a non-standard extension used by Stardew Valley on ported platforms to represent platform-specific patches to a ported version, represented as a fourth number in the version string.
    /// </remarks>
    public class SemanticVersion : ISemanticVersion
    {
        /*********
        ** Fields
        *********/
        /// <summary>A regex pattern matching a valid prerelease or build metadata tag.</summary>
        private const string TagPattern = @"(?>[a-z0-9]+[\-\.]?)+";


        /*********
        ** Accessors
        *********/
        /// <summary>The major version incremented for major API changes.</summary>
        public int MajorVersion { get; }

        /// <summary>The minor version incremented for backwards-compatible changes.</summary>
        public int MinorVersion { get; }

        /// <summary>The patch version for backwards-compatible bug fixes.</summary>
        public int PatchVersion { get; }

        /// <summary>The platform release. This is a non-standard semver extension used by Stardew Valley on ported platforms to represent platform-specific patches to a ported version, represented as a fourth number in the version string.</summary>
        public int PlatformRelease { get; }

        /// <summary>An optional prerelease tag.</summary>
        public string PrereleaseTag { get; }

        /// <summary>Optional build metadata. This is ignored when determining version precedence.</summary>
        public string BuildMetadata { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="major">The major version incremented for major API changes.</param>
        /// <param name="minor">The minor version incremented for backwards-compatible changes.</param>
        /// <param name="patch">The patch version for backwards-compatible fixes.</param>
        /// <param name="platformRelease">The platform-specific version (if applicable).</param>
        /// <param name="prereleaseTag">An optional prerelease tag.</param>
        /// <param name="buildMetadata">Optional build metadata. This is ignored when determining version precedence.</param>
        public SemanticVersion(int major, int minor, int patch, int platformRelease = 0, string prereleaseTag = null, string buildMetadata = null)
        {
            this.MajorVersion = major;
            this.MinorVersion = minor;
            this.PatchVersion = patch;
            this.PlatformRelease = platformRelease;
            this.PrereleaseTag = this.GetNormalizedTag(prereleaseTag);
            this.BuildMetadata = this.GetNormalizedTag(buildMetadata);

            this.AssertValid();
        }

        /// <summary>Construct an instance.</summary>
        /// <param name="version">The assembly version.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="version"/> is null.</exception>
        public SemanticVersion(Version version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version), "The input version can't be null.");

            this.MajorVersion = version.Major;
            this.MinorVersion = version.Minor;
            this.PatchVersion = version.Build;

            this.AssertValid();
        }

        /// <summary>Construct an instance.</summary>
        /// <param name="version">The semantic version string.</param>
        /// <param name="allowNonStandard">Whether to recognize non-standard semver extensions.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="version"/> is null.</exception>
        /// <exception cref="FormatException">The <paramref name="version"/> is not a valid semantic version.</exception>
        public SemanticVersion(string version, bool allowNonStandard = false)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version), "The input version string can't be null.");
            if (!SemanticVersionReader.TryParse(version, allowNonStandard, out int major, out int minor, out int patch, out int platformRelease, out string prereleaseTag, out string buildMetadata) || (!allowNonStandard && platformRelease != 0))
                throw new FormatException($"The input '{version}' isn't a valid semantic version.");

            this.MajorVersion = major;
            this.MinorVersion = minor;
            this.PatchVersion = patch;
            this.PlatformRelease = platformRelease;
            this.PrereleaseTag = prereleaseTag;
            this.BuildMetadata = buildMetadata;

            this.AssertValid();
        }

        /// <summary>Get an integer indicating whether this version precedes (less than 0), supersedes (more than 0), or is equivalent to (0) the specified version.</summary>
        /// <param name="other">The version to compare with this instance.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="other"/> value is null.</exception>
        public int CompareTo(ISemanticVersion other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            return this.CompareTo(other.MajorVersion, other.MinorVersion, other.PatchVersion, (other as SemanticVersion)?.PlatformRelease ?? 0, other.PrereleaseTag);
        }

        /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ISemanticVersion other)
        {
            return other != null && this.CompareTo(other) == 0;
        }

        /// <summary>Whether this is a prerelease version.</summary>
        public bool IsPrerelease()
        {
            return !string.IsNullOrWhiteSpace(this.PrereleaseTag);
        }

        /// <summary>Get whether this version is older than the specified version.</summary>
        /// <param name="other">The version to compare with this instance.</param>
        public bool IsOlderThan(ISemanticVersion other)
        {
            return this.CompareTo(other) < 0;
        }

        /// <summary>Get whether this version is older than the specified version.</summary>
        /// <param name="other">The version to compare with this instance.</param>
        /// <exception cref="FormatException">The specified version is not a valid semantic version.</exception>
        public bool IsOlderThan(string other)
        {
            return this.IsOlderThan(new SemanticVersion(other, allowNonStandard: true));
        }

        /// <summary>Get whether this version is newer than the specified version.</summary>
        /// <param name="other">The version to compare with this instance.</param>
        public bool IsNewerThan(ISemanticVersion other)
        {
            return this.CompareTo(other) > 0;
        }

        /// <summary>Get whether this version is newer than the specified version.</summary>
        /// <param name="other">The version to compare with this instance.</param>
        /// <exception cref="FormatException">The specified version is not a valid semantic version.</exception>
        public bool IsNewerThan(string other)
        {
            return this.IsNewerThan(new SemanticVersion(other, allowNonStandard: true));
        }

        /// <summary>Get whether this version is between two specified versions (inclusively).</summary>
        /// <param name="min">The minimum version.</param>
        /// <param name="max">The maximum version.</param>
        public bool IsBetween(ISemanticVersion min, ISemanticVersion max)
        {
            return this.CompareTo(min) >= 0 && this.CompareTo(max) <= 0;
        }

        /// <summary>Get whether this version is between two specified versions (inclusively).</summary>
        /// <param name="min">The minimum version.</param>
        /// <param name="max">The maximum version.</param>
        /// <exception cref="FormatException">One of the specified versions is not a valid semantic version.</exception>
        public bool IsBetween(string min, string max)
        {
            return this.IsBetween(new SemanticVersion(min, allowNonStandard: true), new SemanticVersion(max, allowNonStandard: true));
        }

        /// <summary>Get a string representation of the version.</summary>
        public override string ToString()
        {
            string version = $"{this.MajorVersion}.{this.MinorVersion}.{this.PatchVersion}";
            if (this.PlatformRelease != 0)
                version += $".{this.PlatformRelease}";
            if (this.PrereleaseTag != null)
                version += $"-{this.PrereleaseTag}";
            if (this.BuildMetadata != null)
                version += $"+{this.BuildMetadata}";
            return version;
        }

        /// <summary>Whether the version uses non-standard extensions, like four-part game versions on some platforms.</summary>
        public bool IsNonStandard()
        {
            return this.PlatformRelease != 0;
        }

        /// <summary>Parse a version string without throwing an exception if it fails.</summary>
        /// <param name="version">The version string.</param>
        /// <param name="parsed">The parsed representation.</param>
        /// <returns>Returns whether parsing the version succeeded.</returns>
        public static bool TryParse(string version, out ISemanticVersion parsed)
        {
            return SemanticVersion.TryParse(version, allowNonStandard: false, out parsed);
        }

        /// <summary>Parse a version string without throwing an exception if it fails.</summary>
        /// <param name="version">The version string.</param>
        /// <param name="allowNonStandard">Whether to allow non-standard extensions to semantic versioning.</param>
        /// <param name="parsed">The parsed representation.</param>
        /// <returns>Returns whether parsing the version succeeded.</returns>
        public static bool TryParse(string version, bool allowNonStandard, out ISemanticVersion parsed)
        {
            try
            {
                parsed = new SemanticVersion(version, allowNonStandard);
                return true;
            }
            catch
            {
                parsed = null;
                return false;
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get a normalized prerelease or build tag.</summary>
        /// <param name="tag">The tag to normalize.</param>
        private string GetNormalizedTag(string tag)
        {
            tag = tag?.Trim();
            return !string.IsNullOrWhiteSpace(tag) ? tag : null;
        }

        /// <summary>Get an integer indicating whether this version precedes (less than 0), supersedes (more than 0), or is equivalent to (0) the specified version.</summary>
        /// <param name="otherMajor">The major version to compare with this instance.</param>
        /// <param name="otherMinor">The minor version to compare with this instance.</param>
        /// <param name="otherPatch">The patch version to compare with this instance.</param>
        /// <param name="otherPlatformRelease">The non-standard platform release to compare with this instance.</param>
        /// <param name="otherTag">The prerelease tag to compare with this instance.</param>
        private int CompareTo(int otherMajor, int otherMinor, int otherPatch, int otherPlatformRelease, string otherTag)
        {
            const int same = 0;
            const int curNewer = 1;
            const int curOlder = -1;

            // compare stable versions
            if (this.MajorVersion != otherMajor)
                return this.MajorVersion.CompareTo(otherMajor);
            if (this.MinorVersion != otherMinor)
                return this.MinorVersion.CompareTo(otherMinor);
            if (this.PatchVersion != otherPatch)
                return this.PatchVersion.CompareTo(otherPatch);
            if (this.PlatformRelease != otherPlatformRelease)
                return this.PlatformRelease.CompareTo(otherPlatformRelease);
            if (this.PrereleaseTag == otherTag)
                return same;

            // stable supersedes prerelease
            bool curIsStable = string.IsNullOrWhiteSpace(this.PrereleaseTag);
            bool otherIsStable = string.IsNullOrWhiteSpace(otherTag);
            if (curIsStable)
                return curNewer;
            if (otherIsStable)
                return curOlder;

            // compare two prerelease tag values
            string[] curParts = this.PrereleaseTag.Split('.', '-');
            string[] otherParts = otherTag.Split('.', '-');
            for (int i = 0; i < curParts.Length; i++)
            {
                // longer prerelease tag supersedes if otherwise equal
                if (otherParts.Length <= i)
                    return curNewer;

                // compare if different
                if (curParts[i] != otherParts[i])
                {
                    // unofficial is always lower-precedence
                    if (otherParts[i].Equals("unofficial", StringComparison.OrdinalIgnoreCase))
                        return curNewer;
                    if (curParts[i].Equals("unofficial", StringComparison.OrdinalIgnoreCase))
                        return curOlder;

                    // compare numerically if possible
                    {
                        if (int.TryParse(curParts[i], out int curNum) && int.TryParse(otherParts[i], out int otherNum))
                            return curNum.CompareTo(otherNum);
                    }

                    // else compare lexically
                    return string.Compare(curParts[i], otherParts[i], StringComparison.OrdinalIgnoreCase);
                }
            }

            // fallback (this should never happen)
            return string.Compare(this.ToString(), new SemanticVersion(otherMajor, otherMinor, otherPatch, otherPlatformRelease, otherTag).ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Assert that the current version is valid.</summary>
        private void AssertValid()
        {
            if (this.MajorVersion < 0 || this.MinorVersion < 0 || this.PatchVersion < 0)
                throw new FormatException($"{this} isn't a valid semantic version. The major, minor, and patch numbers can't be negative.");
            if (this.MajorVersion == 0 && this.MinorVersion == 0 && this.PatchVersion == 0)
                throw new FormatException($"{this} isn't a valid semantic version. At least one of the major, minor, and patch numbers must be more than zero.");

            if (this.PrereleaseTag != null)
            {
                if (this.PrereleaseTag.Trim() == "")
                    throw new FormatException($"{this} isn't a valid semantic version. The prerelease tag cannot be a blank string (but may be omitted).");
                if (!Regex.IsMatch(this.PrereleaseTag, $"^{SemanticVersion.TagPattern}$", RegexOptions.IgnoreCase))
                    throw new FormatException($"{this} isn't a valid semantic version. The prerelease tag is invalid.");
            }

            if (this.BuildMetadata != null)
            {
                if (this.BuildMetadata.Trim() == "")
                    throw new FormatException($"{this} isn't a valid semantic version. The build metadata cannot be a blank string (but may be omitted).");
                if (!Regex.IsMatch(this.BuildMetadata, $"^{SemanticVersion.TagPattern}$", RegexOptions.IgnoreCase))
                    throw new FormatException($"{this} isn't a valid semantic version. The build metadata is invalid.");
            }
        }
    }
}

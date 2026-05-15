using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SwComAddin.Services
{
    /// <summary>
    /// SemVer 2.0.0 实现，用于替代 <see cref="System.Version"/>。
    /// 支持 MAJOR.MINOR.PATCH、可选 -PRERELEASE、可选 +BUILDMETADATA。
    /// 例如：1.2.0、1.2.0-rc.1、1.2.0-beta.3+build.42。
    /// 比较规则遵循 https://semver.org/ §11：
    ///   1) 数字段逐位比较；
    ///   2) 有预发布段的版本 &lt; 无预发布段的同主版本号；
    ///   3) 预发布段按 dot 拆分，逐段比较：纯数字按数值比；混合字符串按 ASCII；纯数字 &lt; 字符串；段多者大。
    /// 构建元数据 (+) 不参与比较。
    /// </summary>
    public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        private static readonly Regex SemVerRegex = new Regex(
            @"^v?(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)" +
            @"(?:-(?<pre>(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?" +
            @"(?:\+(?<build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string PreRelease { get; }      // 不含前导 '-'，为空表示正式版
        public string BuildMetadata { get; }   // 不含前导 '+'，不参与比较

        public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

        public SemanticVersion(int major, int minor, int patch, string preRelease = "", string buildMetadata = "")
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch));
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease ?? string.Empty;
            BuildMetadata = buildMetadata ?? string.Empty;
        }

        public static bool TryParse(string? input, out SemanticVersion? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var match = SemVerRegex.Match(input.Trim());
            if (!match.Success) return false;

            try
            {
                version = new SemanticVersion(
                    int.Parse(match.Groups["major"].Value),
                    int.Parse(match.Groups["minor"].Value),
                    int.Parse(match.Groups["patch"].Value),
                    match.Groups["pre"].Success ? match.Groups["pre"].Value : string.Empty,
                    match.Groups["build"].Success ? match.Groups["build"].Value : string.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static SemanticVersion Parse(string input)
        {
            if (!TryParse(input, out var v))
                throw new FormatException($"Invalid SemVer string: '{input}'.");
            return v;
        }

        public int CompareTo(SemanticVersion? other)
        {
            if (other is null) return 1;

            int c = Major.CompareTo(other.Major); if (c != 0) return c;
            c = Minor.CompareTo(other.Minor); if (c != 0) return c;
            c = Patch.CompareTo(other.Patch); if (c != 0) return c;

            // 预发布规则：有预发布段 < 无预发布段
            bool aPre = IsPreRelease;
            bool bPre = other.IsPreRelease;
            if (aPre && !bPre) return -1;
            if (!aPre && bPre) return 1;
            if (!aPre && !bPre) return 0;

            return ComparePreRelease(PreRelease, other.PreRelease);
        }

        private static int ComparePreRelease(string a, string b)
        {
            var aParts = a.Split('.');
            var bParts = b.Split('.');
            int len = Math.Min(aParts.Length, bParts.Length);

            for (int i = 0; i < len; i++)
            {
                bool aIsNum = int.TryParse(aParts[i], out int aNum);
                bool bIsNum = int.TryParse(bParts[i], out int bNum);

                if (aIsNum && bIsNum)
                {
                    int c = aNum.CompareTo(bNum);
                    if (c != 0) return c;
                }
                else if (aIsNum && !bIsNum)
                {
                    // 数字段 < 字符串段
                    return -1;
                }
                else if (!aIsNum && bIsNum)
                {
                    return 1;
                }
                else
                {
                    int c = string.CompareOrdinal(aParts[i], bParts[i]);
                    if (c != 0) return c < 0 ? -1 : 1;
                }
            }

            // 段数多者更大
            return aParts.Length.CompareTo(bParts.Length);
        }

        public bool Equals(SemanticVersion? other) => CompareTo(other) == 0;
        public override bool Equals(object? obj) => obj is SemanticVersion sv && Equals(sv);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Major;
                h = h * 31 + Minor;
                h = h * 31 + Patch;
                h = h * 31 + (PreRelease?.GetHashCode() ?? 0);
                return h;
            }
        }

        public override string ToString()
        {
            var s = $"{Major}.{Minor}.{Patch}";
            if (IsPreRelease) s += "-" + PreRelease;
            if (!string.IsNullOrEmpty(BuildMetadata)) s += "+" + BuildMetadata;
            return s;
        }

        public static bool operator >(SemanticVersion? a, SemanticVersion? b) => Compare(a, b) > 0;
        public static bool operator <(SemanticVersion? a, SemanticVersion? b) => Compare(a, b) < 0;
        public static bool operator >=(SemanticVersion? a, SemanticVersion? b) => Compare(a, b) >= 0;
        public static bool operator <=(SemanticVersion? a, SemanticVersion? b) => Compare(a, b) <= 0;
        public static bool operator ==(SemanticVersion? a, SemanticVersion? b) => Compare(a, b) == 0;
        public static bool operator !=(SemanticVersion? a, SemanticVersion? b) => Compare(a, b) != 0;

        private static int Compare(SemanticVersion? a, SemanticVersion? b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            return a.CompareTo(b);
        }
    }
}

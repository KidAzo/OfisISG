using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Woi.VrBridge.Client.Protocol
{
    /// <summary>
    /// Computes the participant integrity hash used by <c>session.authorize.command</c>.
    /// Must stay byte-for-byte identical to Bridge <c>ParticipantHasher</c>:
    /// SHA-256 of "fullNameNormalized|personnelIdNormalized", hex-encoded lowercase.
    /// Name: trim + collapse whitespace (case preserved).
    /// Personnel ID: trim + collapse whitespace + invariant UPPER.
    /// </summary>
    public static class ParticipantHashUtility
    {
        public static string Compute(string fullName, string personnelId)
        {
            var canonical = NormalizeName(fullName) + "|" + NormalizePersonnelId(personnelId);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool Matches(string fullName, string personnelId, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return false;
            }

            var computed = Compute(fullName, personnelId);
            return string.Equals(computed, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return CollapseWhitespace(value.Trim());
        }

        static string NormalizePersonnelId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return CollapseWhitespace(value.Trim()).ToUpperInvariant();
        }

        static string CollapseWhitespace(string value)
        {
            var builder = new StringBuilder(value.Length);
            var lastWasSpace = false;
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                    }

                    lastWasSpace = true;
                }
                else
                {
                    builder.Append(c);
                    lastWasSpace = false;
                }
            }

            return builder.ToString();
        }
    }
}

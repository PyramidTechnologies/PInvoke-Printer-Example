using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ESCPOSTester
{
    static class Utilities
    {
        /// <summary>
        /// Accepts a wide variety of string input and strips it down to a numbers-only string
        /// that can be safely converted into a bytearray
        /// </summary>
        /// <param name="source">String input</param>
        /// <returns>String ready for </returns>
        /// <exception cref="ArgumentException">Value cannot be converted into an integer</exception>
        public static byte[] StringToByteArray(string source)
        {

            if (string.IsNullOrEmpty(source))
                return new byte[0];

            string scrubbed = source;

            // Remove any hex modifers, upper case Hex only
            scrubbed = source.Replace("0x", "").ToUpper();

            // Strip out non alphanumberics
            scrubbed = Regex.Replace(scrubbed, @"[^a-zA-Z\d]", @" ");

            // Allow only single spacing
            scrubbed = Regex.Replace(scrubbed, @"\s+", " ").Trim();

            // Then go through each byte at a time
            var split = scrubbed.Split(' ');
            byte[] result = new byte[split.Length];

            for (int i = 0; i < split.Length; i++)
            {
                result[i] = byte.Parse(split[i], NumberStyles.AllowHexSpecifier);
            }

            return result;
        }
    }
}

using System;
using System.Text.RegularExpressions;

namespace LocalSmtpRelay.Helpers
{
    internal static class RegexHelper
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(10);

        public static bool IsMatch(string input, string regex)
            => Regex.IsMatch(input, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace EcoEarnServer.Common;

public static class StringHelper
{
    public static string RemovePrefix(string input, string prefix)
    {
        if (input.IsNullOrEmpty() || prefix.IsNullOrEmpty())
        {
            return "";
        }

        return input.StartsWith(prefix) ? input[prefix.Length..] : input;
    }

    public static string DefaultIfEmpty([CanBeNull] this string source, string defaultVal)
    {
        return source.IsNullOrEmpty() ? defaultVal : source;
    }

    public static bool NotNullOrEmpty([CanBeNull] this string source)
    {
        return !source.IsNullOrEmpty();
    }

    public static bool Match([CanBeNull] this string source, string pattern)
    {
        return source.IsNullOrEmpty() ? false : Regex.IsMatch(source, pattern);
    }

    public static double SafeToDouble(this string s, double defaultValue = 0)
    {
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    public static decimal SafeToDecimal(this string s, decimal defaultValue = 0)
    {
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    public static int SafeToInt(this string s, int defaultValue = 0)
    {
        return int.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    public static long SafeToLong(this string s, long defaultValue = 0)
    {
        return long.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    /// replace all {param.key} in string
    public static string ReplaceWithDict(this string input, Dictionary<string, string> replacements,
        bool throwErrorIfNotFound = true, string defaultValue = "")
    {
        foreach (var pair in replacements)
        {
            var key = "{" + pair.Key + "}";
            if (input.Contains(key))
            {
                input = input.Replace(key, pair.Value);
            }
            else if (throwErrorIfNotFound)
            {
                throw new Exception($"Key '{key}' not found in the input string.");
            }
            else
            {
                input = input.Replace(key, defaultValue);
            }
        }

        return input;
    }
}
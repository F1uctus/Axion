﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Axion.Core {
    public static class Utilities {
        private static readonly DateTimeFormatInfo dateTimeFormat =
            new CultureInfo("en-US").DateTimeFormat;

        private const string timedFileNameFormat = "MMM-dd_HH-mm-ss";

        /// <summary>
        ///     Creates a file name from current date and time
        ///     in format: 'yyyy-MMM-dd_HH-mm-ss'.
        /// </summary>
        internal static string ToFileName(this DateTime dt) {
            return dt.ToString(timedFileNameFormat, dateTimeFormat);
        }

        public static void ResolvePath(string path) {
            if (!Path.IsPathRooted(path)) {
                path = Path.GetFullPath(path);
            }

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }

        public static int? ParseInt(string value, int radix) {
            var result = 0;
            foreach (char c in value) {
                int? oneChar = HexValue(c);
                if (oneChar != null && oneChar < radix) {
                    result = result * radix + (int) oneChar;
                }
                else {
                    return null;
                }
            }

            return result;
        }

        public static int? HexValue(char from) {
            if (char.IsDigit(from)) {
                int.TryParse(from.ToString(), out int x);
                return x;
            }

            if ('a' <= from && from <= 'z') {
                return from - 'a' + 10;
            }

            if ('A' <= from && from <= 'Z') {
                return from - 'A' + 10;
            }

            return null;
        }

        public static T[] Union<T>(this IEnumerable<T> collection1, params T[] collection2) {
            return Enumerable.Union(collection1, collection2).ToArray();
        }

        #region Get user input and split it into launch arguments

        /// <summary>
        ///     Splits user command line input to arguments.
        /// </summary>
        /// <returns>Collection of arguments passed into command line.</returns>
        public static IEnumerable<string> SplitLaunchArguments(string input) {
            var inQuotes = false;
            return Split(
                       input,
                       c => {
                           if (c == '\"') {
                               inQuotes = !inQuotes;
                           }

                           return !inQuotes && char.IsWhiteSpace(c);
                       }
                   )
                   .Select(arg => TrimMatchingChars(arg.Trim(), '\"'))
                   .Where(arg => !string.IsNullOrEmpty(arg));
        }

        private static IEnumerable<string> Split(string str, Func<char, bool> controller) {
            var nextPiece = 0;
            for (var c = 0; c < str.Length; c++) {
                if (controller(str[c])) {
                    yield return str.Substring(nextPiece, c - nextPiece);

                    nextPiece = c + 1;
                }
            }

            yield return str.Substring(nextPiece);
        }

        public static string TrimMatchingChars(string input, char c) {
            if (input.Length >= 2 && input[0] == c && input[^1] == c) {
                return input.Substring(1, input.Length - 2);
            }

            return input;
        }

        public static string Multiply(this string source, int multiplier) {
            var sb = new StringBuilder(multiplier * source.Length);
            for (var i = 0; i < multiplier; i++) {
                sb.Append(source);
            }

            return sb.ToString();
        }

        #endregion
    }
}
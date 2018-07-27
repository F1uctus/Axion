﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Axion.Processing {
    public static class LiteralParser {
        // ParseComplex helpers
        private static readonly char[] signs = { '+', '-' };

        public static string ParseString(string text, bool isRaw) {
            return ParseString(text.ToCharArray(), 0, text.Length, isRaw, false);
        }

        public static string ParseString(char[] text, int start, int length, bool isRaw, bool normalizeLineEndings) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }
            if (isRaw && !normalizeLineEndings) {
                return new string(text, start, length);
            }
            StringBuilder buf = null;
            int           i   = start;
            int           l   = start + length;
            while (i < l) {
                char ch = text[i++];
                if (ch == '\\') {
                    if (buf == null) {
                        buf = new StringBuilder(length);
                        buf.Append(text, start, i - start - 1);
                    }
                    if (i >= l) {
                        if (isRaw) {
                            buf.Append('\\');
                            break;
                        }
                        throw new Exception("Trailing \\ in string");
                    }
                    ch = text[i++];
                    int val;
                    if (ch == 'u' || ch == 'U') {
                        int len = ch == 'u' ? 4 : 8;
                        var max = 16;
                        if (!isRaw) {
                            if (TryParseInt(text, i, len, max, out val)) {
                                if (val < 0 || val > 0x10ffff) {
                                    throw new Exception($@"Can't decode bytes in position {i}: illegal Unicode character");
                                }
                                if (val < 0x010000) {
                                    buf.Append((char) val);
                                }
                                else {
                                    buf.Append(char.ConvertFromUtf32(val));
                                }
                                i += len;
                            }
                            else {
                                throw new Exception($@"Can't decode bytes in position {i}: truncated \uXXXX escape");
                            }
                        }
                        else {
                            buf.Append('\\');
                            buf.Append(ch);
                        }
                    }
                    else {
                        if (isRaw) {
                            buf.Append('\\');
                            buf.Append(ch);
                            continue;
                        }
                        switch (ch) {
                            case 'a':
                                buf.Append('\a');
                                continue;
                            case 'b':
                                buf.Append('\b');
                                continue;
                            case 'f':
                                buf.Append('\f');
                                continue;
                            case 'n':
                                buf.Append('\n');
                                continue;
                            case 'r':
                                buf.Append('\r');
                                continue;
                            case 't':
                                buf.Append('\t');
                                continue;
                            case 'v':
                                buf.Append('\v');
                                continue;
                            case '\\':
                                buf.Append('\\');
                                continue;
                            case '\'':
                                buf.Append('\'');
                                continue;
                            case '\"':
                                buf.Append('\"');
                                continue;
                            case '\r':
                                if (i < l && text[i] == '\n') {
                                    i++;
                                }
                                continue;
                            case '\n': continue;
                            case 'N': {
                                if (i < l && text[i] == '{') {
                                    i++;
                                    var namebuf      = new StringBuilder();
                                    var namecomplete = false;
                                    while (i < l) {
                                        char namech = text[i++];
                                        if (namech != '}') {
                                            namebuf.Append(namech);
                                        }
                                        else {
                                            namecomplete = true;
                                            break;
                                        }
                                    }
                                    if (!namecomplete || namebuf.Length == 0) {
                                        throw new Exception(
                                            $@"Can't decode bytes in position {i}: malformed \N character escape"
                                        );
                                    }
                                    try {
                                        var    uval = "";
                                        string udef = namebuf.ToString();
                                        for (var k = 0; k < udef.Length; k++) {
                                            uval += char.ConvertFromUtf32(udef[k]);
                                        }
                                        buf.Append(uval);
                                    }
                                    catch (KeyNotFoundException) {
                                        throw new Exception(
                                            $@"Can't decode bytes in position {i}: unknown Unicode character name"
                                        );
                                    }
                                }
                                else {
                                    throw new Exception($@"Can't decode bytes in position {i}: malformed \N character escape");
                                }
                            }
                                continue;
                            case 'x': //hex
                                if (!TryParseInt(text, i, 2, 16, out val)) {
                                    goto default;
                                }
                                buf.Append((char) val);
                                i += 2;
                                continue;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7': {
                                val = ch - '0';
                                if (i < l && HexValue(text[i], out int onechar) && onechar < 8) {
                                    val = val * 8 + onechar;
                                    i++;
                                    if (i < l && HexValue(text[i], out onechar) && onechar < 8) {
                                        val = val * 8 + onechar;
                                        i++;
                                    }
                                }
                            }
                                buf.Append((char) val);
                                continue;
                            default:
                                buf.Append("\\");
                                buf.Append(ch);
                                continue;
                        }
                    }
                }
                else if (ch == '\r' && normalizeLineEndings) {
                    if (buf == null) {
                        buf = new StringBuilder(length);
                        buf.Append(text, start, i - start - 1);
                    }

                    // normalize line endings
                    if (i < text.Length && text[i] == '\n') {
                        i++;
                    }
                    buf.Append('\n');
                }
                else {
                    buf?.Append(ch);
                }
            }
            if (buf != null) {
                return buf.ToString();
            }
            return new string(text, start, length);
        }

        public static object ParseInteger(string text, int b) {
            Debug.Assert(b != 0);
            if (!ParseInt(text, b, out int iret)) {
                BigInteger ret = ParseBigInteger(text, b);
                if (!int.TryParse(ret.ToString(), out iret)) {
                    return ret;
                }
            }
            return iret;
        }

        public static object ParseIntegerSign(string text, int b, int start = 0) {
            int end = text.Length, saveb = b, savestart = start;
            if (start < 0 || start > end) {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            short sign = 1;
            if (b < 0 || b == 1 || b > 36) {
                throw new Exception("base must be >= 2 and <= 36");
            }
            ParseIntegerStart(text, ref b, ref start, end, ref sign);
            var ret = 0;
            try {
                int saveStart = start;
                for (;;) {
                    if (start >= end) {
                        if (saveStart == start) {
                            throw new Exception($"invalid literal for int() with base {b}: '{text}'");
                        }
                        break;
                    }
                    if (!HexValue(text[start], out int digit)) {
                        break;
                    }
                    if (!(digit < b)) {
                        if (text[start] == 'l' || text[start] == 'L') {
                            break;
                        }
                        throw new Exception($"invalid literal for int() with base {b}: '{text}'");
                    }
                    checked {
                        // include sign here so that System.Int32.MinValue won't overflow
                        ret = ret * b + sign * digit;
                    }
                    start++;
                }
            }
            catch (OverflowException) {
                return ParseBigIntegerSign(text, saveb, savestart);
            }
            ParseIntegerEnd(text, start, end);
            return ret;
        }

        public static BigInteger ParseBigInteger(string text, int b) {
            Debug.Assert(b != 0);
            BigInteger ret = BigInteger.Zero;
            BigInteger m   = BigInteger.One;
            int        i   = text.Length - 1;
            if (text[i] == 'l' || text[i] == 'L') {
                i -= 1;
            }
            var groupMax = 7;
            if (b <= 10) {
                groupMax = 9; // 2 147 483 647
            }
            while (i >= 0) {
                // extract digits in a batch
                var  smallMultiplier = 1;
                uint uval            = 0;
                for (var j = 0; j < groupMax && i >= 0; j++) {
                    uval            =  (uint) (CharValue(text[i--], b) * smallMultiplier + uval);
                    smallMultiplier *= b;
                }

                // this is more generous than needed
                ret += m * uval;
                if (i >= 0) {
                    m = m * smallMultiplier;
                }
            }
            return ret;
        }

        public static BigInteger ParseBigIntegerSign(string text, int b, int start = 0) {
            int end = text.Length;
            if (start < 0 || start > end) {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            short sign = 1;
            if (b < 0 || b == 1 || b > 36) {
                throw new Exception("base must be >= 2 and <= 36");
            }
            ParseIntegerStart(text, ref b, ref start, end, ref sign);
            BigInteger ret       = BigInteger.Zero;
            int        saveStart = start;
            for (;;) {
                if (start >= end) {
                    if (start == saveStart) {
                        throw new Exception($"invalid literal for int() with base {b}: {text}");
                    }
                    break;
                }
                if (!HexValue(text[start], out int digit)) {
                    break;
                }
                if (!(digit < b)) {
                    if (text[start] == 'l' || text[start] == 'L') {
                        break;
                    }
                    throw new Exception($"invalid literal for int() with base {b}: {text}");
                }
                ret = ret * b + digit;
                start++;
            }
            if (start < end && (text[start] == 'l' || text[start] == 'L')) {
                start++;
            }
            ParseIntegerEnd(text, start, end);
            return sign < 0 ? -ret : ret;
        }

        public static double ParseFloat(string text) {
            try {
                //
                // Strings that end with '\0' is the specific case that CLR libraries allow,
                // however Python doesn't. Since we use CLR floating point number parser,
                // we must check explicitly for the strings that end with '\0'
                //
                if (!string.IsNullOrEmpty(text) && text[text.Length - 1] == '\0') {
                    throw new Exception("null byte in float literal");
                }
                return ParseFloatNoCatch(text);
            }
            catch (OverflowException) {
                return text.TrimStart().StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
            }
        }

        public static Complex ParseComplex(string s) {
            // remove no-meaning spaces and convert to lowercase
            string text = s.Trim().ToLower();
            if (string.IsNullOrEmpty(text) || text.IndexOf(' ') != -1) {
                throw ExnMalformed();
            }

            // remove 1 layer of parens
            if (text.StartsWith("(") && text.EndsWith(")")) {
                text = text.Substring(1, text.Length - 2);
            }
            try {
                int    len = text.Length;
                string real, imag;
                if (text[len - 1] == 'j') {
                    // last sign delimits real and imaginary...
                    int signPos = text.LastIndexOfAny(signs);
                    // ... unless it's after 'e', so we bypass up to 2 of those here
                    for (var i = 0; signPos > 0 && text[signPos - 1] == 'e'; i++) {
                        if (i == 2) {
                            // too many 'e's
                            throw ExnMalformed();
                        }
                        signPos = text.Substring(0, signPos - 1).LastIndexOfAny(signs);
                    }

                    // no real component
                    if (signPos < 0) {
                        return new Complex(0.0, len == 1 ? 1 : ParseFloatNoCatch(text.Substring(0, len - 1)));
                    }
                    real = text.Substring(0,       signPos);
                    imag = text.Substring(signPos, len - signPos - 1);
                    if (imag.Length == 1) {
                        imag += "1"; // convert +/- to +1/-1
                    }
                }
                else {
                    // 'j' delimits real and imaginary
                    string[] splitText = text.Split('j');

                    // no imaginary component
                    if (splitText.Length == 1) {
                        return new Complex(ParseFloatNoCatch(text), 0.0);
                    }

                    // there should only be one j
                    if (splitText.Length != 2) {
                        throw ExnMalformed();
                    }
                    real = splitText[1];
                    imag = splitText[0];

                    // a sign must follow the 'j'
                    if (!(real.StartsWith("+") || real.StartsWith("-"))) {
                        throw ExnMalformed();
                    }
                }
                return new Complex(string.IsNullOrEmpty(real) ? 0 : ParseFloatNoCatch(real), ParseFloatNoCatch(imag));
            }
            catch (OverflowException) {
                throw new Exception("complex() literal too large to convert");
            }
            catch {
                throw ExnMalformed();
            }
        }

        public static Complex ParseImaginary(string text) {
            try {
                return new Complex(
                    0.0,
                    double.Parse(
                        text.Substring(0, text.Length - 1),
                        CultureInfo.InvariantCulture.NumberFormat
                    )
                );
            }
            catch (OverflowException) {
                return new Complex(0, double.PositiveInfinity);
            }
        }

        internal static List<byte> ParseBytes(char[] text, int start, int length, bool isRaw,
                                              bool   normalizeLineEndings) {
            Debug.Assert(text != null);
            var buf = new List<byte>(length);
            int i   = start;
            int l   = start + length;
            while (i < l) {
                char ch = text[i++];
                if (!isRaw && ch == '\\') {
                    if (i >= l) {
                        throw new Exception("Trailing \\ in string");
                    }
                    ch = text[i++];
                    int val;
                    switch (ch) {
                        case 'a':
                            buf.Add((byte) '\a');
                            continue;
                        case 'b':
                            buf.Add((byte) '\b');
                            continue;
                        case 'f':
                            buf.Add((byte) '\f');
                            continue;
                        case 'n':
                            buf.Add((byte) '\n');
                            continue;
                        case 'r':
                            buf.Add((byte) '\r');
                            continue;
                        case 't':
                            buf.Add((byte) '\t');
                            continue;
                        case 'v':
                            buf.Add((byte) '\v');
                            continue;
                        case '\\':
                            buf.Add((byte) '\\');
                            continue;
                        case '\'':
                            buf.Add((byte) '\'');
                            continue;
                        case '\"':
                            buf.Add((byte) '\"');
                            continue;
                        case '\r':
                            if (i < l && text[i] == '\n') {
                                i++;
                            }
                            continue;
                        case '\n': continue;
                        case 'x': //hex
                            if (!TryParseInt(text, i, 2, 16, out val)) {
                                goto default;
                            }
                            buf.Add((byte) val);
                            i += 2;
                            continue;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7': {
                            val = ch - '0';
                            if (i < l && HexValue(text[i], out int onechar) && onechar < 8) {
                                val = val * 8 + onechar;
                                i++;
                                if (i < l && HexValue(text[i], out onechar) && onechar < 8) {
                                    val = val * 8 + onechar;
                                    i++;
                                }
                            }
                        }
                            buf.Add((byte) val);
                            continue;
                        default:
                            buf.Add((byte) '\\');
                            buf.Add((byte) ch);
                            continue;
                    }
                }
                if (ch == '\r' && normalizeLineEndings) {
                    // normalize line endings
                    if (i < text.Length && text[i] == '\n') {
                        i++;
                    }
                    buf.Add((byte) '\n');
                }
                else {
                    buf.Add((byte) ch);
                }
            }
            return buf;
        }

        private static bool HexValue(char ch, out int value) {
            switch (ch) {
                case '0':
                case '\x660':
                    value = 0;
                    break;
                case '1':
                case '\x661':
                    value = 1;
                    break;
                case '2':
                case '\x662':
                    value = 2;
                    break;
                case '3':
                case '\x663':
                    value = 3;
                    break;
                case '4':
                case '\x664':
                    value = 4;
                    break;
                case '5':
                case '\x665':
                    value = 5;
                    break;
                case '6':
                case '\x666':
                    value = 6;
                    break;
                case '7':
                case '\x667':
                    value = 7;
                    break;
                case '8':
                case '\x668':
                    value = 8;
                    break;
                case '9':
                case '\x669':
                    value = 9;
                    break;
                default:
                    if (ch >= 'a' && ch <= 'z') {
                        value = ch - 'a' + 10;
                    }
                    else if (ch >= 'A' && ch <= 'Z') {
                        value = ch - 'A' + 10;
                    }
                    else {
                        value = -1;
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static int HexValue(char ch) {
            if (!HexValue(ch, out int value)) {
                throw new Exception("bad char for integer value: " + ch);
            }
            return value;
        }

        private static int CharValue(char ch, int b) {
            int val = HexValue(ch);
            if (val >= b) {
                throw new Exception($"bad char for the integer value: '{ch}' (base {b})");
            }
            return val;
        }

        private static bool ParseInt(string text, int b, out int ret) {
            ret = 0;
            long m = 1;
            for (int i = text.Length - 1; i >= 0; i--) {
                // avoid the exception here.  Not only is throwing it expensive,
                // but loading the resources for it is also expensive 
                long lret = ret + m * CharValue(text[i], b);
                if (int.MinValue <= lret && lret <= int.MaxValue) {
                    ret = (int) lret;
                }
                else {
                    return false;
                }
                m *= b;
                if (int.MinValue > m || m > int.MaxValue) {
                    return false;
                }
            }
            return true;
        }

        private static bool TryParseInt(char[] text, int start, int length, int b, out int value) {
            value = 0;
            if (start + length > text.Length) {
                return false;
            }
            for (int i = start, end = start + length; i < end; i++) {
                if (HexValue(text[i], out int onechar) && onechar < b) {
                    value = value * b + onechar;
                }
                else {
                    return false;
                }
            }
            return true;
        }

        private static void ParseIntegerStart(string text, ref int b, ref int start, int end, ref short sign) {
            //  Skip whitespace
            while (start < end && char.IsWhiteSpace(text, start)) {
                start++;
            }
            //  Sign?
            if (start < end) {
                switch (text[start]) {
                    case '-':
                        sign = -1;
                        goto case '+';
                    case '+':
                        start++;
                        break;
                }
            }
            //  Skip whitespace
            while (start < end && char.IsWhiteSpace(text, start)) {
                start++;
            }

            //  Determine base
            if (b == 0) {
                if (start < end && text[start] == '0') {
                    // Hex, oct, or bin
                    if (++start < end) {
                        switch (text[start]) {
                            case 'x':
                            case 'X':
                                start++;
                                b = 16;
                                break;
                            case 'o':
                            case 'O':
                                b = 8;
                                start++;
                                break;
                            case 'b':
                            case 'B':
                                start++;
                                b = 2;
                                break;
                        }
                    }
                    if (b == 0) {
                        // Keep the leading zero
                        start--;
                        b = 8;
                    }
                }
                else {
                    b = 10;
                }
            }
        }

        private static void ParseIntegerEnd(string text, int start, int end) {
            //  Skip whitespace
            while (start < end && char.IsWhiteSpace(text, start)) {
                start++;
            }
            if (start < end) {
                throw new Exception("invalid integer number literal");
            }
        }

        private static double ParseFloatNoCatch(string text) {
            string s = ReplaceUnicodeDigits(text);
            switch (s.ToLower(CultureInfo.InvariantCulture).TrimStart()) {
                case "nan":
                case "+nan":
                case "-nan": return double.NaN;
                case "inf":
                case "+inf": return double.PositiveInfinity;
                case "-inf": return double.NegativeInfinity;
                default:
                    // pass NumberStyles to disallow ,'s in float strings.
                    double res = double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
                    return res == 0.0 && text.TrimStart().StartsWith("-") ? 0.0 : res;
            }
        }

        private static string ReplaceUnicodeDigits(string text) {
            StringBuilder replacement = null;
            for (var i = 0; i < text.Length; i++) {
                if (text[i] >= '\x660' && text[i] <= '\x669') {
                    if (replacement == null) {
                        replacement = new StringBuilder(text);
                    }
                    replacement[i] = (char) (text[i] - '\x660' + '0');
                }
            }
            if (replacement != null) {
                text = replacement.ToString();
            }
            return text;
        }

        private static Exception ExnMalformed() {
            return new Exception("complex() arg is a malformed string");
        }
    }
}
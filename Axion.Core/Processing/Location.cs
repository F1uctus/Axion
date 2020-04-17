using System;

namespace Axion.Core.Processing {
    /// <summary>
    ///     (line, column) position of code in source (0-based).
    ///     Convertible to (int, int) 2-tuple.
    /// </summary>
    public readonly struct Location : IEquatable<Location> {
        public readonly int Line;
        public readonly int Column;

        internal Location(int line, int column) {
            Line   = line;
            Column = column;
        }

        public Location Add(int line, int column) {
            return new Location(Line + line, Column + column);
        }

        public override string ToString() {
            return $"{Line + 1}, {Column + 1}";
        }

        public bool Equals(Location other) {
            return Line   == other.Line
                && Column == other.Column;
        }

        public override bool Equals(object? obj) {
            return obj is Location other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return (Line * 397) ^ Column;
            }
        }

        public static bool operator ==(Location left, Location right) {
            return left.Equals(right);
        }

        public static bool operator !=(Location left, Location right) {
            return !left.Equals(right);
        }

        public static implicit operator Location((int, int) position) {
            (int line, int column) = position;
            return new Location(line, column);
        }
    }
}
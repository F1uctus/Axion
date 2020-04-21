using System.Web;
using Axion.Core.Source;
using Newtonsoft.Json;
using static Axion.Core.Processing.Lexical.Tokens.TokenType;

namespace Axion.Core.Processing.Lexical.Tokens {
    public class Token : Node {
        [JsonProperty(Order = 1)]
        public TokenType Type { get; set; }

        [JsonProperty(Order = 2)]
        public string Value { get; protected set; }

        [JsonProperty(Order = 3)]
        public string Content { get; protected set; }

        [JsonProperty(Order = 4)]
        public string EndingWhite { get; set; }

        public Token(
            Unit      source,
            TokenType type        = None,
            string    value       = "",
            string?   content     = null,
            string    endingWhite = "",
            Location  start       = default,
            Location  end         = default
        ) : base(source, start, end) {
            Type        = type;
            Value       = value;
            Content     = content ?? value;
            EndingWhite = endingWhite;
        }

        public bool Is(params TokenType[] types) {
            if (types.Length == 0) {
                return true;
            }

            for (var i = 0; i < types.Length; i++) {
                if (Type == types[i]) {
                    return true;
                }
            }

            return false;
        }

        internal void MarkStart(Location start) {
            Start = start;
        }

        internal void MarkEnd(Location end) {
            End = end;
        }

        public override string ToString() {
            return Type
                 + " :: "
                 + HttpUtility.JavaScriptStringEncode(Value)
                 + " :: "
                 + base.ToString();
        }
    }
}

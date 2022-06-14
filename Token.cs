using System;

namespace Arbiter {
    public enum TokenType {
        Number     = 0,
        String     = 1,
        Identifier = 2,
        Operator   = 3,
    }

    public class Token {
        public TokenType Type;
        public string Data;
        public string Source;
        public int Line;
        public int Column;

        public Token (TokenType type, string data, string source) {
            Type = type;
            Data = data;
            Source = source;
        } 

        public Token (TokenType type, char data, string source) {
            Type = type;
            Data = data.ToString();
            Source = source;
        } 
    }

    [System.Serializable]
    public class UnexpectedTokenException : System.Exception {
        public UnexpectedTokenException() { }
        public UnexpectedTokenException(string message) : base(message) { }
        public UnexpectedTokenException(string message, System.Exception inner) : base(message, inner) { }
        public UnexpectedTokenException(Token token) : base($"Unexpected {token.Type.ToString()} '{token.Data}' token in {token.Source} at line {token.Line}") {}

        protected UnexpectedTokenException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace Arbiter;

public class TokenStream
{
    public bool EndOfStream { get => Position == Tokens.Length; }

    private Token[] Tokens;
    private int Position;

    public TokenStream(List<Token> tokens)
    {
        Tokens = tokens.ToArray();
        Position = 0;
    }

    public static TokenStream Tokenize(string source, Stream stream)
    {
        var tokens = new List<Token>();

        using (var reader = new StreamReader(stream))
            while (!reader.EndOfStream)
            {
                char c = (char)reader.Read();
                if (char.IsWhiteSpace(c))
                    continue;

                if (c == '#')
                {
                    reader.ReadLine();
                }
                else if (c == '"')
                {
                    string str = "";

                    while ((char)reader.Peek() != '"')
                    {
                        c = (char)reader.Read();
                        if (c == '\\')
                            c = (char)reader.Read();

                        str += c;
                    }

                    reader.Read();
                    tokens.Add(new Token(TokenType.String, str, source));
                }
                else if (char.IsPunctuation(c))
                {
                    tokens.Add(new Token(TokenType.Operator, c, source));
                }
                else if (char.IsDigit(c))
                {
                    string digit = "" + c;

                    while (char.IsDigit((char)reader.Peek()))
                    {
                        digit += (char)reader.Read();
                    }

                    tokens.Add(new Token(TokenType.Number, digit, source));
                }
                else
                {
                    string identifier = "" + c;

                    while (!char.IsWhiteSpace((char)reader.Peek()))
                    {
                        identifier += (char)reader.Read();
                    }

                    tokens.Add(new Token(TokenType.Identifier, identifier, source));
                }
            }

        return new TokenStream(tokens);
    }

    public bool AcceptNumber(out int value)
    {
        var token = Peek();

        if (token == null || token.Type != TokenType.Number)
        {
            value = 0;
            return false;
        }

        Pop();
        value = int.Parse(token.Data);

        return true;
    }

    public bool AcceptString(out string? value)
    {
        var token = Peek();

        if (token == null || token.Type != TokenType.String)
        {
            value = null;
            return false;
        }

        Pop();
        value = token.Data;

        return true;
    }

    public bool AcceptIdentifier(out string? value)
    {
        var token = Peek();

        if (token == null || token.Type != TokenType.Identifier)
        {
            value = null;
            return false;
        }

        Pop();
        value = token.Data;

        return true;
    }

    public bool AcceptOperator(out string? value)
    {
        var token = Peek();

        if (token == null || token.Type != TokenType.Operator)
        {
            value = null;
            return false;
        }

        Pop();
        value = token.Data;

        return true;
    }

    public bool AcceptOperator(string value)
    {
        var token = Peek();

        if (token == null || token.Type != TokenType.Operator)
            return false;

        Pop();
        return value == token.Data;
    }

    public void ExpectString(out string value)
    {
        if (!AcceptString(out value))
            throw new UnexpectedTokenException(Peek());
    }

    public string ExpectString()
    {
        string value = null;

        if (!AcceptString(out value))
            throw new UnexpectedTokenException(Peek());

        return value;
    }

    public void ExpectIdentifier(out string value)
    {
        if (!AcceptIdentifier(out value))
            throw new UnexpectedTokenException(Peek());
    }

    public void ExpectIdentifier(string identifier)
    {
        string value = null;
        Token token = Peek();

        if (!AcceptIdentifier(out value))
            throw new UnexpectedTokenException(token);

        if (value != identifier)
            throw new UnexpectedTokenException(token);
    }

    public void ExpectOperator(string op)
    {
        string value = null;
        Token token = Peek();

        if (!AcceptOperator(out value))
            throw new UnexpectedTokenException(token);

        if (value != op)
            throw new UnexpectedTokenException(token);
    }

    public Token? Peek()
    {
        if (Position >= Tokens.Length)
            return null;

        return Tokens[Position];
    }

    private void Pop()
    {
        Position++;
    }
}
using System;
using System.Text;

namespace FourSer.Gen.Helpers;

public class IndentedStringBuilder
{
    private readonly StringBuilder _stringBuilder;
    private int _indentationLevel;
    private bool _isAtStartOfLine = true;
    private const string Indentation = "    ";

    public IndentedStringBuilder()
    {
        _stringBuilder = new StringBuilder();
    }

    public void WriteLine(string line)
    {
        Write(line);
        _stringBuilder.AppendLine();
        _isAtStartOfLine = true;
    }

    public void WriteLine()
    {
        _stringBuilder.AppendLine();
        _isAtStartOfLine = true;
    }

    public void Write(string text)
    {
        if (_isAtStartOfLine)
        {
            AppendIndentation();
            _isAtStartOfLine = false;
        }
        _stringBuilder.Append(text);
    }

    public IDisposable BeginBlock()
    {
        WriteLine("{");
        _indentationLevel++;
        return new Block(this);
    }

    private void EndBlock()
    {
        _indentationLevel--;
        WriteLine("}");
    }

    private void AppendIndentation()
    {
        for (int i = 0; i < _indentationLevel; i++)
        {
            _stringBuilder.Append(Indentation);
        }
    }

    public override string ToString()
    {
        return _stringBuilder.ToString();
    }

    private class Block : IDisposable
    {
        private readonly IndentedStringBuilder _builder;

        public Block(IndentedStringBuilder builder)
        {
            _builder = builder;
        }

        public void Dispose()
        {
            _builder.EndBlock();
        }
    }
}

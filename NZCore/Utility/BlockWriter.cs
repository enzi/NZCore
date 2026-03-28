// <copyright project="NZCore" file="BlockWriter.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Text;

namespace NZCore.Editor
{
    public class BlockWriter
    {
        public readonly StringBuilder StringBuilder;
        private int _indentLevel;

        private const string Indent = "\t";

        public BlockWriter(int indent = 0)
        {
            StringBuilder = new StringBuilder();
            _indentLevel = indent;
        }

        public BlockWriter(StringBuilder stringBuilder, int indent = 0)
        {
            StringBuilder = stringBuilder;
            _indentLevel = indent;
        }

        public void BeginBlock()
        {
            WriteIndent();
            StringBuilder.Append("{\n");
            _indentLevel++;
        }

        public void BeginBlock(string text)
        {
            WriteIndent();
            StringBuilder.Append($"{{{text}\n");
            _indentLevel++;
        }

        public void EndBlock()
        {
            _indentLevel--;
            WriteIndent();
            StringBuilder.Append("}\n");
        }

        public void EndBlock(string text)
        {
            _indentLevel--;
            WriteIndent();
            StringBuilder.Append($"}}{text}\n");
        }

        public void AppendLine()
        {
            StringBuilder.Append('\n');
        }

        public void AppendLine(string text)
        {
            WriteIndent();

            StringBuilder.Append(text);
            StringBuilder.Append('\n');
        }

        public void AppendLine(int customIndent, string text)
        {
            WriteIndent();

            if (customIndent > 0)
            {
                WriteIndent(customIndent);
            }

            StringBuilder.Append(text);
            StringBuilder.Append('\n');
        }

        public void Append(string text)
        {
            StringBuilder.Append(text);
        }

        public StringBuilder Append(StringBuilder sb)
        {
            StringBuilder.Append(sb);
            return StringBuilder;
        }

        public void WriteIndent()
        {
            WriteIndent(_indentLevel);
        }

        public void WriteIndent(int indentAmount)
        {
            for (var i = 0; i < indentAmount; i++)
            {
                StringBuilder.Append($"{Indent}");
            }
        }
    }
}
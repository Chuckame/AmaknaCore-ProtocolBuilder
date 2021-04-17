using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProtocolBuilder.Parsing.Elements
{
    public class ControlStatementEnd : IStatement
    {
        public override string ToString()
        {
            return $"{nameof(ControlStatementEnd)}()";
        }
    }
    
    public enum ControlType
    {
        If,
        Else,
        Elseif,
        While,
        Break,
        Return
    }

    public class ControlStatement : IStatement
    {
        private static Regex Pattern = new Regex(@"\b(?<type>if|else if|else|while|break|return);?\s*(?<condition>\(?\s*[^;]*\s*\)?)?", RegexOptions.Multiline);

        public ControlType ControlType
        {
            get;
            set;
        }

        public string Condition
        {
            get;
            set;
        }

        public override string ToString()
        {
            return $"{nameof(ControlStatement)}({nameof(ControlType)}: {ControlType}, {nameof(Condition)}: {Condition})";
        }

        public static ControlStatement? TryParse(string line) {
            var match = Pattern.Match(line);
            if (match.Success)
                return Parse(match);
            return null;
        }

        private static ControlStatement Parse(Match match)
        {
            var result = new ControlStatement();

            if (match.Groups["type"].Value != "")
                result.ControlType = (ControlType) Enum.Parse(typeof (ControlType), match.Groups["type"].Value.Replace(" ", ""), true);

            if (match.Groups["condition"].Value != "")
            {
                result.Condition = match.Groups["condition"].Value.Split(')').First().Split('(').Last().Trim();
            }

            return result;
        }
    }
}

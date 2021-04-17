using System.Linq;
using System.Text.RegularExpressions;

namespace ProtocolBuilder.Parsing.Elements
{
    /// <summary>
    /// </summary>
    /// <remarks>
    ///   Only work if the value is a variable or a hard coded value
    /// </remarks>
    public class AssignationStatement : IStatement
    {
        private static Regex Pattern = new Regex(@"^(?<var>var\s)?(?<variable>[^:=]+):?(?:[^=]+)?\s*=\s*(?<value>[^;]+);$", RegexOptions.Multiline);
        private static Regex VariablePattern = new Regex(@"^(?<target>[^\.]+\.)*(?<name>.+)", RegexOptions.Multiline);

        public string Name
        {
            get;
            set;
        }

        public string Target
        {
            get;
            set;
        }

        public string Value
        {
            get;
            set;
        }

        public override string ToString()
        {
            return $"{nameof(AssignationStatement)}({nameof(Name)}: {Name}, {nameof(Target)}: {Target}, {nameof(Value)}: {Value})";
        }

        public static AssignationStatement? TryParse(string line) {
            var match = Pattern.Match(line);
            if (match.Success)
                return Parse(match);
            return null;
        }

        private static AssignationStatement Parse(Match match)
        {
            var result = new AssignationStatement();

            if (match.Groups["variable"].Value != "")
            {
                Match variableMatch = VariablePattern.Match(match.Groups["variable"].Value);

                result.Target = variableMatch.Groups["target"].Value.Trim().TrimEnd('.');
                result.Name = variableMatch.Groups["name"].Value.Trim();
            }

            result.Value = match.Groups["value"].Value.Trim();

            if (result.Value.Contains("<") && !result.Value.Contains("\""))
            {
                string generictype = result.Value.Split('<').Last().Split('>').First().Split('.').Last();
                string defaulttype = result.Value.Split('<').Last().Split('>').First();

                result.Value = result.Value.Replace(defaulttype, generictype);
            }

            return result;
        }
    }
}

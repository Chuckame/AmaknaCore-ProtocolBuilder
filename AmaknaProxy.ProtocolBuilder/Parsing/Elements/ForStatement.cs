using System.Text.RegularExpressions;

namespace ProtocolBuilder.Parsing.Elements
{
	public class ForStatement : IStatement
	{
		private static Regex Pattern = new Regex(@"for\s*\(var\s+\w+\s*(?::\w+\s*)?=\s*0\s*;\s*\w+\s*\<\s*([\w.]+)\s*;\s*\w+\+\+\s*\)", RegexOptions.Multiline);
		
		public string LimitVarName { get; set; }

		public override string ToString()
		{
			return $"{nameof(ForStatement)}({nameof(LimitVarName)}: {LimitVarName})";
		}

		public static ForStatement? TryParse(string line) {
			var match = Pattern.Match(line);
			if (match.Success)
				return Parse(match);
			return null;
		}

		private static ForStatement Parse(Match match)
		{
			return new ForStatement()
			{
				LimitVarName = match.Groups[1].Value
			};
		}
	}
}

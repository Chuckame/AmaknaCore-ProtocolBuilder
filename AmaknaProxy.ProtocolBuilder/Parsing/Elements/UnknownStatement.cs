namespace ProtocolBuilder.Parsing.Elements
{
    public class UnknownStatement : IStatement
    {
		public string Value
		{
			get;
			set;
		}

		public override string ToString()
		{
			return $"{nameof(UnknownStatement)}({Value})";
		}
    }
}

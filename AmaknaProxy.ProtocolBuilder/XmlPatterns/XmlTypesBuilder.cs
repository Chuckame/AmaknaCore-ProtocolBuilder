using ProtocolBuilder.Parsing;

namespace ProtocolBuilder.XmlPatterns
{
    public class XmlTypesBuilder : XmlPatternBuilder<XmlType>
    {
        public XmlTypesBuilder(Parser parser) : base(parser, Program.Configuration.TypesProfile)
        {
        }
    }
}

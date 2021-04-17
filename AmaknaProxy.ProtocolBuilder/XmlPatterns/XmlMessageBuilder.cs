using ProtocolBuilder.Parsing;

namespace ProtocolBuilder.XmlPatterns
{
    public class XmlMessageBuilder : XmlPatternBuilder<XmlMessage>
    {
        public XmlMessageBuilder(Parser parser) : base(parser, Program.Configuration.MessagesProfile)
        {
        }
    }
}

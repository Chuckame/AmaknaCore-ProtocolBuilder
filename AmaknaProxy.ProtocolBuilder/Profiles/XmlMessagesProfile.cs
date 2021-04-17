using ProtocolBuilder.Parsing;
using ProtocolBuilder.XmlPatterns;

namespace ProtocolBuilder.Profiles
{
    public class XmlMessagesProfile : XmlProfile<XmlMessage>
    {
        public XmlMessagesProfile()
        {
            BeforeParsingReplacementRules =
                new SerializableDictionary<string, string>
                    {
                        {@"this\.", string.Empty},
                        {@"flash\.geom\.", string.Empty},
                        {@"new Vector\.<([\d\w]+)>", "new List<$1>()"},
                        {@"Vector\.", "List"},
                    };
        }

        protected override XmlPatternBuilder<XmlMessage> newXmlPatternBuilder(Parser parser)
        {
            return new XmlMessageBuilder(parser);
        }
    }
}

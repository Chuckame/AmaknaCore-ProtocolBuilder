using ProtocolBuilder.Parsing;
using ProtocolBuilder.XmlPatterns;

namespace ProtocolBuilder.Profiles
{
    public class XmlTypesProfile : XmlProfile<XmlType> 
    {
        public XmlTypesProfile()
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

        protected override XmlPatternBuilder<XmlType> newXmlPatternBuilder(Parser parser)
        {
            return new XmlTypesBuilder(parser);
        }
    }
}

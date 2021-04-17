using ProtocolBuilder.Parsing;
using ProtocolBuilder.Templates;

namespace ProtocolBuilder.Profiles
{
    public class DatacenterProfile : TemplateProfile
    {
        public DatacenterProfile()
        {
            BeforeParsingReplacementRules =
                new SerializableDictionary<string, string>
                    {
                        {@"Vector\.([\w_\d]+) = new ([\w_\d]+)();", "$1 = new List<$2>();"},
                        // convert "Vector." to List (C#) (and its props)
                        {@"new Vector\.<([\d\w]+)>\((\d+), (true|false)\)", "new List<$1>($2)"},
                        {@"new Vector\.<([\d\w]+)>", "new List<$1>()"},
                        {@"(__AS3__\.vec\.)?Vector\.", "List"},
                        {@"\.length", @".Count"},
                        {@"\bNumber", @"double"},
                        // convert Number to float

                        {@"static const", "const"},
                        // manual fix
                        {
                            @"const OPERATORS_LIST:Array\s?=\s?\[([^\]]+)\]",
                            "readonly static OPERATORS_LIST:Array=new string[]{$1}"
                            },
                        //another hack
                        {@"(protected|private) var _rawZone", "public var rawZone"},
                        {@"(protected|private) var _zoneSize = 4.29497e+009", "public var zoneSize"},
                        {@"(protected|private) var _zoneShape = 4.29497e+009", "public var zoneShape"},
                        {@"(protected|private) var _zoneMinSize = 4.29497e+009", "public var zoneMinSize"},
                        {@"(protected|private) var _weight", "public var weight"},
                        {@"(protected|private) var _type", "public var type"},
                        {@"(protected|private) var _oldValue", "public var oldValue"},
                        {@"(protected|private) var _newValue", "public var newValue"},
                        {@"(protected|private) var _lang", "public var lang"},
                        // ankama's devs are idiots, they attempt to assign -1 to a uint field
                        {@"public var iconId:uint", "public var iconId:int"},
                    };
        }

        public override bool ParsingEnabled()
        {
            return true;
        }

        protected override void SetTemplateParams(Parser parser, string file, TemplateHost host)
        {
        }
    }
}

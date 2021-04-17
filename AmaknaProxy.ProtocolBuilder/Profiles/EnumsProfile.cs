using ProtocolBuilder.Parsing;
using ProtocolBuilder.Templates;

namespace ProtocolBuilder.Profiles
{
    public class EnumsProfile : TemplateProfile
    {
        public override bool ParsingEnabled()
        {
            return true;
        }

        protected override void SetTemplateParams(Parser parser, string file, TemplateHost host)
        {
        }
    }
}

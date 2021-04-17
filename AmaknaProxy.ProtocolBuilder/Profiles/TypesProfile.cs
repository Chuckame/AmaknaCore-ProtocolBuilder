using System.IO;
using ProtocolBuilder.Parsing;
using ProtocolBuilder.Templates;

namespace ProtocolBuilder.Profiles
{
    public class TypesProfile : TemplateProfile
    {
        public override bool ParsingEnabled()
        {
            return false;
        }

        protected override void SetTemplateParams(Parser parser, string file, TemplateHost? host)
        {
            var xmlType = Program.Configuration.XmlTypesProfile.SearchXmlPattern(Path.GetFileNameWithoutExtension(parser.Filename));

            if (xmlType == null)
                Program.Shutdown($"File {file} not found");


            host.Session["Type"] = xmlType;
        }
    }
}

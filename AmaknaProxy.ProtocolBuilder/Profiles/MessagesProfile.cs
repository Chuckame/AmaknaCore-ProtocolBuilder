using System.IO;
using ProtocolBuilder.Parsing;
using ProtocolBuilder.Templates;

namespace ProtocolBuilder.Profiles
{
    public class MessagesProfile : TemplateProfile
    {
        public override bool ParsingEnabled()
        {
            return false;
        }

        protected override void SetTemplateParams(Parser parser, string file, TemplateHost host)
        {
            var xmlMessage = Program.Configuration.XmlMessagesProfile.SearchXmlPattern(Path.GetFileNameWithoutExtension(parser.Filename));

            if (xmlMessage == null)
                Program.Shutdown($"File {file} not found");
            
            host.Session["Message"] = xmlMessage;
        }
    }
}

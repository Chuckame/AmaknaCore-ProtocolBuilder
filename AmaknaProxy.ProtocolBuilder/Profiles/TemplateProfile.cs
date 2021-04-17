using System;
using System.CodeDom.Compiler;
using System.IO;
using ProtocolBuilder.Parsing;
using ProtocolBuilder.Templates;
using Microsoft.VisualStudio.TextTemplating;

namespace ProtocolBuilder.Profiles
{
    public abstract class TemplateProfile: ParsingProfile
    {
        private string _templateContentText;
        
        public override void ExecuteProfile(Parser parser)
        {
            string file = Path.Combine(Program.Configuration.Output, OutPutPath, GetRelativePath(parser.Filename), Path.GetFileNameWithoutExtension(parser.Filename));
            var engine = new Engine();
            var host = new TemplateHost(TemplatePath);
            if (_templateContentText == null)
            {
                _templateContentText = File.ReadAllText(TemplatePath);
            }
            host.Session["Profile"] = this;
            host.Session["Parser"] = parser;
            SetTemplateParams(parser, file, host);
            var output = engine.ProcessTemplate(_templateContentText, host);

            foreach (CompilerError error in host.Errors)
            {
                Console.Error.WriteLine(error.ErrorText);
            }

            if (host.Errors.Count > 0)
                Program.Shutdown();

            File.WriteAllText(file + host.FileExtension, output);

            Console.WriteLine("Wrote {0}", file + host.FileExtension);
        }

        protected abstract void SetTemplateParams(Parser parser, string file, TemplateHost host);
        
        public override bool MethodsIgnored()
        {
            return true;
        }
    }
}
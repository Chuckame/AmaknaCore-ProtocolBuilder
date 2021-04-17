using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using ProtocolBuilder.Parsing;
using ProtocolBuilder.XmlPatterns;

namespace ProtocolBuilder.Profiles
{
    public abstract class XmlProfile<T>: ParsingProfile where T : XmlComponent, new() 
    {
        private static readonly IDictionary<string, T> CACHE = new ConcurrentDictionary<string, T>();
        private static readonly XmlSerializer SERIALIZER = new XmlSerializer(typeof(T));

        public override bool ParsingEnabled()
        {
            return true;
        }

        public override bool MethodsIgnored()
        {
            return false;
        }

        public T SearchXmlPattern(string classname)
        {
            if (!CACHE.TryGetValue(classname, out T type))
            {
                var basePath = Path.Combine(Program.Configuration.Output, OutPutPath);
                string file = Directory.GetFiles(basePath, classname + ".xml", SearchOption.AllDirectories).First();
                type = (T) SERIALIZER.Deserialize(XmlReader.Create(file));
                CACHE.Add(classname, type);
            }

            return type;
        }

        protected abstract XmlPatternBuilder<T> newXmlPatternBuilder(Parser parser);

        public override void ExecuteProfile(Parser parser)
        {
            string relativePath = GetRelativePath(parser.Filename);

            string xmlfile = Path.Combine(Program.Configuration.Output, OutPutPath, relativePath, Path.GetFileNameWithoutExtension(parser.Filename)) + ".xml";

            var builder = newXmlPatternBuilder(parser);

            var parsed =  builder.Parse();
            CACHE.Add(parser.Class.Name, parsed);
            XmlWriter writer = XmlWriter.Create(xmlfile, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NamespaceHandling = NamespaceHandling.OmitDuplicates,
            });
            SERIALIZER.Serialize(writer, parsed);
            writer.Close();

            Console.WriteLine("Wrote {0}", xmlfile);
        }
    }
}
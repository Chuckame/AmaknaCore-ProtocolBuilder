using System.Xml.Serialization;

namespace ProtocolBuilder.XmlPatterns
{
    public class XmlComponent
    {
        [XmlAttribute]
        public string RelativePath
        {
            get;
            set;
        }

        [XmlAttribute]
        public string Name
        {
            get;
            set;
        }

        [XmlAttribute]
        public string Id
        {
            get;
            set;
        }

        [XmlAttribute]
        public string Heritage
        {
            get;
            set;
        }

        [XmlAttribute]
        public string Namespace
        {
            get;
            set;
        }

        public XmlField[] Fields
        {
            get;
            set;
        }
    }
}
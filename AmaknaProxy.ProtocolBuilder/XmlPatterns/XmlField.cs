using System.Xml.Serialization;

namespace ProtocolBuilder.XmlPatterns
{
    public class XmlField
    {
        [XmlAttribute]
        public string Name
        {
            get;
            set;
        }

        [XmlAttribute]
        public string WriteType
        {
            get;
            set;
        }

        [XmlAttribute]
        public string ReadType
        {
            get;
            set;
        }

        [XmlAttribute]
        public bool IsArray
        {
            get;
            set;
        }

        [XmlAttribute]
        public string ArrayLengthReadType
        {
            get;
            set;
        }

        [XmlAttribute]
        public string ArrayLengthWriteType
        {
            get;
            set;
        }

        [XmlAttribute]
        public bool IsStaticType
        {
            get;
            set;
        }

        [XmlAttribute]
        public bool IsPolymorphicType
        {
            get;
            set;
        }

        [XmlAttribute]
        public int FlagIndex
        {
            get;
            set;
        } = -1;

        [XmlAttribute]
        public string Condition
        {
            get;
            set;
        }
        
        [XmlIgnore]
        public bool IsGuessedField
        {
            get;
            set;
        }
    }
}

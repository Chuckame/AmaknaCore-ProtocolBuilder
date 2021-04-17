using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProtocolBuilder.Parsing.Elements;

namespace ProtocolBuilder.Parsing
{
    [Serializable]
	public class Parser
	{
        private static readonly Regex NamespacePattern = new Regex(@"package (\w+(?:\.\w+)*)", RegexOptions.Multiline);
        private static readonly Regex ClassPattern = new Regex(@"public class (?<className>\w+)(?: extends (?:\w+\.)*(?<heritage>\w+))?", RegexOptions.Multiline);
        private static readonly string ConstructorPattern = @"(?<acces>public|protected|private|internal)\s*function\s*(?<name>{0})\((?<argument>[^,)]+,?)*\)";
        private static readonly Regex ConstFieldPattern = new Regex(@"(?<acces>public|protected|private|internal)\s*(?<static>static)?\s*const\s*(?<name>\w+):(?<type>[\w_\.]+(?:<(?:\w+\.)*(?<generictype>[\w_<>]+)>)?)(?<value>\s*=\s*.*)?;", RegexOptions.Multiline);
        private static readonly Regex FieldPattern = new Regex(@"(?<acces>public|protected|private|internal)\s*(?<static>static)?\s*var\s*(?<name>[\w\d@]+):(?<type>[\w\d_\.<>]+)(?<value>\s*=\s*.*)?;", RegexOptions.Multiline);
        private static readonly Regex MethodPattern = new Regex(@"((?<acces>public|protected|private|internal)|(?<override>override)\s)+\s*function\s*(?<prop>get|set)?\s+(?<name>\w+)\((?<argument>[^,)]+,?)*\)\s*:\s*(?:\w+\.)*(?<returntype>\w+)", RegexOptions.Multiline);


        private string m_fileText;
        private string[] m_fileLines;

        private Dictionary<int, int> m_brackets;

        public string Filename { get; private set; }
        public IEnumerable<KeyValuePair<string, string>> BeforeParsingRules
        {
            get;
            set;
        }

        public string[] IgnoredLines
        {
            get;
            set;
        }

        public ClassInfo Class
        {
            get;
            internal set;
        }

        public EnumInfo EnumInfo
        {
            get;
            internal set;
        }

        public List<FieldInfo> Fields
        {
            get;
            internal set;
        }

        public List<MethodInfo> Methods
        {
            get;
            internal set;
        }

        public List<PropertyInfo> Properties
        {
            get;
            internal set;
        }

        public bool IgnoreMethods
        {
            get;
            set;
        }

        public Parser(string filename, IEnumerable<KeyValuePair<string, string>> beforeParsingRules, string[] ignoredLines)
        {
            Filename = filename;
            BeforeParsingRules = beforeParsingRules;
            IgnoredLines = ignoredLines;
        }

        public void ParseFile()
        {
            m_fileLines = File.ReadAllLines(Filename).Where(entry => !IsLineIgnored(entry)).Select(entry => ApplyRules(BeforeParsingRules, entry.Trim())).ToArray();
            m_fileText = string.Join("\r\n", m_fileLines);
            m_brackets = FindBracketsIndexesByLines(m_fileLines, '{', '}');

            var classMatch = ClassPattern.Match(m_fileText);

            if (!classMatch.Success)
            {
                throw new InvalidCodeFileException("This file does not contain a class");
            }

            Class = new ClassInfo
            {
                Name = classMatch.Groups["className"].Value,
                Heritage = classMatch.Groups["heritage"].Value,
                Namespace = NamespacePattern.Match(m_fileText).Groups[1].Value,
                AccessModifier = AccessModifiers.Public,
                // we don't mind about this
                ClassModifier = ClassInfo.ClassModifiers.None
            };

            ParseFields();

            if (!IgnoreMethods)
            {
                ParseMethods();
            }
        }

        private void ParseFields()
        {
            Fields = new List<FieldInfo>();

            Match matchConst = ConstFieldPattern.Match(m_fileText);
            while (matchConst.Success)
            {
                var field = new FieldInfo
                {
                    Modifiers = (AccessModifiers) Enum.Parse(typeof(AccessModifiers), matchConst.Groups["acces"].Value, true),
                    Name = matchConst.Groups["name"].Value,
                    IsProtocolField = !matchConst.Groups["name"].Value.StartsWith("_"),
                    Type = matchConst.Groups["generictype"].Value == string.Empty
                                                   ? matchConst.Groups["type"].Value
                                                   : $"List<{matchConst.Groups["generictype"].Value}>",
                    Value = matchConst.Groups["value"].Value.Replace("=", "").Trim(),
                    IsConst = true,
                    IsStatic = matchConst.Groups["static"].Value != string.Empty,
                };
                
                Fields.Add(field);

                matchConst = matchConst.NextMatch();
            }

            Match matchVar = FieldPattern.Match(m_fileText);
            while (matchVar.Success)
            {
                var field = new FieldInfo
                {
                    Modifiers = (AccessModifiers) Enum.Parse(typeof(AccessModifiers), matchVar.Groups["acces"].Value, true),
                    Name = matchVar.Groups["name"].Value,
                    IsProtocolField = !matchVar.Groups["name"].Value.StartsWith("_"),
                    Type = matchVar.Groups["type"].Value,
                    Value = matchVar.Groups["value"].Value.Trim(),
                    IsStatic = matchConst.Groups["static"].Value != string.Empty
                };

                if (field.Name != "idAccessors")
                {
                    Fields.Add(field);
                }

                matchVar = matchVar.NextMatch();
            }
        }

        private void ParseMethods()
        {
            Methods = new List<MethodInfo>();
            Properties = new List<PropertyInfo>();

            Match matchMethods = MethodPattern.Match(m_fileText);

            while (matchMethods.Success)
            {
                // do not support properties
                if (!string.IsNullOrEmpty(matchMethods.Groups["prop"].Value))
                {
                    matchMethods = matchMethods.NextMatch();
                    continue;
                }

                MethodInfo method = BuildMethodInfoFromMatch(matchMethods, false);
                method.Statements = BuildMethodElementsFromMatch(matchMethods).ToList();

                Methods.Add(method);

                matchMethods = matchMethods.NextMatch();
            }
        }

        private MethodInfo BuildMethodInfoFromMatch(Match match, bool constructor)
        {
            var method = new MethodInfo
            {
                AccessModifier = (AccessModifiers) Enum.Parse(typeof(AccessModifiers), match.Groups["acces"].Value, true),
                Name = match.Groups["name"].Value,
                Modifiers = match.Groups["override"].Value == "override"
                                ? new List<MethodInfo.MethodModifiers>(new[] { MethodInfo.MethodModifiers.Override })
                                : new List<MethodInfo.MethodModifiers>(new[] { MethodInfo.MethodModifiers.None }),
                ReturnType = constructor ? "" : match.Groups["returntype"].Value,
            };

            var args = new List<Argument>();
            foreach (object capture in match.Groups["argument"].Captures)
            {
                var arg = new Argument();

                string argStr = capture.ToString().Trim().Replace(",", "");

                arg.Name = argStr.Split(':').First().Trim();

                if (argStr.Contains("<"))
                {
                    string generictype = argStr.Split('<').Last().Split('>').First().Split('.').Last();

                    arg.Type = $"List<{generictype}>";
                }
                else
                    arg.Type = argStr.Split(':').Last().Split('.').Last().Trim();

                if (arg.Type.Contains("="))
                {
                    arg.DefaultValue = arg.Type.Split('=').Last().Trim();
                    arg.Type = arg.Type.Split('=').First().Trim();
                }
                else if (!string.IsNullOrEmpty(args.LastOrDefault().DefaultValue))
                {
                    arg.DefaultValue = "null";
                }

                args.Add(arg);

            }

            method.Arguments = args.ToArray();

            if (!string.IsNullOrEmpty(match.Groups["prop"].Value))
            {
                var foundProperty = Properties.FirstOrDefault(entry => entry.Name == method.Name);
                PropertyInfo property = foundProperty ?? new PropertyInfo
                {
                    Name = method.Name,
                    AccessModifier = method.AccessModifier,
                };

                if (match.Groups["prop"].Value == "get")
                {
                    property.MethodGet = method;
                    property.PropertyType = method.ReturnType;
                }
                else if (match.Groups["prop"].Value == "set")
                {
                    property.MethodSet = method;
                }
                if (foundProperty == null)
                    Properties.Add(property);
            }

            return method;
        }

        private IEnumerable<IStatement> BuildMethodElementsFromMatch(Match match)
        {
            int bracketOpen =
                Array.FindIndex(m_fileLines, (entry) => entry.Contains(match.Groups[0].Value));
            if (!m_fileLines[bracketOpen].EndsWith("{"))
                bracketOpen++;
            int bracketClose = m_brackets[bracketOpen];

            var methodlines = new string[(bracketClose - 1) - bracketOpen];

            Array.Copy(m_fileLines, bracketOpen + 1, methodlines, 0, (bracketClose - 1) - bracketOpen);

            return ParseMethodExecutions(methodlines);
        }

        private static Dictionary<int, int> FindBracketsIndexesByLines(string[] lines, char startDelimiter, char endDelimiter)
        {
            var elementsStack = new Stack<int>();
            var result = new Dictionary<int, int>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(startDelimiter))
                    elementsStack.Push(i);

                if (lines[i].Contains(endDelimiter))
                {
                    int index = elementsStack.Pop();

                    result.Add(index, i);
                }
            }

            if (elementsStack.Count > 0)
                foreach (int i in elementsStack)
                {
                    throw new Exception($"Bracket '{startDelimiter}' at index " + i + " is not closed");
                }

            return result;
        }

        private IEnumerable<IStatement> ParseMethodExecutions(IEnumerable<string> lines)
        {
            var result = new List<IStatement>();

            int blockDepth = 0;
            foreach (string line in lines.Select(entry => entry.Trim()))
            {
                try
                {
                    if (IsLineIgnored(line) || line == "{")
                        continue;

                    if (line == "}")
                    {
                        if (blockDepth > 0)
                        {
                            result.Add(new ControlStatementEnd());
                            blockDepth--;
                        }
                        continue;
                    }
                    
                    if (ControlStatement.TryParse(line) is { } controlStatement)
                    {
                        blockDepth++;
                        result.Add(controlStatement);
                    }
                    else if (ForStatement.TryParse(line) is { } forStatement)
                    {
                        blockDepth++;
                        result.Add(forStatement);
                    }
                    else if (AssignationStatement.TryParse(line) is { } assignationStatement)
                    {
                        result.Add(assignationStatement);
                    }
                    else if (InvokeExpression.TryParse(line) is { } invokeStatement)
                    {
                        if (!string.IsNullOrEmpty(invokeStatement.ReturnVariableAssignation) && string.IsNullOrEmpty(invokeStatement.Preffix))
                        {
                            var field = Fields.FirstOrDefault(entry => entry.Name == invokeStatement.ReturnVariableAssignation);
                            if (field != null)
                            {
                                invokeStatement.Preffix = $"({field.Type})";
                            }
                        }
                        result.Add(invokeStatement);
                    } else {
                        result.Add(new UnknownStatement
                        {
                            Value = line
                        });
                    }
                } catch (Exception e) {
                    Console.Error.WriteLine($"Error while parsing line '{line}' for '{Class.Name}': {e.Message}");
                }
            }

            return result;
        }

        private static string ApplyRules(IEnumerable<KeyValuePair<string, string>> rules, string str) {
            if (rules == null)
                return str;

            if (string.IsNullOrEmpty(str))
                return str;

            foreach (var rule in rules) {
                str = Regex.Replace(str, rule.Key, rule.Value);
            }

            return str;
        }

        private bool IsLineIgnored(string line) {
            return IgnoredLines != null && IgnoredLines.Any(rule => Regex.IsMatch(line, rule));
        }
    }

    public class InvalidCodeFileException : Exception {
        public InvalidCodeFileException(string message) : base(message) { }
    }
}

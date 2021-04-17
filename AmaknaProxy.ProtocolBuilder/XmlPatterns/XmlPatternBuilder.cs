using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProtocolBuilder.Parsing;
using ProtocolBuilder.Parsing.Elements;
using ProtocolBuilder.Profiles;

namespace ProtocolBuilder.XmlPatterns
{
    public abstract class XmlPatternBuilder<T> where T : XmlComponent, new()
    {
        private static readonly Regex ExtractReadTypePattern = new Regex(@"read(\w+)\(");
        private static readonly Regex IsTypesNewPattern = new Regex(@"new (\w+)\(");
        private static readonly Regex GetFlagPattern = new Regex(@"getFlag\(\w+,(\d+)\)");
        private static readonly Regex GetInstancePattern = new Regex(@"getInstance\((\w+),(\w+)\)");

        private static readonly IDictionary<string, string> readToWriteType = new ConcurrentDictionary<string, string>()
        {
            ["Flag"] = "Flag",
            ["Boolean"] = "Boolean",
            ["Bytes"] = "Bytes",

            ["Double"] = "Double",
            ["Float"] = "Float",

            ["Byte"] = "Byte",
            ["UnsignedByte"] = "Byte",

            ["Short"] = "Short",
            ["UnsignedShort"] = "Short",

            ["Int"] = "Int",
            ["UnsignedInt"] = "Int",

            ["VarShort"] = "VarShort",
            ["VarUhShort"] = "VarShort",

            ["VarInt"] = "VarInt",
            ["VarUhInt"] = "VarInt",

            ["VarLong"] = "VarLong",
            ["VarUhLong"] = "VarLong",

            ["UTF"] = "UTF",
        };

        private readonly Parser Parser;
        private readonly ParsingProfile _profile;

        protected XmlPatternBuilder(Parser parser,ParsingProfile profile)
        {
            Parser = parser;
            _profile = profile;
        }

        public T Parse()
        {
            var xmlComponent = new T()
            {
                Name = Parser.Class.Name,
                Id = Parser.Fields.Find(entry => entry.Name == "protocolId").Value,
                Heritage = Parser.Class.Heritage == "NetworkMessage" ||  Parser.Class.Heritage == "" ? null : Parser.Class.Heritage,
                Namespace = Parser.Class.Namespace,
                RelativePath = _profile.GetRelativePath(Parser.Filename).Replace('\\', '.').Replace('/', '.')
            };
            var xmlFields = new List<XmlField>();

            var deserializeAsMethod = Parser.Methods.First(entry => entry.Name == $"deserializeAs_{xmlComponent.Name}");
            BuildFields(deserializeAsMethod, xmlFields);

            var parsedFields = Parser.Fields.Where(x => x.IsProtocolField && x.Name != "protocolId").Select(x => x.Name).ToList();
            var builtFields = xmlFields.Where(x => !x.IsGuessedField).Select(x => x.Name).ToList();
            var guessedFields = xmlFields.Where(x => x.IsGuessedField).Select(x => x.Name).ToList();
            if (builtFields.Except(parsedFields).Any())
            {
                throw new Exception($"Parsed [{string.Join(", ", parsedFields)}] fields, while built [{string.Join(", ", builtFields)}] and guessed {string.Join(", ", guessedFields)}");
            }

            foreach (var field in xmlFields)
            {
                if (!field.IsStaticType && !field.IsPolymorphicType)
                {
                    if (!readToWriteType.ContainsKey(field.ReadType))
                        throw new NotSupportedException($"write type {field.ReadType} is unknown");
                    field.WriteType = readToWriteType[field.ReadType];
                }
                else
                {
                    field.WriteType = field.ReadType;
                }
                if (!string.IsNullOrEmpty(field.ArrayLengthReadType))
                {
                    if (!readToWriteType.ContainsKey(field.ArrayLengthReadType))
                        throw new NotSupportedException($"array length write type {field.ArrayLengthReadType} is unknown");
                    field.ArrayLengthWriteType = readToWriteType[field.ArrayLengthReadType];
                }
            }

            xmlComponent.Fields = xmlFields.ToArray();
            return xmlComponent;
        }

        private void BuildFields(MethodInfo method, List<XmlField> xmlFields)
        {
            int currentStatementIndex = 0;

            #region common methods
            IStatement? GetNextStatement()
            {
                return currentStatementIndex + 1 < method.Statements.Count ? method.Statements[currentStatementIndex + 1] : null;
            }

            string GetCondition()
            {
                return GetNextStatement() is ControlStatement {ControlType: ControlType.If} conditionStatement
                    ? conditionStatement.Condition
                    : null;
            }

            S? FindPreviousStatement<S>(int baseIndex, Predicate<S> predicate) where S : class, IStatement
            {
                for (int i = baseIndex - 1; i >= 0; i--)
                {
                    if (method.Statements[i] is S statement && predicate(statement))
                    {
                        return statement;
                    }
                }

                return default;
            }
            
            S? FindNextStatement<S>(int baseIndex, Predicate<S> predicate) where S : class, IStatement
            {
                for (int i = baseIndex + 1; i < method.Statements.Count; i++)
                {
                    if (method.Statements[i] is S statement && predicate(statement))
                    {
                        return statement;
                    }
                }

                return default;
            }
            #endregion

            #region basic field
            /*
  1. is AssignationStatement with Name is inside fields (where field.Name == assignment.Name)
     - possible assignations Value :
          - ProtocolTypeManager.getInstance(TYPE,ID_VAR) (polymorphic type)
            - go back to find ID_VAR assignation (where assignment.Name == Args[1])
          - new TYPE() (static type)
          - readTYPE() (primitive type)
            - Extract condition
          - BooleanByteWrapper.getFlag(RAW_VAR,INDEX)
             */
            XmlField? TryProcessBasicField(IStatement statement)
            {
                if (statement is AssignationStatement assignation && FindField(assignation.Name) is { } field)
                {
                    return ExtractFieldFromAssignation(assignation, field);
                }
                return null;
            }
            XmlField? ExtractFieldFromAssignation(AssignationStatement assignation, FieldInfo field)
            {
                if (assignation.Value.StartsWith("ProtocolTypeManager.getInstance"))
                {
                    var match = GetInstancePattern.Match(assignation.Value);
                    if (!match.Success)
                        throw new NotSupportedException($"Unable to parse ProtocolTypeManager.getInstance: {assignation.Value}");
                    var typeName = match.Groups[1].Value;
                    var idVarName = match.Groups[2].Value;
                    var idVarAssignment = FindPreviousStatement<AssignationStatement>(currentStatementIndex, s => s.Name == idVarName);
                    if (idVarAssignment == null)
                        throw new NotImplementedException("Unable to parse ProtocolTypeManager.getInstance: type id not found on previous statements");
                    string idType = GetMatchFirstValue(ExtractReadTypePattern, idVarAssignment.Value);
                    if (idType != "UnsignedShort")
                        throw new NotSupportedException("Current parsed protocol only takes Short for types id's");
                    
                    return new XmlField()
                    {
                        Name = field.Name,
                        ReadType = typeName,
                        IsPolymorphicType = true,
                    };
                }
                if (assignation.Value.StartsWith("new "))
                {
                    return new XmlField()
                    {
                        Name = field.Name,
                        ReadType = GetMatchFirstValue(IsTypesNewPattern, assignation.Value),
                        IsStaticType = true,
                    };
                }
                if (assignation.Value.Contains(".read"))
                {
                    return new XmlField()
                    {
                        Name = field.Name,
                        ReadType = GetMatchFirstValue(ExtractReadTypePattern, assignation.Value),
                        Condition = GetCondition(),
                    };
                }
                if (assignation.Value.StartsWith("BooleanByteWrapper.getFlag"))
                {
                    return new XmlField()
                    {
                        Name = field.Name,
                        ReadType = "Flag",
                        FlagIndex = int.Parse(GetMatchFirstValue(GetFlagPattern, assignation.Value))
                    };
                }

                return null;
            }
            #endregion

            #region array field
            /*
  2. is InvokeExpression with Name == push and Target is inside fields (where invocation.Name == push && field.Name == invocation.Target)
    - go back to find the previous 'for (...;...<LimitVarName;...)'
      - go back to find LimitVarName assignation (where assignment.Name == ForStatement.LimitVarName)
    - go back to find pushed value assignation (where assignment.Name == Args[0])
    - use same algo as 1.
             */
            XmlField? TryProcessArrayField(IStatement statement)
            {
                if (statement is InvokeExpression {Name: "push"} invocation && FindField(invocation.Target) is { } field)
                {
                    var arrayLengthReadType = ExtractArrayLengthReadType(currentStatementIndex);

                    // find pushed value assignation
                    var s = FindPreviousStatement<AssignationStatement>(currentStatementIndex, s => s.Name == invocation.Args[0]);
                    if (s == null)
                        throw new NotImplementedException($"Unable to parse push method: pushed value '{invocation.Args[0]}' assignation not found");
                    
                    var xmlField = ExtractFieldFromAssignation(s, field);
                    if (xmlField == null)
                        throw new NotImplementedException($"Unable to parse push method: no field extracted from assignation {s}");
                    xmlField.IsArray = true;
                    xmlField.ArrayLengthReadType = arrayLengthReadType;
                    return xmlField;
                }
                return null;
            }

            string ExtractArrayLengthReadType(int fromIndex)
            {
                var forStatement = FindPreviousStatement<ForStatement>(fromIndex, s => true);
                if (forStatement == null)
                    throw new NotSupportedException("Unable to parse push method: previous 'for' loop statement not found");
                var lengthAssignment = FindPreviousStatement<AssignationStatement>(method.Statements.IndexOf(forStatement), s => s.Name == forStatement.LimitVarName);
                if (lengthAssignment == null)
                    throw new NotSupportedException($"Unable to parse push method: array length variable '{forStatement.LimitVarName}' assignment not found");
                return GetMatchFirstValue(ExtractReadTypePattern, lengthAssignment.Value);
            }
            #endregion
            
            #region Bytes field
            /*
            3. is InvokeExpression with name == readBytes
             */
            XmlField? TryProcessBytesField(IStatement statement)
            {
                if (statement is InvokeExpression {Name: "readBytes"} invokeExpression)
                {
                    return ProcessReadBytesMethod(invokeExpression);
                }
                return null;
            }
            
            XmlField ProcessReadBytesMethod(InvokeExpression invokeExpression)
            {
                if (invokeExpression.Args.Length != 3)
                    throw new NotImplementedException($"Unable to parse readBytes: Bad params count ({invokeExpression.Args.Length} instead of 3)");
                string bufferVariableName = invokeExpression.Args[0];
                string lengthVariableName = invokeExpression.Args[2];
                var lengthAssignment = FindPreviousStatement<AssignationStatement>(currentStatementIndex, s => s.Name == lengthVariableName);
                if (lengthAssignment == null)
                    throw new NotImplementedException("Unable to parse readBytes: length type not found on previous statements");
                string lengthType = GetMatchFirstValue(ExtractReadTypePattern, lengthAssignment.Value);
                if (lengthType != "VarInt")
                    throw new NotSupportedException("Current parsed protocol only takes VarInt for bytes length");
                
                string fieldName;
                var field = FindField(bufferVariableName);
                if (field == null)
                {
                    var nextAssignment = FindNextStatement<AssignationStatement>(currentStatementIndex, s => s.Value == bufferVariableName);
                    if (nextAssignment == null)
                        throw new NotImplementedException($"Unable to parse readBytes: field {bufferVariableName} not found on next statements");
                    fieldName = nextAssignment.Name;

                    if (fieldName == null)
                        throw new NotImplementedException($"Unable to parse readBytes: field {bufferVariableName} not found on next statements");

                    field = FindField(fieldName);
                    if (field == null)
                    {
                        Console.WriteLine($"field {fieldName} not found, but added");
                    }
                }
                else
                {
                    fieldName = bufferVariableName;
                }

                return new XmlField
                {
                    Name = fieldName,
                    ReadType = "Bytes",
                    IsArray = false,
                    IsPolymorphicType = false,
                    IsStaticType = false,
                    IsGuessedField = true,
                    ArrayLengthReadType = lengthType,
                };
            }
            #endregion

            for (; currentStatementIndex < method.Statements.Count; currentStatementIndex++)
            {
                IStatement currentStatement = method.Statements[currentStatementIndex];

                if (TryProcessBasicField(currentStatement) is {} basicField)
                {
                    xmlFields.Add(basicField);
                }
                else if (TryProcessArrayField(currentStatement) is {} arrayField)
                {
                    xmlFields.Add(arrayField);
                }
                else if (TryProcessBytesField(currentStatement) is {} bytesField)
                {
                    xmlFields.Add(bytesField);
                }
                else if (currentStatement is InvokeExpression subcall && string.IsNullOrEmpty(subcall.Target) && string.IsNullOrEmpty(subcall.Preffix))
                {
                    var function = Parser.Methods.FirstOrDefault(x => x.Name == subcall.Name);
                    if (function != null)
                    {
                        if (function == method)
                            throw new NotSupportedException("same method detected");
                        BuildFields(function, xmlFields);
                    }
                    else
                        throw new NotImplementedException($"Method '{subcall.Name}' apparently not parsed");
                }
            }
        }

        private FieldInfo? FindField(string name)
        {
            return Parser.Fields.Find(entry => entry.Name == name);
        }

        private static string GetMatchFirstValue(Match match)
        {
            return match.Groups[1].Value;
        }

        private static string GetMatchFirstValue(Regex pattern, string value)
        {
            return GetMatchFirstValue(pattern.Match(value));
        }
    }
}

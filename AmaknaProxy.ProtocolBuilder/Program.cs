using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using ProtocolBuilder.Parsing;
using ProtocolBuilder.Profiles;

namespace ProtocolBuilder
{
    public class Program
    {
        public static Configuration Configuration = new Configuration();

        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var serializer = new XmlSerializer(typeof(Configuration));
            Configuration.SetDefault();

            string configPath = "./config.xml";
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-config":
                        if (args.Length < i + 2)
                            Shutdown("Value of -config is not defined like : -config {configPath}");

                        configPath = args[i + 1];
                        break;
                    case "-createconfig":
                        var writer = XmlWriter.Create(configPath, new XmlWriterSettings()
                        {
                            Indent = true
                        });
                        serializer.Serialize(writer, Configuration);
                        writer.Close();
                        Shutdown("Config created. Please restart");
                        break;
                }
            }

            if (!File.Exists(configPath))
            {
                var writer = XmlWriter.Create(configPath, new XmlWriterSettings()
                                                              {
                                                                  Indent = true
                                                              });
                serializer.Serialize(writer, Configuration);
                writer.Close();
                Shutdown("Config created. Please restart");
            }
            else
            {
                var reader = XmlReader.Create(configPath, new XmlReaderSettings());
                Configuration = serializer.Deserialize(reader) as Configuration;
                reader.Close();
            }

            var profiles =
            new ParsingProfile[]
                {
                    Configuration.XmlTypesProfile,
                    Configuration.XmlMessagesProfile,
                    Configuration.TypesProfile,
                    Configuration.MessagesProfile,
                    Configuration.DatacenterProfile,
                    Configuration.EnumsProfile,
                };

            foreach (ParsingProfile parsingProfile in profiles)
            {
                if (parsingProfile == null || parsingProfile.Disabled)
                    continue;

                Console.WriteLine("Executing profile \'{0}\' ... ", parsingProfile.Name);

                if (parsingProfile.OutPutNamespace != null)
                    parsingProfile.OutPutNamespace = parsingProfile.OutPutNamespace.Insert(0, Configuration.BaseNamespace);

                if (!Directory.Exists(Configuration.Output))
                    Directory.CreateDirectory(Configuration.Output);

                
                if (Directory.Exists(Path.Combine(Configuration.Output, parsingProfile.OutPutPath)))
                {
                    DeleteDirectory(Path.Combine(Configuration.Output, parsingProfile.OutPutPath));
                }

                Directory.CreateDirectory(Path.Combine(Configuration.Output, parsingProfile.OutPutPath));

                IEnumerable<string> files = Directory.EnumerateFiles(
                    Path.Combine(Configuration.SourcePath, parsingProfile.SourcePath), "*.as",
                    SearchOption.AllDirectories);

                Parallel.ForEach(files, new ParallelOptions() {MaxDegreeOfParallelism = (int)Configuration.Parallelism}, file =>
                {
                    string relativePath = parsingProfile.GetRelativePath(file);

                    if (!Directory.Exists(Path.Combine(Configuration.Output, parsingProfile.OutPutPath, relativePath)))
                        Directory.CreateDirectory(Path.Combine(Configuration.Output, parsingProfile.OutPutPath, relativePath));

                    var parser = new Parser(file, parsingProfile.BeforeParsingReplacementRules,
                            parsingProfile.IgnoredLines)
                        {IgnoreMethods = parsingProfile.MethodsIgnored()};

                    try
                    {
                        if (parsingProfile.ParsingEnabled())
                            parser.ParseFile();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"File {Path.GetFileName(file)} not parsed correctly: {e.Message}");
                        return;
                    }

                    parsingProfile.ExecuteProfile(parser);
                });

                Console.WriteLine("Done !");
            }
        }

        private static void DeleteDirectory(string targetDir)
        {
            string[] files = Directory.GetFiles(targetDir);
            string[] dirs = Directory.GetDirectories(targetDir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(targetDir, false);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Shutdown("Unhandled Exception : " + e.ExceptionObject);
        }

        public static void Shutdown(string reason = "")
        {
            Console.Error.WriteLine("The program is shutting down{0}", !string.IsNullOrEmpty(reason) ? $" for reason: {reason}" : "");
            Environment.Exit(1);
        }
    }
}

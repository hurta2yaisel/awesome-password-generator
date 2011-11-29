using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using Password_Generator;
using System.Threading;


namespace apg_cl
{
    class Program
    {
        static Mutex m = new Mutex(false, "Awesome Password Generator");   // for inno setup (see AppMutex)

        static PasswordGenerator pswgen = new PasswordGenerator();

        static Hashtable CommandLineArgs = new Hashtable();    // Indexed command line args

        static PasswordGenerator.PasswordGenerationOptions pgo = new PasswordGenerator.PasswordGenerationOptions();
        private enum DestinationType { console, file };
        static private DestinationType destinationType;
        static private string fileName;
        static private bool appendToFile;   // or replace if FALSE

        enum ExitCodes { ok, cmdlineArgError, memAllocError, cantSavePasswordsToFile };

        //--------------------------------------------------
        //--------------------------------------------------

        static void Main(string[] args)
        {
            ParseCmdline(args);
            ProcessCmdline();
            GeneratePasswords();

            Environment.Exit((int)ExitCodes.ok);
        }

        //--------------------------------------------------

        // fill the CommandLineArgs hashtable
        static void ParseCmdline(string[] args)
        {
            if (args.Length == 0)
            {
                PrintAppInfoAndUsage();
                Environment.Exit((int)ExitCodes.cmdlineArgError);
            }

            string pattern = @"(?<argname>--?\w+):(?<argvalue>.+)";

            foreach (string arg in args)
            {
                // split argument into name and value
                Match match = Regex.Match(arg, pattern);

                // find long argument name
                string argLongName, argName, argValue;
                if (match.Success)  // argument with value, like "--argname:value"
                {
                    argName = match.Groups["argname"].Value;
                    argValue = match.Groups["argvalue"].Value;
                }
                else  // argument without value, like "--argname"
                {
                    argName = arg;
                    argValue = "";
                }
                argLongName = GetLongArgOrValueName(argName);
                if (argLongName.Length == 0)
                {
                    ReportErrorAndExit(string.Format("ERROR: unknown argument: {0}", arg), ExitCodes.cmdlineArgError);
                }

                // Store command line arg and value
                CommandLineArgs[argLongName] = argValue;
            }
        }

        //--------------------------------------------------

        // fill the pgo struct and initialize PasswordGenerator class with it
        static void ProcessCmdline()
        {
            // password length
            if (!CommandLineArgs.ContainsKey("--length"))
            {
                ReportErrorAndExit("ERROR: required argument is absent: --length", ExitCodes.cmdlineArgError);
            }
            if (!int.TryParse(CommandLineArgs["--length"].ToString(), out pgo.pswLength) || pgo.pswLength <= 0)
            {
                ReportErrorAndExit("ERROR: incorrect argument value: --length:" + CommandLineArgs["--length"].ToString(), ExitCodes.cmdlineArgError);
            }

            // quantity
            if (!CommandLineArgs.ContainsKey("--quantity"))
            {
                ReportErrorAndExit("ERROR: required argument is absent: --quantity", ExitCodes.cmdlineArgError);
            }
            if (!int.TryParse(CommandLineArgs["--quantity"].ToString(), out pgo.quantity) || pgo.quantity <= 0)
            {
                ReportErrorAndExit("ERROR: incorrect argument value: --quantity:" + CommandLineArgs["--quantity"].ToString(), ExitCodes.cmdlineArgError);
            }

            // charsets
            if (!CommandLineArgs.ContainsKey("--charsets"))
            {
                ReportErrorAndExit("ERROR: required argument is absent: --charsets", ExitCodes.cmdlineArgError);
            }
            string charsets = CommandLineArgs["--charsets"].ToString();
            if (charsets.Length == 0)
            {
                ReportErrorAndExit("ERROR: required argument value is empty: --charsets", ExitCodes.cmdlineArgError);
            }
            // fill the pgo.charsets array
            pgo.charsets = new string[0];
            Hashtable cs;
            cs = pswgen.Charsets;
            foreach (string charset in charsets.Split(','))
            {
                if (charset.Length == 0)
                {
                    ReportErrorAndExit("ERROR: incorrect argument value: --charsets:" + charsets, ExitCodes.cmdlineArgError);
                }
                if (!cs.ContainsKey(charset))
                {
                    ReportErrorAndExit("ERROR: unknown charset in the \"--charsets\" argument: " + charset, ExitCodes.cmdlineArgError);
                }
                Array.Resize(ref pgo.charsets, pgo.charsets.Length + 1);
                pgo.charsets[pgo.charsets.Length - 1] = charset;
            }

            // user defined charset
            if (pgo.charsets.Contains("userDefined"))
            {
                if(!CommandLineArgs.ContainsKey("--userDefinedCharset"))
                {
                    ReportErrorAndExit("ERROR: required argument is absent: --userDefinedCharset", ExitCodes.cmdlineArgError);
                }
                string udc = CommandLineArgs["--userDefinedCharset"].ToString();
                if (udc.Length == 0)
                {
                    ReportErrorAndExit("ERROR: argument value is empty: --userDefinedCharset", ExitCodes.cmdlineArgError);
                }
                pgo.userDefinedCharset = udc;
            }

            // generate easy-to-type passwords
            if (CommandLineArgs.ContainsKey("--easytotype"))
            {
                string ett = CommandLineArgs["--easytotype"].ToString();
                if (ett.Length != 0)
                {
                    ReportErrorAndExit("ERROR: incorrect argument value (must be empty!): --easytotype:" + ett, ExitCodes.cmdlineArgError);
                }
                pgo.easyToType = true;
            }

            // exclude confusing characters
            if (CommandLineArgs.ContainsKey("--excludeConfusingCharacters"))
            {
                string ecc = CommandLineArgs["--excludeConfusingCharacters"].ToString();
                if (ecc.Length != 0)
                {
                    ReportErrorAndExit("ERROR: incorrect argument value (must be empty!): --excludeConfusingCharacters:" + ecc, ExitCodes.cmdlineArgError);
                }
                pgo.excludeConfusing = true;
            }

            // destination
            if (!CommandLineArgs.ContainsKey("--destination"))
            {
                ReportErrorAndExit("ERROR: required argument is absent: --destination", ExitCodes.cmdlineArgError);
            }
            string dest = CommandLineArgs["--destination"].ToString();
            if (dest.Length == 0)
            {
                ReportErrorAndExit("ERROR: required argument value is empty: --destination", ExitCodes.cmdlineArgError);
            }
            string destType = GetLongArgOrValueName(dest.Split(':')[0]);
            switch (destType)
            {
                case "console":
                    destinationType = DestinationType.console;
                    break;
                case "file":
                    if (dest.Split(':').Length < 3)
                    {
                        ReportErrorAndExit("ERROR: incorrect destination: --destination:" + dest, ExitCodes.cmdlineArgError);
                    }

                    string destFileAccessMethod = GetLongArgOrValueName(dest.Split(':')[1]);
                    switch (destFileAccessMethod)
                    {
                        case "append":
                            appendToFile = true;
                            break;
                        case "replace":
                            appendToFile = false;
                            break;
                        default:
                            ReportErrorAndExit("ERROR: unknown file access method: --destination:" + dest, ExitCodes.cmdlineArgError);
                            break;
                    }

                    //fileName;

                    int pos = dest.IndexOf(':', 0);
                    pos = dest.IndexOf(':', pos + 1);
                    fileName = dest.Substring(pos + 1);
                    if (fileName.Length == 0)
                    {
                        ReportErrorAndExit("ERROR: file name is empty: --destination:" + dest, ExitCodes.cmdlineArgError);
                    }

                    destinationType = DestinationType.file;
                    break;
                default:
                    ReportErrorAndExit("ERROR: unknown destination type: --destination:" + dest.Split(':')[0], ExitCodes.cmdlineArgError);
                    break;
            }

            // initialize class
            pswgen = new PasswordGenerator(pgo);
            if (!pswgen.isReady)
            {
                ReportErrorAndExit("ERROR: some errors in command line arguments", ExitCodes.cmdlineArgError);
            }
        }

        //--------------------------------------------------

        static string GetLongArgOrValueName(string name)
        {
            Hashtable names = new Hashtable();
            names.Add("-q", "--quantity");
            names.Add("-l", "--length");
            names.Add("-c", "--charsets");
            names.Add("-udc", "--userDefinedCharset");
            names.Add("-ett", "--easytotype");
            names.Add("-ecc", "--excludeConfusingCharacters");
            names.Add("-d", "--destination");
            names.Add("c", "console");
            names.Add("f", "file");
            names.Add("a", "append");
            names.Add("r", "replace");

            if (names.ContainsKey(name))
                return (string)names[name];   // convert short name to long name
            else if (names.ContainsValue(name))
                return name; // name is already a long name, no need in conversion
            else
                return "";   // argument or value not found!
        }

        //--------------------------------------------------

        static void ReportErrorAndExit(string errmsg, ExitCodes exitcode)
        {
            PrintAppInfoAndUsage();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errmsg);
            Environment.Exit((int)exitcode);
        }

        //--------------------------------------------------

        static void PrintAppInfoAndUsage()
        {
            Console.WriteLine();
            Console.WriteLine("Awesome Password Generator " +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() +
                " (console version)");
            Console.WriteLine("Copyright (c) 2011  __alex");
            Console.WriteLine("Licensed under GNU General Public License v3");
            Console.WriteLine("Homepage: http://code.google.com/p/awesome-password-generator/");
            Console.WriteLine("Forum:    http://groups.google.com/group/awesome-password-generator");
            Console.WriteLine();
            Console.WriteLine("Usage: Run GUI app (Awesome Password Generator.exe), set up password length,");
            Console.WriteLine("       quantity, charsets and other options as you wish and click");
            Console.WriteLine("       \"Command line builder\" expander.");
            Console.WriteLine("       Run apg-cl.exe with generated command line.");
            Console.WriteLine();
        }

        //--------------------------------------------------

        static void GeneratePasswords()
        {
            switch (destinationType)
            {
                case DestinationType.console:
                    for (int i = 0; i < pgo.quantity; i++)
                        Console.WriteLine(pswgen.GeneratePassword());
                    break;

                case DestinationType.file:
                    // allocate memory
                    string[] bulkPasswords;
                    try
                    {
                        bulkPasswords = new string[pgo.quantity];
                    }
                    catch (Exception exception)
                    {
                        ReportErrorAndExit("ERROR: Can't allocate memory!\n\n" + exception.Message.ToString(), ExitCodes.memAllocError);
                        return;
                    }

                    // generate pgo.quantity passwords
                    //int lastWholePercent = 0, curWholePercent;
                    string psw;
                    for (int i = 1; i <= pgo.quantity; i++)
                    {
                        psw = pswgen.GeneratePassword();
                        bulkPasswords[i - 1] = psw;

                        /*
                        // update progress bar (if needed)
                        curWholePercent = (int)Math.Round((double)i * (double)99 / (double)pgo.quantity, MidpointRounding.AwayFromZero);
                        if (lastWholePercent != curWholePercent)
                        {
                            //worker.ReportProgress(curWholePercent);
                            lastWholePercent = curWholePercent;
                        }*/
                    }

                    // save passwords in file
                    try
                    {
                        if (appendToFile)
                            System.IO.File.AppendAllLines(fileName, bulkPasswords);
                        else
                            System.IO.File.WriteAllLines(fileName, bulkPasswords);
                    }
                    catch (Exception exception)
                    {
                        ReportErrorAndExit("ERROR: Can't save generated passwords to file!\n\n" + exception.Message.ToString(), ExitCodes.cantSavePasswordsToFile);
                    }

                    break;
            }
        }
    }
}

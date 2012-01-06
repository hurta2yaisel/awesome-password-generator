using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Collections;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Runtime.Serialization;


namespace Awesome_Password_Generator
{
    public class MyClass
    {

        public static MainWindow mw2;
    }

    [ServiceContract]
    [ServiceKnownType(typeof(QuickGenMode))]
    public interface IQuickGen
    {
        [OperationContract(IsOneWay = true)]
        void DoQuickGen(QuickGenMode quickgenMode);
    }

    [DataContract]
    public enum QuickGenMode { [EnumMember] password, [EnumMember] wpa };

    public class QuickGen : IQuickGen
    {
        public void DoQuickGen(QuickGenMode quickgenMode)
        {
            MessageBox.Show("QuickGen mode: " + quickgenMode.ToString());   //`
            switch (quickgenMode)
            {
                case QuickGenMode.password:
                    break;
                case QuickGenMode.wpa:
                    break;
            }
            MyClass.mw2
            //`todo
        }
    }

    //---

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        bool appIsAlreadyRunning;
        Mutex mutexApp;
        enum ExitCodes { ok, cmdlineArgError, memAllocError, cantSavePasswordsToFile };
        Hashtable CommandLineArgs = new Hashtable();    // Indexed command line args
        private QuickGenMode quickGenMode;
        public MainWindow mainWindow = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            mutexApp = new Mutex(false, "Awesome Password Generator", out appIsAlreadyRunning);
            appIsAlreadyRunning = !appIsAlreadyRunning;

            if (e.Args.Count() > 0)
            {
                ParseCmdline(e.Args);
                ProcessCmdline();

                if (appIsAlreadyRunning)
                {
                    // send command to perform a QuickGen to the remote app and exit
                    PerformQuickGen();
                    Environment.Exit((int)ExitCodes.ok);
                }
                else
                {
                    // show main window minimized and do QuickGen

                    mainWindow = new MainWindow();
                    mainWindow.WindowState = WindowState.Minimized;
                    mainWindow.Show();

                    PerformQuickGen();
                }
            }
            else
            {
                // show main window
                mainWindow = new MainWindow();
                mainWindow.Show();
            }

            CreateWCFServer();  // for receiving QuickGen commands

            /*
            JumpTask task = new JumpTask
            {
                Title = "Check for Updates",
                Arguments = "/update",
                Description = "Checks for Software Updates",
                CustomCategory = "Actions",
                IconResourcePath = Assembly.GetEntryAssembly().CodeBase,
                ApplicationPath = Assembly.GetEntryAssembly().CodeBase
            };

            JumpList jumpList = new JumpList();
            jumpList.JumpItems.Add(task);
            jumpList.ShowFrequentCategory = false;
            jumpList.ShowRecentCategory = false;

            JumpList.SetJumpList(Application.Current, jumpList);
            */
        }

        //--------------------------------------------------

        // fill the CommandLineArgs hashtable
        void ParseCmdline(string[] args)
        {
            if (args.Length == 0)
            {
                //PrintAppInfoAndUsage();
                //Environment.Exit((int)ExitCodes.cmdlineArgError);
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
                else  // argument without value, like "-argname"
                {
                    argName = arg;
                    argValue = "";
                }
                argLongName = GetLongArgOrValueName(argName);
                if (argLongName.Length == 0)
                {
                    ReportErrorAndExit(string.Format("Unknown argument: {0}", arg), ExitCodes.cmdlineArgError);
                }

                // Store command line arg and value
                CommandLineArgs[argLongName] = argValue;
            }
        }

        //--------------------------------------------------

        // fill the pgo struct and initialize PasswordGenerator class with it
        void ProcessCmdline()
        {
            string quickGen = CommandLineArgs["--quickgen"].ToString();
            if (quickGen.Length == 0)
            {
                ReportErrorAndExit("Required argument value is empty: --quickgen", ExitCodes.cmdlineArgError);
            }

            switch (GetLongArgOrValueName(quickGen.Split(':')[0]))
            {
                case "password":
                    quickGenMode = QuickGenMode.password;
                    break;
                case "wpa":
                    quickGenMode = QuickGenMode.wpa;
                    break;
                default:
                    ReportErrorAndExit("Unknown QuickGen mode: --quickgen:" + quickGen.Split(':')[0], ExitCodes.cmdlineArgError);
                    break;
            }

        }

        //--------------------------------------------------

        string GetLongArgOrValueName(string name)
        {
            Hashtable names = new Hashtable();
            names.Add("-qg", "--quickgen");
            names.Add("p", "password");
            names.Add("w", "wpa");

            if (names.ContainsKey(name))
                return (string)names[name];   // convert short name to long name
            else if (names.ContainsValue(name))
                return name; // name is already a long name, no need in conversion
            else
                return "";   // argument or value not found!
        }

        //--------------------------------------------------

        public void PerformQuickGen()
        {
            // perform QuickGen in current process or send a signal to the already running copy?
            if (appIsAlreadyRunning)
            {
                // create WCF client and send QuickGen command to the running app instance

                ChannelFactory<IQuickGen> pipeFactory =
                  new ChannelFactory<IQuickGen>(
                    new NetNamedPipeBinding(),
                    new EndpointAddress(
                      "net.pipe://localhost/AwesomePasswordGenerator/QuickGen"));

                IQuickGen pipeProxy =
                  pipeFactory.CreateChannel();

                pipeProxy.DoQuickGen(quickGenMode);

                // despite IsOneWay = true property, Close() method will wait for server to complete all queued DoQuickGen() methods
                //pipeFactory.Close();
            }
            else
            {
                // perform QuickGen QuickGen in the current process
                //`todo
                
            }
        }

        //--------------------------------------------------

        private void CreateWCFServer()
        {
            ServiceHost host = new ServiceHost(
              typeof(QuickGen),
              new Uri("net.pipe://localhost/AwesomePasswordGenerator"));

            try
            {
                host.AddServiceEndpoint(typeof(IQuickGen),
                    new NetNamedPipeBinding(),
                    "QuickGen");

                /*
                // Step 4 of the hosting procedure: Enable metadata exchange.
                ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                host.Description.Behaviors.Add(smb); 
                */

                host.Open();
            }
            catch (CommunicationException ce)
            {
                MessageBox.Show(String.Format("Can't create WCF server!\n\n{0}", ce.Message),
                    System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                host.Abort();
            }
        }

        //--------------------------------------------------

        void ReportErrorAndExit(string errmsg, ExitCodes exitcode)
        {
            MessageBox.Show(errmsg,
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit((int)exitcode);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Runtime.Serialization;
using Password_Generator;


namespace Awesome_Password_Generator
{
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
            if (App.appInQuickGenMode) return;

            App.appInQuickGenMode = true;
            App.PerformQuickGenLocally(quickgenMode);
            App.appInQuickGenMode = false;
        }
    }

    //---

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow mainWindow;
        public static bool appInQuickGenMode = false;

        //---

        bool appIsAlreadyRunning;
        Mutex mutexApp;
        enum ExitCodes { ok, cmdlineArgError, invalidGenerationSettings };
        Hashtable CommandLineArgs = new Hashtable();    // Indexed command line args
        private QuickGenMode quickGenMode;

        //---

        protected override void OnStartup(StartupEventArgs e)
        {
            // create/open existing mutex to check if app is already running and also for inno setup (see AppMutex)
            mutexApp = new Mutex(false, "Awesome Password Generator", out appIsAlreadyRunning);
            appIsAlreadyRunning = !appIsAlreadyRunning;

            if (e.Args.Count() > 0)
            {
                ParseCmdline(e.Args);
                ProcessCmdline();

                // QuickGen mode
                appInQuickGenMode = true;
                // perform QuickGen in current process or send a signal to the already running copy?
                if (appIsAlreadyRunning)
                {
                    // send command to perform a QuickGen to the remote app and exit
                    PerformQuickGenRemotely();
                    Environment.Exit((int)ExitCodes.ok);
                }
                else
                {
                    // do QuickGen locally and exit
                    mainWindow = new MainWindow();
                    if(!PerformQuickGenLocally(quickGenMode))   // do QuickGen in the current process
                        Shutdown((int)ExitCodes.invalidGenerationSettings);
                    else
                        Shutdown((int)ExitCodes.ok);
                    return;
                }
            }
            else
            {
                // show main window
                mainWindow = new MainWindow();
                mainWindow.Show();
            }

            Task.Factory.StartNew(() => CreateWCFServer());  // for receiving QuickGen commands

            // create jumplist in code (just example)
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

        /// <summary>
        /// create WCF client and send QuickGen command to the running app instance
        /// </summary>
        private void PerformQuickGenRemotely()
        {
            try
            {
                ChannelFactory<IQuickGen> pipeFactory =
                  new ChannelFactory<IQuickGen>(
                    new NetNamedPipeBinding(),
                    new EndpointAddress(
                      "net.pipe://localhost/AwesomePasswordGenerator/QuickGen"));

                IQuickGen pipeProxy =
                  pipeFactory.CreateChannel();
                pipeProxy.DoQuickGen(quickGenMode);
            }
            catch (Exception e)
            {
                // can be if remote server is not available. for example, if another instance of app is running in the QuickGen mode 
                // and busy in this very moment (showing QuickGenInfo window or in the middle of password generation process)
            }
            finally
            {
                // despite IsOneWay = true property, Close() method will wait for server to complete all queued DoQuickGen() methods
                //pipeFactory.Close();
            }
        }

        //--------------------------------------------------

        /// <summary>
        /// do QuickGen in the current process
        /// </summary>
        public static bool PerformQuickGenLocally(QuickGenMode quickgenMode)
        {
            PasswordGenerator pswgen = null;
            
            switch (quickgenMode)
            {
                case QuickGenMode.password:
                    ExecuteAction(new Action(() => mainWindow.PrepareToGeneration(Awesome_Password_Generator.MainWindow.genType.Single, Awesome_Password_Generator.MainWindow.pswType.Password, ref pswgen)));
                    break;
                case QuickGenMode.wpa:
                    ExecuteAction(new Action(() => mainWindow.PrepareToGeneration(Awesome_Password_Generator.MainWindow.genType.Single, Awesome_Password_Generator.MainWindow.pswType.WPA, ref pswgen)));
                    break;
            }

            if (!pswgen.isReady)
            {
                MessageBox.Show(String.Format("Can't generate a password!\n\nRun app normally and check generation settings."),
                    "QuickGen - Awesome Password Generator",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;   // error
            }
            else
            {
                string psw = pswgen.GeneratePassword();
                ExecuteAction(new Action(() => Clipboard.SetText(psw)));

                if (mainWindow.showQuickGenInfoWindow)
                {
                    ExecuteAction(new Action(() => new QuickGenInfo().ShowDialog())); 
                }
                else
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }

            return true;    // ok
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
                // it's a little bit annoying to receive this message every time second copy of the app is launching
                /*
                MessageBox.Show(String.Format("Can't create WCF server!\n\n{0}", ce.Message),
                    System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                */
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

        //--------------------------------------------------

        public static void ExecuteAction(Action a)
        {
            if (mainWindow.Dispatcher.CheckAccess())
                a.Invoke();
            else
                mainWindow.Dispatcher.Invoke(a);
        }
    }
}

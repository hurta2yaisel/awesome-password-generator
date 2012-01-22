using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Windows.Controls;
using System.ComponentModel;
using System.Xml;
using System.Collections;
using Password_Generator;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Shell;
using System.Reflection;


namespace Awesome_Password_Generator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public PasswordGenerator pswgen = null;
        public enum pswType { Password, WPA };
        public enum genType { Single, Bulk };
        public string appPath;

        //---

        public bool showQuickGenInfoWindow = true;

        //---

        private bool initializeComponentIsCompleted = false;   // will be TRUE after InitializeComponent()

        private bool disableExpandersEvents = false;
        private bool lockExpanders = false;
        private pswType lastExpanded_PswType;
        private genType lastExpanded_GenType;

        private string fileName; // for bulk generation
        private bool appendToFile;   // or replace if FALSE
        string[] bulkPasswords;
        BackgroundWorker bgworker = new BackgroundWorker();

        string cfgFileName;
        bool portableMode;

        private int monospaceFontSizeBig = 40, monospaceFontSizeMedium = 30, monospaceFontSizeSmall = 20;

        //--------------------------------------------------
        //--------------------------------------------------

        public MainWindow()
        {
            InitializeComponent();
            initializeComponentIsCompleted = true;

            // deal with fonts
            bool segoeUIFontisPresent = false, consolasFontIsPresent = false;
            foreach (FontFamily fontFamily in Fonts.SystemFontFamilies)
            {
                // FontFamily.Source contains the font family name.
                if (fontFamily.Source == "Segoe UI") segoeUIFontisPresent = true;
                if (fontFamily.Source == "Consolas") consolasFontIsPresent = true;
            }
            if(!segoeUIFontisPresent)
                foreach (object control in FindAllChildren(this))
                    if (control is Separator && control.GetType().GetProperty("Tag") != null && ((Separator)control).Tag != null && ((Separator)control).Tag.ToString() == "NotSegoeUIFontCompensator")
                        ((Separator)control).Visibility = Visibility.Hidden;
            if (!consolasFontIsPresent)
            {
                monospaceFontSizeBig = 40; monospaceFontSizeMedium = 29; monospaceFontSizeSmall = 18;
            }

            // check if applicaton runs in portable mode and fill some global variables
            appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            portableMode = System.IO.File.Exists(appPath + "\\portable");
            if (portableMode)
            {
                cfgFileName = appPath + "\\config.xml";
                txtBulkFile.Text = appPath + "\\passwords.txt";
            }
            else
            {
                string appdataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Awesome Password Generator";
                cfgFileName = appdataDir + "\\config.xml";
                if (!System.IO.Directory.Exists(appdataDir))
                    System.IO.Directory.CreateDirectory(appdataDir);
                txtBulkFile.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\passwords.txt";
            }

            // load configuration and prepare GUI
            LoadConfig();
            cmdRegenerate_Click(null, null);    // generate password
            GenerateCommandLines();

            // initialize background worker for bulk generation
            bgworker.WorkerReportsProgress = true;
            bgworker.WorkerSupportsCancellation = true;
            bgworker.DoWork += bgworker_DoWork;
            bgworker.ProgressChanged += bgworker_ProgressChanged;
            bgworker.RunWorkerCompleted += bgworker_RunWorkerCompleted;

            // show version info
            this.Title = Assembly.GetExecutingAssembly().GetName().Name + " " +
                Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Build.ToString();
            lblAboutVersion.Content = this.Title + " build " +
                Assembly.GetExecutingAssembly().GetName().Version.MinorRevision.ToString();
            // Get all Copyright attributes on this assembly
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            lblAboutCopyright.Content = "Copyright " + ((AssemblyCopyrightAttribute)attributes[0]).Copyright.Replace(" __alex", " ____alex");
        }

        //--------------------------------------------------

        public static IEnumerable<Object> FindAllChildren(DependencyObject depObj)
        {
            if (depObj != null)
                foreach (object child in LogicalTreeHelper.GetChildren(depObj))
                    // check if object is control, not the string
                    if (child != null && child.GetType().IsSubclassOf(typeof(System.Windows.UIElement)) == true)
                    {
                        yield return child;

                        foreach (Object childOfChild in FindAllChildren((DependencyObject)child))
                        {
                            yield return childOfChild;
                        }
                    }
        }

        //--------------------------------------------------

        /// <summary>
        /// Finds a Child of a given item in the logical tree. 
        /// </summary>
        /// <param name="parent">A direct parent of the queried item.</param>
        /// <param name="childName">x:Name or Name of child. </param>
        /// <returns>The first parent item that matches the submitted type parameter. 
        /// If not matching item can be found, a null parent is being returned.</returns>
        public static object FindChild(DependencyObject parent, string childName)
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                // check if object is control, not the string
                if (child != null && child.GetType().IsSubclassOf(typeof(System.Windows.UIElement)) == true)
                {
                    if (child.GetType().GetProperty("Name") != null && ((FrameworkElement)child).Name == childName)
                        return child;   // match!

                    object childOfChild = FindChild((DependencyObject)child, childName);
                    if (childOfChild != null)
                        return childOfChild;    // match!
                }
            }

            return null;    // not found (
        }

        //--------------------------------------------------

        private void LoadConfig()
        {
            try
            {
                using (XmlReader reader = XmlReader.Create(cfgFileName, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true }))
                {
                    reader.Read();  // skip declatation

                    // check configuration file version
                    reader.Read();
                    string cfgVerOfCurrentFile = reader.GetAttribute("Version");
                    string cfgVerCorrect = "2.0";
                    string[] cfgVerSupported = new string[] { "1.0" };  // try to read such cfgfiles anyway
                    // accept configuration file only with correct major version (different minor versions are allowed though)
                    if ((cfgVerOfCurrentFile.Split('.')[0] != cfgVerCorrect.Split('.')[0]) && !cfgVerSupported.Contains(cfgVerOfCurrentFile))
                    {
                        throw new Exception(String.Format("WARNING: Configuration file version is invalid (must be {0}, not {1}), and all settings will be reset to defaults!", cfgVerCorrect, cfgVerOfCurrentFile));
                    }

                    while (true)
                    {
                        reader.Read();
                        if (reader.Name == "Config" && reader.NodeType == XmlNodeType.EndElement) break;  // end of config

                        switch (reader.Name)
                        {
                            case "MainWindowPosition":
                                // restore main window position
                                this.Top = Double.Parse(reader.GetAttribute("Top"));
                                this.Left = Double.Parse(reader.GetAttribute("Left"));
                                break;
                            case "QuickGen":
                                showQuickGenInfoWindow = bool.Parse(reader.GetAttribute("ShowQuickGenInfoWindow"));
                                break;
                            case "Controls":
                                // restore controls status
                                while (true)
                                {
                                    reader.Read();
                                    if (reader.Name == "Controls" && reader.NodeType == XmlNodeType.EndElement) break;  // end of controls

                                    if (reader.NodeType == XmlNodeType.Element)
                                    {
                                        object o = FindChild(this, reader.GetAttribute("Name"));
                                        if (o != null)  // skip unknown controls
                                        {
                                            string s = o.GetType().ToString();
                                            switch (o.GetType().Name)
                                            {
                                                case "Expander":
                                                    ((Expander)o).IsExpanded = bool.Parse(reader.GetAttribute("IsExpanded"));
                                                    break;
                                                case "IntegerUpDown":
                                                    ((IntegerUpDown)o).Value = int.Parse(reader.GetAttribute("Value"));
                                                    break;
                                                case "CheckBox":
                                                    ((CheckBox)o).IsChecked = bool.Parse(reader.GetAttribute("IsChecked"));
                                                    break;
                                                case "TextBox":
                                                    ((TextBox)o).Text = reader.GetAttribute("Text");
                                                    break;
                                                case "WatermarkTextBox":
                                                    ((WatermarkTextBox)o).Text = reader.GetAttribute("Text");
                                                    break;
                                                case "RadioButton":
                                                    ((RadioButton)o).IsChecked = bool.Parse(reader.GetAttribute("IsChecked"));
                                                    break;
                                                case "Slider":
                                                    ((Slider)o).Value = int.Parse(reader.GetAttribute("Value"));
                                                    break;
                                            }
                                        }
                                    }
                                }
                                break;
                            default:
                                throw new Exception(String.Format("WARNING: Configuration file is invalid or damaged! Check your settings - they can be reset to defaults!"));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                //e.Message.ToString()
            }
        }

        //--------------------------------------------------

        private void SaveConfig()
        {
            try
            {
                // do not save the state of this contols in config file
                string[] controlsExclusions = new string[] { "txtResult", "txtCmdlineLong", "txtCmdlineShort" };

                using (XmlWriter writer = XmlWriter.Create(cfgFileName, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true }))
                {
                    writer.WriteStartElement("Config");
                    writer.WriteAttributeString("Version", "2.0");

                    // save main window position
                    writer.WriteStartElement("MainWindowPosition");
                    writer.WriteAttributeString("Top", this.Top.ToString());
                    writer.WriteAttributeString("Left", this.Left.ToString());
                    writer.WriteEndElement();

                    // save QuickGen settings
                    writer.WriteStartElement("QuickGen");
                    writer.WriteAttributeString("ShowQuickGenInfoWindow", showQuickGenInfoWindow.ToString());
                    writer.WriteEndElement();

                    // save controls state
                    writer.WriteStartElement("Controls");

                    foreach (object exp in FindAllChildren(this))
                    {
                        if (exp.GetType().GetProperty("Name") != null)
                            if (exp is Expander && !controlsExclusions.Contains(((FrameworkElement)exp).Name))
                            {
                                writer.WriteStartElement("Expander");
                                writer.WriteAttributeString("Name", ((FrameworkElement)exp).Name);
                                writer.WriteAttributeString("IsExpanded", ((Expander)exp).IsExpanded.ToString());

                                foreach (object ctrl in FindAllChildren((Expander)exp))
                                    if (!controlsExclusions.Contains(((FrameworkElement)ctrl).Name))
                                        switch (ctrl.GetType().Name)
                                        {
                                            case "IntegerUpDown":
                                                writer.WriteStartElement("Control");
                                                writer.WriteAttributeString("Name", ((FrameworkElement)ctrl).Name);
                                                writer.WriteAttributeString("Value", ((IntegerUpDown)ctrl).Value.ToString());
                                                writer.WriteEndElement();
                                                break;
                                            case "CheckBox":
                                                writer.WriteStartElement("Control");
                                                writer.WriteAttributeString("Name", ((FrameworkElement)ctrl).Name);
                                                writer.WriteAttributeString("IsChecked", ((CheckBox)ctrl).IsChecked.ToString());
                                                writer.WriteEndElement();
                                                break;
                                            case "TextBox":
                                                writer.WriteStartElement("Control");
                                                writer.WriteAttributeString("Name", ((FrameworkElement)ctrl).Name);
                                                writer.WriteAttributeString("Text", ((TextBox)ctrl).Text);
                                                writer.WriteEndElement();
                                                break;
                                            case "WatermarkTextBox":
                                                writer.WriteStartElement("Control");
                                                writer.WriteAttributeString("Name", ((FrameworkElement)ctrl).Name);
                                                writer.WriteAttributeString("Text", ((WatermarkTextBox)ctrl).Text);
                                                writer.WriteEndElement();
                                                break;
                                            case "RadioButton":
                                                writer.WriteStartElement("Control");
                                                writer.WriteAttributeString("Name", ((FrameworkElement)ctrl).Name);
                                                writer.WriteAttributeString("IsChecked", ((RadioButton)ctrl).IsChecked.ToString());
                                                writer.WriteEndElement();
                                                break;
                                            case "Slider":
                                                writer.WriteStartElement("Control");
                                                writer.WriteAttributeString("Name", ((FrameworkElement)ctrl).Name);
                                                writer.WriteAttributeString("Value", ((Slider)ctrl).Value.ToString());
                                                writer.WriteEndElement();
                                                break;
                                        }

                                writer.WriteEndElement();   // </Expander>
                            }
                    }

                    writer.WriteEndElement();   // </Controls>

                    writer.WriteEndElement();   // </Config>
                    writer.WriteEndDocument();
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                if (!portableMode)
                    System.Windows.MessageBox.Show("ERROR: Can't save configuration!\n\n" + e.Message.ToString(),
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //--------------------------------------------------

        public void PrepareToGeneration(genType gt, pswType pt, ref PasswordGenerator pg)
        {
            PasswordGenerator.PasswordGenerationOptions pgo = new PasswordGenerator.PasswordGenerationOptions();

            // options
            switch (gt)
            {
                case genType.Single:
                    pgo.easyToType = (bool)chkSingle_EasyToType.IsChecked;
                    pgo.excludeConfusing = (bool)chkSingle_ExcludeConfusingChars.IsChecked;
                    pgo.quantity = 1;
                    break;
                case genType.Bulk:
                    pgo.easyToType = (bool)chkBulkEasyToType.IsChecked;
                    pgo.excludeConfusing = (bool)chkBulkExcludeConfusingChars.IsChecked;
                    pgo.quantity = udBulkQuantity.Value ?? 0;
                    fileName = txtBulkFile.Text;
                    appendToFile = rbBulkAppend.IsChecked ?? true;
                    break;
                default:
                    pgo.pswLength = 0;  // mark structure as invalid
                    return;   // error
            }

            // fill the pgo.charsets array
            pgo.charsets = new string[0];

            switch (pt)
            {
                case pswType.Password:
                    pgo.pswLength = udPswLength.Value ?? 0;

                    // search for all checkboxes in the expPassword expander
                    foreach (object ctrl in FindAllChildren(expPassword))
                        if (ctrl.GetType().Name == "CheckBox" && (bool)((CheckBox)ctrl).IsChecked && ((CheckBox)ctrl).Tag != null)
                        {
                            Array.Resize(ref pgo.charsets, pgo.charsets.Length + 1);
                            pgo.charsets[pgo.charsets.Length - 1] = (string)((CheckBox)ctrl).Tag;
                        }

                    // user defined charset
                    if ((bool)chkPswUserDefinedCharacters.IsChecked)
                        pgo.userDefinedCharset = txtPswUserDefinedCharacters.Text;

                    break;
                case pswType.WPA:
                    if ((bool)rbWpaPassphrase.IsChecked)
                    {
                        pgo.pswLength = udWpaLength.Value ?? 0;

                        // search for all checkboxes in the expWPA expander
                        foreach (object ctrl in FindAllChildren(expWPA))
                            if (ctrl.GetType().Name == "CheckBox" && (bool)((CheckBox)ctrl).IsChecked && ((CheckBox)ctrl).Tag != null)
                            {
                                Array.Resize(ref pgo.charsets, pgo.charsets.Length + 1);
                                pgo.charsets[pgo.charsets.Length - 1] = (string)((CheckBox)ctrl).Tag;
                            }

                            // user defined charset
                        if ((bool)chkWpaUserDefinedCharacters.IsChecked)
                            pgo.userDefinedCharset = txtWpaUserDefinedCharacters.Text;
                    }
                    else  // rbWpa256bitKey is checked
                    {
                        pgo.pswLength = 64;

                        // use only one charset
                        pgo.charsets = new string[1];
                        pgo.charsets[0] = "hex";
                    }
                    break;
                default:
                    pgo.pswLength = 0;
                    return; // some error
            }
            
            pg = new PasswordGenerator(pgo);    // create and initialize the password generator class
        }

        //--------------------------------------------------

        private void cmdRegenerate_Click(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            PrepareToGeneration(lastExpanded_GenType, lastExpanded_PswType, ref pswgen);
            string psw = pswgen.GeneratePassword();

            // split long passwords
            string pswFormatted = "";
            int pos = 0;
            while (pos < psw.Length)
            {
                if (pos != 0) pswFormatted += "\n";
                pswFormatted += psw.Substring(pos, Math.Min(32, psw.Length - pos));
                pos += 32;
            }

            txtResult.Text = pswFormatted;
            if (expSingleGeneration.IsExpanded && (bool)chkCopyToClipboardAutomatically.IsChecked && !App.appInQuickGenMode)
                Clipboard.SetText(psw);
            
            // display password strength
            Brush[] brushes = new Brush[] { Brushes.LightPink, new SolidColorBrush(Color.FromArgb(255, 255, 255, 128)), new SolidColorBrush(Color.FromArgb(255, 200, 255, 200)), Brushes.LawnGreen };
            if (pswgen.isReady)
            {
                lblStrength.Content = "Strength: " + pswgen.PasswordStrength.ToString().ToUpper();
                lblStrength.Background = brushes[(int)pswgen.PasswordStrength];
            }
            else
            {
                lblStrength.Content = "Strength: n/a";
                lblStrength.Background = Brushes.Gray;
            }
        }

        //--------------------------------------------------

        private void txtResult_TextChanged(object sender, TextChangedEventArgs e)
        {
            // adjust font size
            if (txtResult.Text.Length <= 14)
                txtResult.FontSize = monospaceFontSizeBig;
            else if (txtResult.Text.Length <= 20)
                txtResult.FontSize = monospaceFontSizeMedium;
            else
                txtResult.FontSize = monospaceFontSizeSmall;
        }

        //--------------------------------------------------

        private void cmdCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtResult.Text.Replace("\n", ""));
        }

        //--------------------------------------------------

        private void Window_Closed(object sender, EventArgs e)
        {
            if ((bool)chkClearClipboardOnExit.IsChecked && !App.appInQuickGenMode)
                // clear clipboard only if it contains a password
                if (Clipboard.GetText() == txtResult.Text.Replace("\n", ""))
                    Clipboard.SetText("");

            SaveConfig();
        }

        //--------------------------------------------------

        #region expanders

        private void expPassword_Expanded(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expPassword.IsExpanded = false;
                disableExpandersEvents = false;
                return;
            }

            lastExpanded_PswType = pswType.Password;
            expWPA.IsExpanded = false;
            if (!expSingleGeneration.IsExpanded && !expBulkGeneration.IsExpanded)
                switch (lastExpanded_GenType)
                {
                    case genType.Single:
                        expSingleGeneration.IsExpanded = true;
                        break;
                    case genType.Bulk:
                        expBulkGeneration.IsExpanded = true;
                        break;
                }
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            cmdRegenerate_Click(null, null);    // generate password

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expWPA_Expanded(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expWPA.IsExpanded = false;
                disableExpandersEvents = false;
                return;
            }

            lastExpanded_PswType = pswType.WPA;
            expPassword.IsExpanded = false;
            if (!expSingleGeneration.IsExpanded && !expBulkGeneration.IsExpanded)
                switch (lastExpanded_GenType)
                {
                    case genType.Single:
                        expSingleGeneration.IsExpanded = true;
                        break;
                    case genType.Bulk:
                        expBulkGeneration.IsExpanded = true;
                        break;
                }
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            cmdRegenerate_Click(null, null);    // generate password

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expSingleGeneration_Expanded(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expSingleGeneration.IsExpanded = false;
                disableExpandersEvents = false;
                return;
            }

            lastExpanded_GenType = genType.Single;
            if (expPassword.IsExpanded == false && expWPA.IsExpanded == false)
                switch (lastExpanded_PswType)
                {
                    case pswType.Password:
                        expPassword.IsExpanded = true;
                        break;
                    case pswType.WPA:
                        expWPA.IsExpanded = true;
                        break;
                }
            expBulkGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expBulkGeneration_Expanded(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expBulkGeneration.IsExpanded = false;
                disableExpandersEvents = false;
                return;
            }

            lastExpanded_GenType = genType.Bulk;
            if (expPassword.IsExpanded == false && expWPA.IsExpanded == false)
                switch (lastExpanded_PswType)
                {
                    case pswType.Password:
                        expPassword.IsExpanded = true;
                        break;
                    case pswType.WPA:
                        expWPA.IsExpanded = true;
                        break;
                }
            expSingleGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expCmdline_Expanded(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expCmdline.IsExpanded = false;
                disableExpandersEvents = false;
                return;
            }

            expPassword.IsExpanded = false;
            expWPA.IsExpanded = false;
            expSingleGeneration.IsExpanded = false;
            expBulkGeneration.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;

            GenerateCommandLines();
        }

        //--------------------------------------------------

        private void expAbout_Expanded(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expAbout.IsExpanded = false;
                disableExpandersEvents = false;
                return;
            }

            expPassword.IsExpanded = false;
            expWPA.IsExpanded = false;
            expSingleGeneration.IsExpanded = false;
            expBulkGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expPassword_Collapsed(object sender, RoutedEventArgs e)
        {
            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expPassword.IsExpanded = true;
                disableExpandersEvents = false;
                return;
            }

            expWPA.IsExpanded = false;
            expSingleGeneration.IsExpanded = false;
            expBulkGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expWPA_Collapsed(object sender, RoutedEventArgs e)
        {
            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expWPA.IsExpanded = true;
                disableExpandersEvents = false;
                return;
            }

            expPassword.IsExpanded = false;
            expSingleGeneration.IsExpanded = false;
            expBulkGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expSingleGeneration_Collapsed(object sender, RoutedEventArgs e)
        {
            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expSingleGeneration.IsExpanded = true;
                disableExpandersEvents = false;
                return;
            }

            expPassword.IsExpanded = false;
            expWPA.IsExpanded = false;
            expBulkGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expBulkGeneration_Collapsed(object sender, RoutedEventArgs e)
        {
            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expBulkGeneration.IsExpanded = true;
                disableExpandersEvents = false;
                return;
            }

            expPassword.IsExpanded = false;
            expWPA.IsExpanded = false;
            expSingleGeneration.IsExpanded = false;
            expCmdline.IsExpanded = false;
            expAbout.IsExpanded = false;

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expCmdline_Collapsed(object sender, RoutedEventArgs e)
        {
            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expBulkGeneration.IsExpanded = true;
                disableExpandersEvents = false;
                return;
            }

            switch (lastExpanded_PswType)
            {
                case pswType.Password:
                    expPassword.IsExpanded = true;
                    break;
                case pswType.WPA:
                    expWPA.IsExpanded = true;
                    break;
            }
            switch (lastExpanded_GenType)
            {
                case genType.Single:
                    expSingleGeneration.IsExpanded = true;
                    break;
                case genType.Bulk:
                    expBulkGeneration.IsExpanded = true;
                    break;
            }

            disableExpandersEvents = false;
        }

        //--------------------------------------------------

        private void expAbout_Collapsed(object sender, RoutedEventArgs e)
        {
            if (disableExpandersEvents) return;
            disableExpandersEvents = true;

            if (lockExpanders)
            {
                expAbout.IsExpanded = true;
                disableExpandersEvents = false;
                return;
            }

            switch (lastExpanded_PswType)
            {
                case pswType.Password:
                    expPassword.IsExpanded = true;
                    break;
                case pswType.WPA:
                    expWPA.IsExpanded = true;
                    break;
            }
            switch (lastExpanded_GenType)
            {
                case genType.Single:
                    expSingleGeneration.IsExpanded = true;
                    break;
                case genType.Bulk:
                    expBulkGeneration.IsExpanded = true;
                    break;
            }

            disableExpandersEvents = false;
        }

        #endregion  // expanders

        //--------------------------------------------------

        private void GenerateCommandLines()
        {
            if (pswgen == null || !pswgen.isReady) return; // invalid settings

            string clLong = "";
            string clShort = "";

            // quantity
            switch (lastExpanded_GenType)
            {
                case genType.Single:
                    clLong += "--quantity:1 ";
                    clShort += "-q:1 ";
                    break;
                case genType.Bulk:
                    clLong += string.Format("--quantity:{0} ", udBulkQuantity.Value);
                    clShort += string.Format("-q:{0} ", udBulkQuantity.Value);
                    break;
            }

            // length
            switch (lastExpanded_PswType)
            {
                case pswType.Password:
                    clLong += string.Format("--length:{0} ", udPswLength.Value);
                    clShort += string.Format("-l:{0} ", udPswLength.Value);
                    break;
                case pswType.WPA:
                    if ((bool)rbWpaPassphrase.IsChecked)
                    {
                        clLong += string.Format("--length:{0} ", udWpaLength.Value);
                        clShort += string.Format("-l:{0} ", udWpaLength.Value);
                    }
                    else // 256 bit WPA key
                    {
                        clLong += "--length:64 ";
                        clShort += "-l:64 ";
                    }
                    break;
            }

            // charsets
            clLong += "\"--charsets:";
            clShort += "\"-c:";
            int c;
            switch (lastExpanded_PswType)
            {
                case pswType.Password:
                    // search for all checkboxes in the expPassword expander
                    c = 0;
                    foreach (object ctrl in FindAllChildren(expPassword))
                        if (ctrl.GetType().Name == "CheckBox" && (bool)((CheckBox)ctrl).IsChecked && ((CheckBox)ctrl).Tag != null)
                        {
                            clLong += (c != 0 ? "," : "") + (string)((CheckBox)ctrl).Tag;
                            clShort += (c != 0 ? "," : "") + (string)((CheckBox)ctrl).Tag;
                            c++;
                        }
                    break;
                case pswType.WPA:
                    if ((bool)rbWpaPassphrase.IsChecked)
                    {
                        // search for all checkboxes in the expWPA expander
                        c = 0;
                        foreach (object ctrl in FindAllChildren(expWPA))
                            if (ctrl.GetType().Name == "CheckBox" && (bool)((CheckBox)ctrl).IsChecked && ((CheckBox)ctrl).Tag != null)
                            {
                                clLong += (c != 0 ? "," : "") + (string)((CheckBox)ctrl).Tag;
                                clShort += (c != 0 ? "," : "") + (string)((CheckBox)ctrl).Tag;
                                c++;
                            }
                    }
                    else // 256 bit WPA key
                    {
                        clLong += "hex";
                        clShort += "hex";
                    }
                    break;
            }
            clLong += "\" ";
            clShort += "\" ";

            // user defined charset
            switch (lastExpanded_PswType)
            {
                case pswType.Password:
                    if ((bool)chkPswUserDefinedCharacters.IsChecked)
                    {
                        clLong += "\"--userDefinedCharset:" + txtPswUserDefinedCharacters.Text + "\" ";
                        clShort += "\"-udc:" + txtPswUserDefinedCharacters.Text + "\" ";
                    }
                    break;
                case pswType.WPA:
                    if ((bool)rbWpaPassphrase.IsChecked)
                    {
                        if ((bool)chkWpaUserDefinedCharacters.IsChecked)
                        {
                            clLong += "\"--userDefinedCharset:" + txtWpaUserDefinedCharacters.Text + "\" ";
                            clShort += "\"-udc:" + txtWpaUserDefinedCharacters.Text + "\" ";
                        }
                    }
                    break;
            }

            // easy to type
            switch (lastExpanded_GenType)
            {
                case genType.Single:
                    if ((bool)chkSingle_EasyToType.IsChecked)
                    {
                        clLong += "--easytotype ";
                        clShort += "-ett ";
                    }
                    break;
                case genType.Bulk:
                    if ((bool)chkBulkEasyToType.IsChecked)
                    {
                        clLong += "--easytotype ";
                        clShort += "-ett ";
                    }
                    break;
            }

            // exclude confusing characters
            switch (lastExpanded_GenType)
            {
                case genType.Single:
                    if ((bool)chkSingle_ExcludeConfusingChars.IsChecked)
                    {
                        clLong += "--excludeConfusingCharacters ";
                        clShort += "-ecc ";
                    }
                    break;
                case genType.Bulk:
                    if ((bool)chkBulkExcludeConfusingChars.IsChecked)
                    {
                        clLong += "--excludeConfusingCharacters ";
                        clShort += "-ecc ";
                    }
                    break;
            }

            // destination
            if ((bool)cmdCmdlineFile.IsChecked)
            {
                clLong += "\"--destination:file:";
                clShort += "\"-d:f:";
                if ((bool)rbBulkAppend.IsChecked)
                {
                    clLong += "append:";
                    clShort += "a:";
                }
                else
                {
                    clLong += "replace:";
                    clShort += "r:";
                }
                clLong += txtBulkFile.Text + "\"";
                clShort += txtBulkFile.Text + "\"";
            }
            else if ((bool)cmdCmdlineConsole.IsChecked)
            {
                clLong += "--destination:console";
                clShort += "-d:c";
            }

            txtCmdlineLong.Text = clLong;
            txtCmdlineShort.Text = clShort;
        }

        //--------------------------------------------------

        private void cmdBulkStart_Click(object sender, RoutedEventArgs e)
        {
            PrepareToGeneration(lastExpanded_GenType, lastExpanded_PswType, ref pswgen);
            PasswordGenerator.PasswordGenerationOptions pgo = pswgen.PGO;
            string[] workCharsets = pswgen.WorkCharsets;

            if (pswgen == null){
                System.Media.SystemSounds.Hand.Play();
                return;
            }
            if(!pswgen.isReady)
            {
                if (workCharsets.Length == 0)
                {
                    System.Windows.MessageBox.Show("ERROR: Can't generate passwords from nothing!\n\nSelect one or several charsets (like A..Z or 0..9) and try again.",
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    // some error
                    System.Media.SystemSounds.Hand.Play();
                }
                return; // class is not ready to passwords generation
            }
            if (fileName.Length == 0)
            {
                System.Windows.MessageBox.Show("ERROR: Location for generated passwords is not chosen yet!\n\nSpecify the file in which you want to save passwords and try again.",
                    System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // allocate memory
            try
            {
                bulkPasswords = new string[pgo.quantity];
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(string.Format("ERROR: Can't allocate memory!\n\n{0}",
                    exception.Message.ToString()), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // disable controls
            lockExpanders = true;   // prevent all expanders from expanding/collapsing
            expPassword.IsEnabled = false;
            expWPA.IsEnabled = false;
            cmdBulkStart.IsEnabled = false;
            cmdBulkStop.IsEnabled = true;

            txtBulkFile.IsEnabled = false;
            cmdBulkBrowse.IsEnabled = false;
            rbBulkAppend.IsEnabled = false;
            rbBulkReplace.IsEnabled = false;
            udBulkQuantity.IsEnabled = false;
            chkBulkEasyToType.IsEnabled = false;
            chkBulkExcludeConfusingChars.IsEnabled = false;

            pbBulkProgressBar.Value = 0;
            pbBulkProgressBar.Visibility = Visibility.Visible;

            TaskbarItemInfo.ProgressValue = 0;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

            bgworker.RunWorkerAsync(pgo);  // generate passwords in background
        }

        //--------------------------------------------------

        private void cmdBulkStop_Click(object sender, RoutedEventArgs e)
        {
            bgworker.CancelAsync();
        }

        //--------------------------------------------------

        #region bgworker

        private void bgworker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            BackgroundWorker worker = sender as BackgroundWorker;

            PasswordGenerator.PasswordGenerationOptions pgo = (PasswordGenerator.PasswordGenerationOptions)e.Argument;

            // generate pgo.quantity passwords
            int lastWholePercent = 0, curWholePercent;
            string psw;
            for (int i = 1; i <= pgo.quantity; i++)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }

                psw = pswgen.GeneratePassword();
                bulkPasswords[i - 1] = psw;

                // update progress bar (if needed)
                curWholePercent = (int)Math.Round((double)i * (double)99 / (double)pgo.quantity, MidpointRounding.AwayFromZero);
                if (lastWholePercent != curWholePercent)
                {
                    worker.ReportProgress(curWholePercent);
                    lastWholePercent = curWholePercent;
                }
            }

            // save passwords in file
            try
            {
                if (appendToFile)
                    System.IO.File.AppendAllLines(fileName, bulkPasswords);
                else
                    System.IO.File.WriteAllLines(fileName, bulkPasswords);
            }
            catch
            {
                throw;  // see bgworker_RunWorkerCompleted()
            }

            worker.ReportProgress(100);
        }

        //--------------------------------------------------

        private void bgworker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbBulkProgressBar.Value = e.ProgressPercentage;
            TaskbarItemInfo.ProgressValue = (double)e.ProgressPercentage / 100;
        }

        //--------------------------------------------------

        private void bgworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
            }
            else if (e.Error != null)
            {
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;

                System.Windows.MessageBox.Show(string.Format("ERROR: Can't save generated passwords to file!\n\n{0}",
                    e.Error.Message), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // normal completion
            }

            pbBulkProgressBar.Visibility = Visibility.Hidden;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;

            txtBulkFile.IsEnabled = true;
            cmdBulkBrowse.IsEnabled = true;
            rbBulkAppend.IsEnabled = true;
            rbBulkReplace.IsEnabled = true;
            udBulkQuantity.IsEnabled = true;
            chkBulkEasyToType.IsEnabled = true;
            chkBulkExcludeConfusingChars.IsEnabled = true;

            cmdBulkStop.IsEnabled = false;
            cmdBulkStart.IsEnabled = true;
            expPassword.IsEnabled = true;
            expWPA.IsEnabled = true;
            lockExpanders = false;

            //System.Media.SystemSounds.Beep.Play();
        }

        #endregion  // bgworker

        //--------------------------------------------------

        private void udPswLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            cmdRegenerate_Click(null, null);
        }

        //--------------------------------------------------

        private void txtPswUserDefinedCharacters_TextChanged(object sender, TextChangedEventArgs e)
        {
            cmdRegenerate_Click(null, null);
        }

        //--------------------------------------------------

        private void cmdBulkBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Configure save file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            try
            {
                string initDir = System.IO.Path.GetDirectoryName(txtBulkFile.Text);
                if (initDir.Length != 0)
                    dlg.InitialDirectory = initDir; // open dialog in the last visited directory
            }
            catch { }   // ignore any exceprions
            dlg.DefaultExt = ".txt"; // Default file extension
            dlg.Filter = "Text documents|*.txt|All Files|*"; // Filter files by extension

            // Show save file dialog box
            bool? result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                txtBulkFile.Text = dlg.FileName;
            }
        }

        //--------------------------------------------------

        private void cmdBulkStart_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // change picture on the button
            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            if ((bool)e.NewValue)
                logo.UriSource = new Uri("res\\Play_4_x48.ico", UriKind.Relative);
            else
                logo.UriSource = new Uri("res\\Play_4_Gray_x48.ico", UriKind.Relative);
            logo.EndInit();
            imgBulkStart.Source = logo;
        }

        //--------------------------------------------------

        private void Image_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // change picture on the button
            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            if ((bool)e.NewValue)
                logo.UriSource = new Uri("res\\Stop Alt_x48.ico", UriKind.Relative);
            else
                logo.UriSource = new Uri("res\\Stop Alt_Gray_x48.ico", UriKind.Relative);
            logo.EndInit();
            imgBulkStop.Source = logo;
        }

        //--------------------------------------------------

        private void cmdCmdlineCopyLong_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtCmdlineLong.Text);
        }

        //--------------------------------------------------

        private void cmdCmdlineCopyShort_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtCmdlineShort.Text);
        }

        //--------------------------------------------------

        private void cmdCmdlineFile_Checked(object sender, RoutedEventArgs e)
        {
            GenerateCommandLines();
        }

        //--------------------------------------------------

        private void cmdCmdlineConsole_Checked(object sender, RoutedEventArgs e)
        {
            GenerateCommandLines();
        }

        //--------------------------------------------------

        private void txtWpaUserDefinedCharacters_TextChanged(object sender, TextChangedEventArgs e)
        {
            cmdRegenerate_Click(null, null);
        }

        //--------------------------------------------------

        public void hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri.IsAbsoluteUri)
            {
                // open a webpage

                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            }
            else
            {
                // open a file

                string eula = appPath + "\\" + e.Uri.OriginalString;
                if (!File.Exists(eula))
                {
                    System.Windows.MessageBox.Show("ERROR: License file \"" + e.Uri.OriginalString + "\" is missing!",
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().Name,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo(eula));
            }

            e.Handled = true;
        }

        //--------------------------------------------------

        private void rbWpa256bitKey_Checked(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            spWpaPassphrase.IsEnabled = false;

            cmdRegenerate_Click(null, null);
        }

        //--------------------------------------------------

        private void rbWpaPassphrase_Checked(object sender, RoutedEventArgs e)
        {
            if (!initializeComponentIsCompleted) return;

            spWpaPassphrase.IsEnabled = true;

            cmdRegenerate_Click(null, null);
        }

        //--------------------------------------------------
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections;


namespace Password_Generator
{
    public class PasswordGenerator
    {
        public Hashtable charsets = new Hashtable();
        public struct charset
        {
            public string rangeName;    // like "A..Z"
            public string symbols; // like "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            public int easiness;   // using in generating easy-to-type passwords

            // constructor
            public charset(string rangeName, string symbols, int easiness)
            {
                this.rangeName = rangeName;
                this.symbols = symbols;
                this.easiness = easiness;
            }
        }
        
        private RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        public struct PasswordGenerationOptions
        {
            public int pswLength; // password length. 0 means that struct is invalid.
            public int quantity;    // how many passwords needs to be generated (in bulk mode only)
            public string[] charsets;
            public string userDefinedCharset;
            public bool excludeConfusing;   // exclude confusing characters from the password
            public bool easyToType;     // generate easy-to-type password
        }
        private PasswordGenerationOptions pgo = new PasswordGenerationOptions();

        public bool isReady = false;    // true if class is ready to generate passwords

        private string[] workCharsets;

        public enum enumPasswordStrength { weak, normal, good, excellent };
        private enumPasswordStrength passwordStrength;
        

        //--------------------------------------------------
        //--------------------------------------------------

        private void FillCharsets()
        {
            // fill charsets
            charsets.Add("hex", new charset("", "0123456789ABCDEF", 0));
            charsets.Add("special", new charset("!@#...", "!@#$%^&*()~-_=+\\|/[]{};:`'\",.<>?", 0));
            charsets.Add("digits", new charset("0..9", "0123456789", 50));
            charsets.Add("userDefined", new charset("", "", 20));
            charsets.Add("-", new charset("", " |`'018", -1)); // confusing characters

            charsets.Add("enU", new charset("A..Z", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 90));
            charsets.Add("enL", new charset("a..z", "abcdefghijklmnopqrstuvwxyz", 100));
            charsets.Add("en-", new charset("", "iljoBJIO", -1)); // confusing characters

            charsets.Add("ruU", new charset("А..Я", "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ", 70));
            charsets.Add("ruL", new charset("а..я", "абвгдеёжзийклмнопрстуфхцчшщъыьэюя", 80));
            charsets.Add("ru-", new charset("", "обзэОВЗЭ", -1));
        }

        //--------------------------------------------------

        // constructor
        public PasswordGenerator()
        {
            FillCharsets();
        }

        //--------------------------------------------------

        // constructor
        public PasswordGenerator(PasswordGenerationOptions pgo)
        {
            this.pgo = pgo;

            FillCharsets();

            Init();
        }

        //--------------------------------------------------

        // init or re-init the class
        public void Init()
        {
            isReady = false;

            // set the user definded charset in the charsets hashtable
            charset cs = (charset)charsets["userDefined"];
            cs.symbols = pgo.userDefinedCharset;
            charsets["userDefined"] = cs;

            // merge all confusingChars-charsets into one string
            string charsetConfusing = "";
            foreach (string key in charsets.Keys)
            {
                if (key.Contains("-"))
                    charsetConfusing += ((charset)charsets[key]).symbols;
            }

            // check userDefined charset (if used)
            if (pgo.charsets.Contains("userDefined"))
            {
                charset userDefinedCharset = (charset)charsets["userDefined"];

                if (userDefinedCharset.symbols.Length != 0)
                {
                    // merge all used charsets except "userDefined" into one string
                    string allChars = "";
                    foreach (string chs in pgo.charsets)
                    {
                        if (chs != "userDefined")
                            allChars += ((charset)charsets[chs]).symbols;
                    }

                    // remove duplicates from userDefined charset
                    string s = userDefinedCharset.symbols;
                    for (int i = s.Length - 1; i >= 0; i--)
                        if (allChars.Contains(s[i]))
                            s = s.Remove(i, 1);
                    userDefinedCharset.symbols = s;

                    // remove confusing characters from userDefined charset
                    if (pgo.excludeConfusing)
                        for (int i = s.Length - 1; i >= 0; i--)
                            if (charsetConfusing.Contains(s[i]))
                                s = s.Remove(i, 1);
                    userDefinedCharset.symbols = s;
                }

                // check if userDefined charset is not empty after all this dups and confusing chars removals
                if (userDefinedCharset.symbols.Length == 0)
                    Array.Resize(ref pgo.charsets, pgo.charsets.Length - 1);  // remove the last array element (which is "userDefined")
            }

            // sort pgo.charsets array by easiness (for generating easy-to-type passwords)
            Array.Sort(pgo.charsets, delegate(string s1, string s2)
            {
                return ((charset)charsets[s1]).easiness.CompareTo(((charset)charsets[s2]).easiness);
            });
            // make the array begin with easiest charset
            Array.Reverse(pgo.charsets);

            // fill workCharsets array
            // it will include charsets selected by user, but without confusing characters (if this option is checked)
            workCharsets = new string[0];
            string wcs;

            foreach (string chs in pgo.charsets)
            {
                wcs = ((charset)charsets[chs]).symbols;

                if (pgo.excludeConfusing)
                    // remove confusing characters from wcs charset
                    if (pgo.excludeConfusing)
                        for (int i = wcs.Length - 1; i >= 0; i--)
                            if (charsetConfusing.Contains(wcs[i]))
                                wcs = wcs.Remove(i, 1);

                Array.Resize(ref workCharsets, workCharsets.Length + 1);
                workCharsets[workCharsets.Length - 1] = wcs;
            }

            // to be, or not to be? :)
            if (workCharsets.Length == 0)
            {
                pgo.pswLength = 0;
                isReady = false;    // errors in the pgo struct
            }
            else
                isReady = true; // class is ready to generate passwords

            CalculatePasswordStrength();
        }

        //--------------------------------------------------

        /// <summary>
        /// Generates true (well, sort of) random number within range [0..maxValue]
        /// </summary>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        private UInt32 GetRandomWithinRange(UInt32 maxValue)
        {
            if (maxValue == 0)
                throw new ArgumentOutOfRangeException("maxValue must be greater than 0");

            byte[] arRnd = new byte[4];
            rng.GetBytes(arRnd);
            UInt32 uiRnd, uiRndLimited;

            // how much bits maxValue is using?
            UInt32 usedBits = 1;
            UInt32 maxval = maxValue;
            while (maxval >= 1)
            {
                usedBits++;
                maxval = maxval / 2;
            }

            UInt32 mask = (UInt32)Math.Pow(2, usedBits) - 1;

            while (true)    // yes, it's theoretically infinite cycle :)  we must have some luck to break through!
            {
                // generate a random value
                rng.GetBytes(arRnd);
                uiRnd = BitConverter.ToUInt32(arRnd, 0);
                // and limit this value
                uiRndLimited = uiRnd & mask;

                // check if it's not exceed maxValue...
                if (uiRndLimited <= maxValue)
                    return uiRndLimited;    // and done! :)
            }
        }

        //--------------------------------------------------

        public string GeneratePassword()
        {
            if (pgo.pswLength == 0)
            {
                //System.Media.SystemSounds.Beep.Play();
                return "";  // structure is invalid
            }

            int i, j;

            // make password's layout
            int[] pswLayout = new int[pgo.pswLength];
            if (pgo.easyToType)
            {
                // generate easy-to-type password
                // made whole password from the easiest available charset with only a few symbols from other charsets

                // workCharsets[0] is the easiest charset (workCharsets array is sorted by easiness)
                for (i = 0; i < pgo.pswLength; i++) pswLayout[i] = 0;

                int spaceForSecondaryChars = pgo.pswLength - 1; // "secondary chars" means chars from non-easiest charsets

                // if password is long enough, some EXTRA chars from non-easiest charsets can be added, but not more then extraSecondaryCharsLimit:
                // pgo.pswLength    extraSecondaryCharsLimit
                // 1..12            0
                // 13..15           1
                // 16..18           2
                // >=19             3
                // 
                // for example, if you generate password from charsets az,AZ,09,#% and password length is:
                // 1..12   - your password will contain up to 3 chars from non-easiest charsets (which are AZ,09,#% in this case)
                // 13..15  - 3..4 (3 + [0..1]) chars from non easiest charsets
                // 16..18  - 3..5 (3 + [0..2]) chars from non easiest charsets
                // >=19    - 3..6 (3 + [0..3]) chars from non easiest charsets
                // (other symbols of the password will be from easiest charset - az)
                int extraSecondaryCharsLimit = Math.Max(Math.Min((int)((pgo.pswLength-10)/3), 3), 0);

                // add chars from non-easiest charsets into the password layout
                int pos;
                for (j = 1; j < workCharsets.Length; j++)
                {
                    if (spaceForSecondaryChars == 0) break;  // there is no room in the layout for the new chars from non-easiest charsets

                    // secondaryCharsCnt symbols will be added from workCharsets[j] charset into the password layout;
                    // if password is short, add only 1 char; but if password is big enough, additional extra character CAN be added (or not)
                    int secondaryCharsCnt = 1;
                    if (extraSecondaryCharsLimit > 0)
                    {
                        int extraSecondaryCharsCnt = (int)GetRandomWithinRange(1); // generate random number [0..1]
                        secondaryCharsCnt += extraSecondaryCharsCnt;
                        extraSecondaryCharsLimit -= extraSecondaryCharsCnt;
                    }

                    for (i = 0; i < secondaryCharsCnt; i++)
                    {
                        if (spaceForSecondaryChars == 0) break;  // there is no room in the layout for the new non-lowercase character

                        // search for a place in the layout. we must replace any easiest letter in the layout with non-easiest one
                        do
                        {
                            pos = (int)GetRandomWithinRange((UInt32)pgo.pswLength-1); // generate random number [0 .. pgo.pswLength-1]
                        } while (pswLayout[pos] != 0);

                        pswLayout[pos] = j; // j is workChasets array index
                        spaceForSecondaryChars--;
                    }
                }
            }
            else
            {
                // generate common password (not an easy-to-type; in such password charsets will be selected with the same probability)

                while (true)
                {
                    // make password's layout
                    for (i = 0; i < pgo.pswLength; i++)
                    {
                        pswLayout[i] = (int)GetRandomWithinRange((UInt32)workCharsets.Length-1); // generate random number [0 .. workCharsets.Length-1]
                    }

                    // count used chargroups in the generated layout.
                    // all selected chargroups must be used, or, if pgo.pswLength is too small to include even one char 
                    // from each charset, usedCharGroupsCnt must be equal to pgo.pswLength
                    int usedCharGroupsCnt = 0;
                    for (i = 0; i < pgo.pswLength; i++)
                        if (pswLayout.Contains(i)) usedCharGroupsCnt++;

                    if (usedCharGroupsCnt == Math.Min(workCharsets.Length, pgo.pswLength)) break;
                }
            }

            // generate password from layout
            string psw = "";
            for (i = 0; i < pgo.pswLength; i++)
            {
                psw += workCharsets[pswLayout[i]][(int)GetRandomWithinRange((UInt32)workCharsets[pswLayout[i]].Length-1)];  // generate random number [0 .. workCharsets[pswLayout[i]].Length)-1]
            }

            return psw; // successfully generated
        }

        //--------------------------------------------------

        public PasswordGenerationOptions PGO
        {
            get { return pgo; }
        }

        //--------------------------------------------------

        public string[] WorkCharsets
        {
            get { return workCharsets; }
        }

        //--------------------------------------------------

        public Hashtable Charsets
        {
            get { return charsets; }
        }

        //--------------------------------------------------

        private void CalculatePasswordStrength()
        {
            double[] ppsArray = new double[]
            {
                1000,   // Online Attack Scenario (Assuming one thousand guesses per second)
                0.55e9, // NVIDIA GTS250, NTLM cracking with Extreme GPU Bruteforcer
                1.33e9, // NVIDIA GeForce GTX 295 can try up to 1330 million Vista NTLM passwords per second.
                2.8e9,  // NVIDIA Tesla S1070 can try up to 2800 million Vista NTLM passwords per second.
                100e9,  // Offline Fast Attack Scenario (Assuming one hundred billion guesses per second)
                100e12  // Massive Cracking Array Scenario (Assuming one hundred trillion guesses per second)
            };
            double pps = ppsArray[2];
            double[] timeBorders = new double[] { 0, 14, 90, 365 }; // in days; according to enumPasswordStrength elements: weak, normal...

            double combinations = 0;

            // do not narrow search space depth if "exclude confusing characters" option is checked
            // hacker does not know about this option :)
            foreach (string cs in pgo.charsets)
                combinations += ((charset)charsets[cs]).symbols.Length; // Search Space Depth (Alphabet)
            // OR
            // calculate true search space depth when "exclude confusing characters" option is checked
            //foreach (string cs in workCharsets)
            //    combinations += cs.Length;  // Search Space Depth (Alphabet)

            // count of all possible passwords with this alphabet size and up to this password's length
            combinations = Math.Pow(combinations, pgo.pswLength);

            double days = combinations / pps / 3600 / 24;
            for(int i=timeBorders.Length-1;i>=0;i--)
                if (days >= timeBorders[i])
                {
                    passwordStrength = (enumPasswordStrength)i;
                    return;
                }
        }

        //--------------------------------------------------

        public enumPasswordStrength PasswordStrength
        {
            get { return passwordStrength; }
        }
    }
}

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

        // from which password will be generated. doesn't include confused characters if user has selected appropriate checkbox
        private string[] workCharsets;  

        public enum enumPasswordStrengthClass { weak, normal, good, excellent };

        public struct PasswordStrengthInfo
        {
            public enumPasswordStrengthClass strengthClass;
            public double combinations;
            public string crackTime;    // e.g. "6 days" or "1 hour"
            public double assumedSpeed;  // speed used in calculations, in passwords per second
        }

        private PasswordStrengthInfo passwordStrength;

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
            charsets.Add("ru-", new charset("", "обзэОВЗЭ", -1)); // confusing characters
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
        }

        //--------------------------------------------------

        /// <summary>
        /// Generates unbiased random number within range [0..maxValue]
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

        public string GeneratePassword(bool bulkMode = false)
        {
            if (pgo.pswLength == 0)
            {
                //System.Media.SystemSounds.Beep.Play();
                return "";  // structure is invalid
            }

            string psw = "";
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

                // generate password from layout
                for (i = 0; i < pgo.pswLength; i++)
                {
                    psw += workCharsets[pswLayout[i]][(int)GetRandomWithinRange((UInt32)workCharsets[pswLayout[i]].Length - 1)];  // generate random number [0 .. workCharsets[pswLayout[i]].Length)-1]
                }
            }
            else
            {
                // generate common password (not an easy-to-type; in such password charsets will be selected with the same probability)

                // combine all charsets into one big charset
                string allCharsets = "";
                foreach (string s in workCharsets)
                    allCharsets += s;

                while (true)
                {
                    // generate password
                    psw = "";
                    for (i = 0; i < pgo.pswLength; i++)
                    {
                        psw += allCharsets[(int)GetRandomWithinRange((UInt32)allCharsets.Length - 1)];  // generate random number [0 .. allCharsets.Length-1]
                    }

                    // count used charsers in the generated password.
                    // all selected charsets must be used, or, if pgo.pswLength is too small to include even a single char 
                    // from each charset, usedCharGroupsCnt must be equal to pgo.pswLength
                    int usedCharsetsCnt = 0;
                    for (i = 0; i < workCharsets.Length; i++)
                        for (j = 0; j < pgo.pswLength; j++)
                            if (workCharsets[i].Contains(psw[j]))
                            {
                                usedCharsetsCnt++;
                                break;
                            }

                    // if pgo.pswLength is too small to include even a single char from each charset, usedCharGroupsCnt must be equal to pgo.pswLength
                    if (usedCharsetsCnt == Math.Min(workCharsets.Length, pgo.pswLength))
                        break; // successfully generated
                }
            }

            // password is successfully generated
            if(!bulkMode)   // don't spend time on calculation password strength when in bulk mode
                CalculatePasswordStrength(psw);
            return psw;
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

        static IEnumerable<int[]> GetAllPossibleItemsCombinations(int allItems, int selectedItems)
        {
            if (allItems < selectedItems)
                throw new ArgumentOutOfRangeException("allItems must be greater than selectedItems");

            int[] result = new int[selectedItems];
            bool[] items = new bool[allItems];
            int i, j;

            for (i = 0; i < allItems; i++) // initialize array - select first selectedItems items of items[] array
                items[i] = (i < selectedItems);

            // return the first combination
            j = 0;
            for (i = 0; i < allItems; i++)
                if (items[i])
                {
                    result[j] = i;
                    j++;
                }
            yield return result;

            while (true)
            {
                // check if we reached the end of items[] array
                int selectedItemsAtTheEndOfTheArray = 0;
                for (i = allItems - 1; i >= 0; i--)
                    if (items[i])
                        selectedItemsAtTheEndOfTheArray++;
                    else
                        break;
                if (selectedItemsAtTheEndOfTheArray == selectedItems)
                    break;  // all possible combinations are found; exit

                // searching item closest to the end of the items[] array
                int lastSelectedItem = 0;
                for (i = allItems - 1; i >= 0; i--)
                    if (items[i])
                    {
                        lastSelectedItem = i;
                        break;
                    }

                if (lastSelectedItem != allItems - 1)  // if it's not the last item of the items[] array...
                {
                    // move selection closer to the end of the items[] array
                    items[lastSelectedItem] = false;
                    items[lastSelectedItem + 1] = true;
                }
                else    // it's the last item of the items[] array. 
                {
                    // how many consecutive selected items at the end of the items[] array?
                    for (i = allItems - 1; i >= 0; i--)
                        if (!items[i])
                            break;
                    if (i == -1) break;    // in case of allItems==selectedItems
                    int groupStart = i + 1;
                    int groupEnd = allItems - 1;
                    int groupSize = groupEnd - groupStart + 1;

                    // move found group "left" to closest selected item
                    for (; i >= 0; i--)
                        if (items[i])
                            break;  // first selected item ouside the group found

                    // increase size of the group by 1 and set new borders
                    groupStart = i;
                    groupSize++;
                    groupEnd = groupStart + groupSize - 1;

                    for (i = groupStart; i <= groupEnd; i++)
                        items[i] = true;    // move group
                    for (i = groupEnd + 1; i < allItems; i++)
                        items[i] = false;   // mark rest of the items as unselected

                    // and move this whole larger group [groupStart..lastSelectedItem] to the right by 1 item
                    items[groupStart] = false;
                    items[groupEnd + 1] = true;
                }

                // return next combination
                j = 0;
                for (i = 0; i < allItems; i++)
                    if (items[i])
                    {
                        result[j] = i;
                        j++;
                    }
                yield return result;
            }
        }

        //--------------------------------------------------

        /// <summary>
        /// Calculates number of combinations for password pgo.pswLength symbols long.
        /// </summary>
        /// <param name="charsetsAr"></param>
        /// <returns></returns>
        private double CalculateCombinationsNumber(string[] charsetsAr)
        {
            int allCharsetsLength = 0;
            foreach (string s in charsetsAr)
                allCharsetsLength += s.Length;  // Search Space Depth (Alphabet) size
            
            double combinations = Math.Pow(allCharsetsLength, pgo.pswLength);

            switch (charsetsAr.Length)
            {
                case 1:
                    break;
                default:    // 2 or more
                    // creating copy of charsetsAr without i-item
                    for (int arLength = charsetsAr.Length - 1; arLength > 0; arLength--)
                    {
                        foreach (int[] ar in GetAllPossibleItemsCombinations(charsetsAr.Length, arLength))
                        {
                            // creating copy of charsetsAr without charsetsAr[arLength] item
                            string[] reducedCharsetsAr = new string[arLength];
                            for (int i = 0; i < arLength; i++)
                                reducedCharsetsAr[i] = charsetsAr[ar[i]];

                            combinations -= CalculateCombinationsNumber(reducedCharsetsAr);
                        }
                    }
                    break;
            }

            return combinations;
        }

        //--------------------------------------------------

        private void CalculatePasswordStrength(string psw)
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
            double pps = ppsArray[2];   // assume cracking speed (passwords per second)
            double[] timeBorders = new double[] { 0, 7, 60, 365 }; // in days; according to enumPasswordStrength elements: weak, normal...

            int allCharsetsLength = 0;
            int i, j;


            // filling actualCharsets[] array
         
            string[] actualCharsets;    // actually used in currently generated password

            // count used charsers in the generated password.
            // all selected charsets must be used, or, if pgo.pswLength is too small to include even a single char 
            // from each charset, usedCharGroupsCnt must be equal to pgo.pswLength
            int usedCharsetsCnt = 0;
            for (i = 0; i < workCharsets.Length; i++)
                for (j = 0; j < pgo.pswLength; j++)
                    if (workCharsets[i].Contains(psw[j]))
                    {
                        usedCharsetsCnt++;
                        break;
                    }
                    
            // if password length is lesser then selected charsets number, not all charsets will be used, so we need to find
            // what actual charsets are used in this paticual password.
            actualCharsets = new string[usedCharsetsCnt];
            int index = 0;
            for (i = 0; i < workCharsets.Length; i++)
                for (j = 0; j < pgo.pswLength; j++)
                    if (workCharsets[i].Contains(psw[j]))
                    {
                        actualCharsets[index] = workCharsets[i];
                        index++;
                        break;
                    }


            // do not narrow search space depth if "exclude confusing characters" option is checked
            // hacker does not know about this option :)
            //foreach (string cs in pgo.charsets)
            //    combinations += ((charset)charsets[cs]).symbols.Length; // Search Space Depth (Alphabet)
            // OR
            // calculate true search space depth when "exclude confusing characters" option is checked
            foreach (string s in actualCharsets)
                allCharsetsLength += s.Length;  // Search Space Depth (Alphabet) size

            // count of all possible passwords with this alphabet size and up to this password's length
            double combinationsAll = Math.Pow(allCharsetsLength, pgo.pswLength);
            
            // wrong method above. Since application only accepts passwords with all selected charsets included, this also means
            // it rejects some weak passwords, and final search space depth will be fewer.
            double combinations = CalculateCombinationsNumber(actualCharsets);

            //// aproximate calculation - another wrong method. sometimes result is below zero :)
            //double combinationsApproximate = combinationsAll;
            //for (int i = 0; i < actualCharsets.Length; i++)
            //{
            //    // creating copy of charsetsAr without i-item
            //    string[] reducedCharsetsAr = new string[actualCharsets.Length - 1];
            //    int index = 0;
            //    for (int j = 0; j < actualCharsets.Length; j++)
            //        if (j != i)
            //        {
            //            reducedCharsetsAr[index] = actualCharsets[j];
            //            index++;
            //        }

            //    int charsetsLength = 0;
            //    foreach (string s in reducedCharsetsAr)
            //        charsetsLength += s.Length;  // Search Space Depth (Alphabet) size

            //    combinationsApproximate -= Math.Pow(charsetsLength, pgo.pswLength);
            //}

            passwordStrength.assumedSpeed = pps;
            passwordStrength.combinations = combinations;
            double days = combinations / pps / 3600 / 24;
            double years = days / 365;
            if (years >= 1)
                passwordStrength.crackTime = ((UInt32)years != 0 ? ((UInt32)years).ToString() : years.ToString("g3")) + 
                    " year" + (years > 1 ? "s" : "");
            else
                passwordStrength.crackTime = ((int)days).ToString() + " day" + ((int)days != 1 ? "s" : "");
            for (i = timeBorders.Length - 1; i >= 0; i--)
                if (days >= timeBorders[i])
                {
                    passwordStrength.strengthClass = (enumPasswordStrengthClass)i;
                    return;
                }
        }

        //--------------------------------------------------

        public PasswordStrengthInfo PasswordStrength
        {
            get { return passwordStrength; }
        }
    }
}

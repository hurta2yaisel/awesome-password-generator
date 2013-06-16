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
            public int easiness;   // using when generating easy-to-type passwords. Higher values are "easier".

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

        // from which password will be generated. doesn't include confusing characters (if user has selected the appropriate checkbox)
        private string[] workCharsets;
        private string[] workCharsetsKeys;

        int[] easyToTypePasswordLayout;

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

            // fill workCharsets[] array
            // it will include charsets selected by user, but without confusing characters (if this option is checked)
            workCharsets = new string[0];
            workCharsetsKeys = new string[0];
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
                Array.Resize(ref workCharsetsKeys, workCharsetsKeys.Length + 1);
                workCharsetsKeys[workCharsetsKeys.Length - 1] = chs;
            }

            // to be or not to be? :)
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
                return 0;   // the only one possible result :)

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

        private int GetRandomWithinRange(int maxValue)
        {
            if (maxValue < 0)
                throw new ArgumentOutOfRangeException("maxValue must be greater than 0");
            if (maxValue == 0)
                return 0;   // the only one possible result :)

            return (int)GetRandomWithinRange((UInt32)maxValue);
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
            int i, j, k;

            if (pgo.easyToType)
            {
                // generate easy-to-type password
                // made whole password from the easiest available charset with only a few symbols from other charsets

                easyToTypePasswordLayout = new int[pgo.pswLength];

                // workCharsets[0] is the easiest charset (workCharsets array is sorted by easiness)
                for (i = 0; i < pgo.pswLength; i++) easyToTypePasswordLayout[i] = 0;

                // "secondary chars" means chars from non-easiest charsets. 
                // at least one symbol from the easiest charset must present in the password.
                int spaceLeftForSecondaryChars = pgo.pswLength - 1;

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
                int extraSecondaryCharsLimit;
                extraSecondaryCharsLimit = Math.Min((int)((pgo.pswLength - 10) / 3), 3);
                extraSecondaryCharsLimit = Math.Min(extraSecondaryCharsLimit, pgo.pswLength - workCharsets.Length - 1);
                if (extraSecondaryCharsLimit < 0) extraSecondaryCharsLimit = 0;

                // add chars from non-easiest charsets into the password layout
                for (i = workCharsets.Length - 2; i >= 0; i--)
                {
                    // if there is no more room in the layout for the new chars from non-easiest charsets, layout is ready
                    if (spaceLeftForSecondaryChars == 0) break;

                    // chose one of i not yet used in currently generating password non-easiest charsets
                    int currentNonEasiestCharsetIndex = -2;  // index of item in workCharsets[] array
                    int currentNonEasiestCharsetSequenceNumber = GetRandomWithinRange(i); // generate random number [0..i]
                    int nonEasiestCharsetsCnt = 0;
                    for (j = 1; j < workCharsets.Length; j++)
                    {
                        for (k = 0; k < pgo.pswLength; k++)
                            if (easyToTypePasswordLayout[k] == j)
                                break;  // this charset is already present in password
                        if (k == pgo.pswLength)
                        {
                            // found one of not yet used non-easiest charsets
                            nonEasiestCharsetsCnt++;
                            if (nonEasiestCharsetsCnt - 1 == currentNonEasiestCharsetSequenceNumber)
                            {
                                currentNonEasiestCharsetIndex = j;
                                break;
                            }
                        }
                    }
                    // now when we have randomly selected non-easiest charset, let's add it to random place in the password's layout
                    int placeInTheLayoutIndex = -2;  // index of item in easyToTypePasswordLayout[] array
                    int placeInTheLayoutSequenceNumber = GetRandomWithinRange(spaceLeftForSecondaryChars); // generate random number [0..(spaceLeftForSecondaryChars - 1)]
                    int placeInTheLayoutCnt = 0;
                    for (j = 0; j < pgo.pswLength; j++)
                        if (easyToTypePasswordLayout[j] == 0)
                        {
                            placeInTheLayoutCnt++;
                            if (placeInTheLayoutCnt - 1 == placeInTheLayoutSequenceNumber)
                            {
                                placeInTheLayoutIndex = j;
                                break;
                            }
                        }

                    // update password's layout
                    if (placeInTheLayoutIndex == -2 || currentNonEasiestCharsetIndex == -2)
                        throw new Exception("error!");   //`
                    easyToTypePasswordLayout[placeInTheLayoutIndex] = currentNonEasiestCharsetIndex;
                    
                    spaceLeftForSecondaryChars--;
                }

                // extraSecondaryChars EXTRA characters from non-easiest charsets will be added to password
                int extraSecondaryChars = GetRandomWithinRange(extraSecondaryCharsLimit); // generate random number [0..extraSecondaryCharsLimit]
                for (i = 0; i < extraSecondaryChars; i++)
                {
                    if (spaceLeftForSecondaryChars == 0) break;

                    int extraNonEasiestCharsetIndex = GetRandomWithinRange(workCharsets.Length - 1); // generate random number [0..(workCharsets.Length - 1)]

                    // now when we have randomly selected non-easiest charset, let's add it to random place in the password's layout
                    int placeInTheLayoutIndex = -2;  // index of item in easyToTypePasswordLayout[] array
                    int placeInTheLayoutSequenceNumber = GetRandomWithinRange(spaceLeftForSecondaryChars); // generate random number [0..(spaceLeftForSecondaryChars - 1)]
                    int placeInTheLayoutCnt = 0;
                    for (j = 0; j < pgo.pswLength; j++)
                        if (easyToTypePasswordLayout[j] == 0)
                        {
                            placeInTheLayoutCnt++;
                            if (placeInTheLayoutCnt - 1 == placeInTheLayoutSequenceNumber)
                            {
                                placeInTheLayoutIndex = j;
                                break;
                            }
                        }

                    // update password's layout
                    if (placeInTheLayoutIndex == -2)
                        throw new Exception("error!");   //`
                    easyToTypePasswordLayout[placeInTheLayoutIndex] = extraNonEasiestCharsetIndex;

                    spaceLeftForSecondaryChars--;
                }

                // generate password from layout
                for (i = 0; i < pgo.pswLength; i++)
                {
                    psw += workCharsets[easyToTypePasswordLayout[i]][GetRandomWithinRange(workCharsets[easyToTypePasswordLayout[i]].Length - 1)];  // generate random number [0 .. workCharsets[pswLayout[i]].Length)-1]
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
                        psw += allCharsets[GetRandomWithinRange(allCharsets.Length - 1)];  // generate random number [0 .. allCharsets.Length-1]
                    }

                    // count used charsers in the generated password.
                    // all selected charsets must be used, or, if pgo.pswLength is too small to include even a single char 
                    // from each charset, usedCharsetsCnt must be equal to pgo.pswLength
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

        private IEnumerable<int[]> GetAllPossibleItemsCombinations(int allItems, int selectedItems)
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
        private double CalculateStrongCombinationsNumber(string[] charsetsAr)
        {
            // Imagine user has checked 0..9 and a..z charsets and password length is... let's say 7. How many combinations possible?
            // easy: (09.len+az.len)^7 == (10+26)^7. But it includes either "strong" passwords with both numbers and letters and "weak" passwords 
            // with only digits or letters included. Assuming user has checked both charsets for a reason, application must not generate
            // "weak" passwords. It reduces total combinations number of course, but how much exactly combinations remains?
            // Simple subtraction will help: we already know all combinations number - (10+26)^7, also we have 10^7 passwords with only digits
            // and 26^7 password with letters only. So it will be  
            // (09+az)^7 - 09^7 - az^7 == (10+26)^7 - 10^7 - 26^7  == 7.03e+10
            // "strong" passwords.
            //
            // Another example - with same password length but more charsets: 0..9, a..z, #..$.
            // Here passwords with 2 charsets will be also "weak", together with 1 charset passwords.
            // Assumption 
            // (09+az+#$)^7 - (09+az)^7 - (09+#$)^7 - (az+#$)^7 - 09^7 - az^7 - #$^7 
            //       ^             ^          ^           ^        ^      ^      ^
            // all passwords       weak passwords with only       weak passwords with
            //                           2 charsets                  only 1 charset
            // is wrong, because we subtracting too much: (09+az)^7, for example, includes 09^7 and az^7. Correct answer is:
            // (09+az+#$)^7 - ((09+az)^7 - 09^7 - az^7) - ((09+#$)^7 - 09^7 - #$^7) - ((az+#$)^7 - az^7 - #$^7) - 09^7 - az^7 - #$^7 ==
            // (10+26+32)^7 - ((10+26)^7 - 10^7 - 26^7) - ((10+32)^7 - 10^7 - 32^7) - ((26+32)^7 - 26^7 - 32^7) - 10^7 - 26^7 - 32^7 ==
            // == 4.25e+12
            //
            // Let's skip example with 4 charsets, ok? ;)
            // Auxiliary GetAllPossibleItemsCombinations() function do some rearranging. 
            // For example, from (09, az, #$) it returns (09, az), (09, #$) and (az, #$),
            // (09, az) -> (09) and (az), and so on.

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
                        if (arLength >= pgo.pswLength) continue;    // for small passwords where pgo.Length < workCharsets.Length

                        foreach (int[] ar in GetAllPossibleItemsCombinations(charsetsAr.Length, arLength))
                        {
                            // creating copy of charsetsAr without charsetsAr[arLength] item
                            string[] reducedCharsetsAr = new string[arLength];
                            for (int i = 0; i < arLength; i++)
                                reducedCharsetsAr[i] = charsetsAr[ar[i]];

                            combinations -= CalculateStrongCombinationsNumber(reducedCharsetsAr);
                        }
                    }
                    break;
            }

            return combinations;
        }

        //--------------------------------------------------

        private double Factorial(double n)
        {
            double result = 1;
            for (int i = 2; i <= n; i++)
                result *= i;
            
            return result;
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
            passwordStrength.assumedSpeed = pps;

            double[] timeBorders = new double[] { 0, 7, 60, 365 }; // in days; according to enumPasswordStrength elements: weak, normal...

            int allCharsetsLength = 0;
            int i, j;

            int extraSecondaryCharsLimit;
            int noneasiestCharsetsTotalLength;
            int extraChars;
            int easiestSymbolsInPassword;
            double combinationsTemp;
            double multisetPermutations;
            double premutations;

            double combinationsStandardBruteforce = 0;  // "if hacker uses standard bruteforce"
            double combinationsBasedOnAlgorithm = 0;    // "if hacker knows and can exploit algorithm"


            // filling actualCharsets[] array
            string[] actualCharsets;    // actually used in currently generated password
            string[] actualCharsetsKeys;

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
            actualCharsetsKeys = new string[usedCharsetsCnt];
            if (pgo.pswLength < workCharsets.Length)
            {
                // password is too short to include symbols from every selected charsets, so actualCharsets will be smaller than workCharsets
                int index = 0;
                for (i = 0; i < workCharsets.Length; i++)
                    for (j = 0; j < pgo.pswLength; j++)
                        if (workCharsets[i].Contains(psw[j]))
                        {
                            actualCharsets[index] = workCharsets[i];
                            actualCharsetsKeys[index] = workCharsetsKeys[i];
                            index++;
                            break;
                        }
            }
            else
            {
                // password is long enough to include symbols from every selected charsets, so actualCharsets == workCharsets
                workCharsets.CopyTo(actualCharsets, 0);
                workCharsetsKeys.CopyTo(actualCharsetsKeys, 0);
            }


            // calculate combinations quantity "if hacker uses standard bruteforce (which happens when he does not know password-generation
            // algorithm, or does not know it's options (easy-to-type or common password, if "exclude confused characters" option is selected
            // or not), or just can't exploit it)"

            // do not narrow search space depth if "exclude confusing characters" option is checked.
            // hacker does not know if this option is checked or not :)
            foreach (string key in actualCharsetsKeys)
                allCharsetsLength += ((charset)charsets[key]).symbols.Length; // Search Space Depth (Alphabet)
            // OR
            // calculate true search space depth when "exclude confusing characters" option is checked
            //foreach (string s in actualCharsets)
            //    allCharsetsLength += s.Length;  // Search Space Depth (Alphabet) size

            // count of all possible passwords with this alphabet size and up to this password's length
            combinationsStandardBruteforce = Math.Pow(allCharsetsLength, pgo.pswLength);


            // caclulate number of combinations based on password generation algorithm, or "if hacker knows the algorithm and can exploit it"
            if (pgo.easyToType) // easy-to-type password
            {
                // there are three methods of calculating number of combinations, depending on password length
                if (pgo.pswLength == 1)
                {
                    // if password contains only one symbol - it's always from the easiest charset
                    combinationsBasedOnAlgorithm = workCharsets[0].Length;
                }
                else if (pgo.pswLength < workCharsets.Length)
                {
                    // password is too short to include symbols from every selected charsets
                    // actualCharsets is smaller than workCharsets
                    // Password will include one symbol from the easiest charset and pgo.pswLength-1 symbols from randomly selected 
                    // DIFFERENT (it's important when calculating premutations) non-easiest charsets.

                    noneasiestCharsetsTotalLength = 0;
                    for (i = 1; i < workCharsets.Length; i++)
                        noneasiestCharsetsTotalLength += workCharsets[i].Length;

                    combinationsBasedOnAlgorithm = workCharsets[0].Length * Math.Pow(noneasiestCharsetsTotalLength, pgo.pswLength - 1);
                    premutations = Factorial(pgo.pswLength);
                    combinationsBasedOnAlgorithm *= premutations;
                }
                else
                {
                    // password is long enough to include symbols from every selected charsets
                    // actualCharsets == workCharsets

                    // let's see an example of easy-to-type password 10 symbols long with following selected charsets:
                    // az (26 symbols), AZ (26), 09 (10), #% (32).
                    // az will be the "easiest" (to type) charset, so password will include 1 symbol from AZ charset, 1 from 09, 1 from #% and
                    // 7 symbols from az charset.
                    // now to combinations. combinations number in any single password layout will be: 26^7 * 26 * 10 * 32. But it's only one
                    // layout. Now we need to find number of all possible layouts.
                    // Combinatorics will help us, here is the article about permutations:
                    // http://en.wikipedia.org/wiki/Permutation
                    // In a case when password length == number of selected charsets (which is not out case :) and every position of 
                    // the password contains a symbol from different charset, number of layouts will be:
                    // Factorial(number_of_selected_charsets) == 4!    Easy. But in our case when password length is 10, 7 positions
                    // in password will contain symbols from the same ("easiest") charset - az. It's a multiset permutation.
                    // So, layouts quantity is:  
                    // Factorial(password_length)/(Factorial(symbols_number_from_az_charset)*Factorial(AZ)*Factorial(09)*Factorial(#$)) ==
                    // == 10!/(7!*1!*1!*1!) == 10!/7!.
                    // Final result will be:
                    // combinations_number_in_a_one_single_layout * layouts_number == (26^7 * 26 * 10 * 32) * (10!/7!)
                    //
                    // Task becomes more interesting when password length is 13 or greater :)  
                    // Some EXTRA symbols from non-easiest charsets can be added, but not more then extraSecondaryCharsLimit:
                    // pgo.pswLength    extraSecondaryCharsLimit
                    // 1..12            0
                    // 13..15           1
                    // 16..18           2
                    // >=19             3
                    // In our next example password length will be 20, and same selected charsets as in previous example.
                    // Up to 3 extra symbols from non-easiest charsets can be added to such password. What we need to do is:
                    // - calculate combinations number assuming 0 extra symbols, just like before;
                    // - calculate combinations number assuming 1 extra symbol;
                    // - calculate combinations number assuming 2 extra symbols;
                    // - calculate combinations number assuming 3 extra symbols;
                    // - total result will be sum of all 4 values above.
                    // And... how to take into account extra symbols? When we add 1 extra symbol, password will contain:
                    // 16 symbols from az, 1 from AZ, 1 from 09, 1 from #$ and 1 symbol from (az+AZ+09+#$) charset, and
                    // combinations number in any single password layout will be:
                    // 26^16 * 26 * 10 * 32 * 94.
                    // When calculating layouts, size of any particular charset doesn't matter. We know that extra symbols will be from
                    // non-easiest charsets, and whatever charset it will be from, it will be SECOND symbol from this charset in the password.
                    // Let's imagine that #$ is that lucky charset, I repeat - it doesn't matter.
                    // So, layouts quantity is (notify the 2! instead of Factorial(#$) in the end of the formula):
                    // Factorial(password_length)/(Factorial(symbols_number_from_az_charset)*Factorial(AZ)*Factorial(09)*2!) ==
                    // == 20!/(16!*1!*1!*2!) == 10!/16!*2!,
                    // and the final result:
                    // (26^16 * 26 * 10 * 32 * 94) * (10!/16!*2!)

                    extraSecondaryCharsLimit = Math.Min((int)((pgo.pswLength - 10) / 3), 3);
                    extraSecondaryCharsLimit = Math.Min(extraSecondaryCharsLimit, pgo.pswLength - workCharsets.Length - 1);
                    if (extraSecondaryCharsLimit < 0) extraSecondaryCharsLimit = 0;

                    // workCharsets[0] is the easiest charset (workCharsets array is sorted by easiness)

                    noneasiestCharsetsTotalLength = 0;
                    for (i = 1; i < workCharsets.Length; i++)
                        noneasiestCharsetsTotalLength += workCharsets[i].Length;
                    combinationsBasedOnAlgorithm = 0;

                    for (extraChars = 0; extraChars <= extraSecondaryCharsLimit; extraChars++)
                    {
                        easiestSymbolsInPassword = Math.Max(pgo.pswLength - workCharsets.Length + 1 - extraChars, 1);

                        // combinations number in any single password layout will be:
                        combinationsTemp = Math.Pow(workCharsets[0].Length, easiestSymbolsInPassword);
                        for (i = 1; i < workCharsets.Length; i++)
                            combinationsTemp *= workCharsets[i].Length;
                        if (extraChars != 0)
                            combinationsTemp *= Math.Pow(noneasiestCharsetsTotalLength, extraChars);

                        // layouts number:
                        multisetPermutations = Factorial(pgo.pswLength);
                        multisetPermutations /= Factorial(easiestSymbolsInPassword);
                        if (workCharsets.Length > 1)
                            multisetPermutations /= Factorial(1 + extraChars);

                        // final result (with premutations)
                        combinationsTemp *= multisetPermutations;
                        combinationsBasedOnAlgorithm += combinationsTemp;
                    }
                }
            }

            else  // common password (not an easy-to-type)
            {
                //// calculate search space depth
                //foreach (string s in actualCharsets)
                //    allCharsetsLength += s.Length;  // Search Space Depth (Alphabet) size

                //// count of all possible passwords with this alphabet size and up to this password's length
                //combinationsAll = Math.Pow(allCharsetsLength, pgo.pswLength);

                // wrong method above. Since application only accepts passwords with all selected charsets included, this also means
                // it rejects some weak passwords, and final combinations number will be fewer.
                combinationsBasedOnAlgorithm = CalculateStrongCombinationsNumber(workCharsets);
            }


            // assuming the worst... :)
            passwordStrength.combinations = Math.Min(combinationsBasedOnAlgorithm, combinationsStandardBruteforce);

            double days = passwordStrength.combinations / pps / 3600 / 24;
            double years = days / 365;
            if (years >= 1)
                passwordStrength.crackTime = ((UInt32)years != 0 ? ((UInt32)years).ToString() : years.ToString("g3")) + 
                    " year" + (Math.Floor(years) > 1 ? "s" : "");
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

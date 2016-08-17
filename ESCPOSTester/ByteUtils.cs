using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESCPOSTester
{
    class ByteUtils
    {
        /// <summary>
        /// Reads data from a stream until the end is reached. The
        /// data is returned as a byte array. An IOException is
        /// thrown if any of the underlying IO calls fail.
        /// </summary>
        public static byte[] ReadFileContainHexStringASCII(string absFilePath)
        {
            string line;
            var list = new List<string>();

            // Read the file and display it line by line.
            using (System.IO.StreamReader file =
                new System.IO.StreamReader(absFilePath))
            {
                while ((line = file.ReadLine()) != null)
                {
                    list.Add(line.Replace(" ", string.Empty));
                }

                var bList = new List<byte>();

                foreach (string s in list)
                {
                    foreach (byte b in StringToByteArrayFastest(s))
                        bList.Add(b);
                }

                return bList.ToArray<byte>();
            }
        }
        

        /// <summary>
        /// Convert a string into a byte array
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : 55);
        }
    }
}

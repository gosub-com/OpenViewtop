using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace Gosub.Viewtop
{
    public class Util
    {
        const string HEX_DIGITS = "0123456789ABCDEF";
        public static string ToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(HEX_DIGITS[b >> 4]);
                sb.Append(HEX_DIGITS[b & 0x0F]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Generate a 32 byte cryptographically secure random number
        /// </summary>
        public static string GenerateSalt()
        {
            var salt = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);
            return Util.ToHex(salt);
        }

        public static int GenerateRandomId()
        {
            var idBytes = new byte[4];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(idBytes);
            int id = 0;
            foreach (var b in idBytes)
                id = id * 256 + b;
            return id & 0x7FFFFFFF;
        }

    }
}

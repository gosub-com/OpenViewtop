using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;


namespace Gosub.Viewtop
{
    public class Util
    {
        const string OPEN_VIEWTOP_PFX_FILE_NAME = "OpenViewTop";
        const string OPEN_VIEWTOP_PFX_PASSWORD = "OpenViewTop";
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
            return ToHex(salt);
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

        static public X509Certificate2 GetCertificate()
        {
            try
            {
                // Try reading a previously created pfx
                string pfxPath = Application.CommonAppDataPath + "\\" + OPEN_VIEWTOP_PFX_FILE_NAME + ".pfx";
                byte[] pfx = new byte[0];
                try { pfx = File.ReadAllBytes(pfxPath); }
                catch { }

                // Create the PFX and save it, if necessary
                if (pfx.Length == 0)
                {
                    pfx = PFXGenerator.GeneratePfx(OPEN_VIEWTOP_PFX_FILE_NAME, OPEN_VIEWTOP_PFX_PASSWORD);
                    File.WriteAllBytes(pfxPath, pfx);
                }
                return new X509Certificate2(pfx, OPEN_VIEWTOP_PFX_PASSWORD,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error setting up HTTPS secure link: " + ex.Message);
            }
            return null;
        }


    }
}

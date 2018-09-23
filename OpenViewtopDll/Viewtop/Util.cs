using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Gosub.Http;
using MonoX509 = Mono.Security.X509;
using System.Collections;
using System.Security.Principal;
using System.Security.AccessControl;



namespace Gosub.Viewtop
{
    public class Util
    {
        const string OPEN_VIEWTOP_PFX_FILE_NAME = "OpenViewtop";
        const string OPEN_VIEWTOP_PFX_PASSWORD = "OpenViewtop";
        const string HEX_DIGITS = "0123456789ABCDEF";

        static readonly string OPEN_VIEWTOP_PFX_PATH = Application.CommonAppDataPath + "\\" + OPEN_VIEWTOP_PFX_FILE_NAME + ".pfx";

        /// <summary>
        /// Allow users without admin rights to change this file
        /// </summary>
        public static void SetWorldAccessControl(string fileName)
        {
            try
            {
                var fs = new FileSecurity();
                fs.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(
                    WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
                File.SetAccessControl(fileName, fs);
            }
            catch (Exception ex)
            {
                Log.Write("SetWorldAccessControl", ex);
            }
        }

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

        public static long GenerateRandomId()
        {
            // Do not overflow a double precision nubmer (mantissa is 53 bits)
            var idBytes = new byte[6];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetNonZeroBytes(idBytes);
            long id = 0;
            foreach (var b in idBytes)
                id = id * 256 + b;
            return id;
        }

        /// <summary>
        /// Get the certificate (create a new one if it doesn't exist)
        /// </summary>
        static public X509Certificate2 GetCertificate()
        {
            try
            {
                // Try reading the previously created pfx
                return new X509Certificate2(File.ReadAllBytes(OPEN_VIEWTOP_PFX_PATH), OPEN_VIEWTOP_PFX_PASSWORD);
            }
            catch (Exception ex)
            {
                // Create new self signed certificate
                Log.Write("Creating new self signed certificate because: " + ex.Message);
                return RegenerateSelfSignedCertificte();
            }
        }

        /// <summary>
        /// Regenerate the self signed certificate and save to file
        /// </summary>
        static public X509Certificate2 RegenerateSelfSignedCertificte()
        {
            var pfxBytes = GeneratePfx(OPEN_VIEWTOP_PFX_FILE_NAME, OPEN_VIEWTOP_PFX_PASSWORD);
            File.WriteAllBytes(OPEN_VIEWTOP_PFX_PATH, pfxBytes);
            SetWorldAccessControl(OPEN_VIEWTOP_PFX_FILE_NAME);
            return new X509Certificate2(pfxBytes, OPEN_VIEWTOP_PFX_PASSWORD);
        }

        static public void LoadCertificateFile(string pfxFilePath)
        {
            var pfxBytes = File.ReadAllBytes(pfxFilePath);

            X509Certificate2 cert;

            try
            {
                cert = new X509Certificate2(pfxBytes, OPEN_VIEWTOP_PFX_PASSWORD);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\r\n\r\nNOTE: The pfx file must be created with the password '" + OPEN_VIEWTOP_PFX_PASSWORD + "'");
            }
            if (cert.PublicKey == null)
                throw new Exception("The certificate must also contain the public key");
            if (cert.PrivateKey == null)
                throw new Exception("The certificate must also contain the private key");
            File.WriteAllBytes(OPEN_VIEWTOP_PFX_PATH, pfxBytes);
            SetWorldAccessControl(OPEN_VIEWTOP_PFX_FILE_NAME);
        }

        //adapted from https://github.com/mono/mono/blob/master/mcs/tools/security/makecert.cs
        static byte[] GeneratePfx(string certificateName, string password)
        {
            var sn = GenerateSerialNumber();
            var subject = string.Format("CN={0}", certificateName);
            var notBefore = DateTime.Now;
            var notAfter = DateTime.Now.AddYears(20);
            var subjectKey = new RSACryptoServiceProvider(2048);
            var hashName = "SHA256";

            var cb = new MonoX509.X509CertificateBuilder(3);
            cb.SerialNumber = sn;
            cb.IssuerName = subject;
            cb.NotBefore = notBefore;
            cb.NotAfter = notAfter;
            cb.SubjectName = subject;
            cb.SubjectPublicKey = subjectKey;
            cb.Hash = hashName;

            var rawcert = cb.Sign(subjectKey);

            var p12 = new MonoX509.PKCS12();
            p12.Password = password;

            Hashtable attributes = GetAttributes();
            p12.AddCertificate(new MonoX509.X509Certificate(rawcert), attributes);
            p12.AddPkcs8ShroudedKeyBag(subjectKey, attributes);

            return p12.GetBytes();
        }

        private static Hashtable GetAttributes()
        {
            ArrayList list = new ArrayList();
            // we use a fixed array to avoid endianess issues 
            // (in case some tools requires the ID to be 1).
            list.Add(new byte[4] { 1, 0, 0, 0 });
            Hashtable attributes = new Hashtable(1);
            attributes.Add(MonoX509.PKCS9.localKeyId, list);
            return attributes;
        }

        private static byte[] GenerateSerialNumber()
        {
            byte[] sn = Guid.NewGuid().ToByteArray();

            //must be positive
            if ((sn[0] & 0x80) == 0x80)
                sn[0] -= 0x80;
            return sn;
        }


    }
}

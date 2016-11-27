﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Mono.Security.X509;
using System.Security.Cryptography;

namespace Gosub.Viewtop
{
    /// <summary>
    /// It appears that .NET doesn't include a way to create self signed certificates, 
    /// so let's use mono.security to do it.
    /// 
    /// This code was copied from here: 
    /// http://www.freekpaans.nl/2015/04/creating-self-signed-x-509-certificates-using-mono-security
    /// 
    /// </summary>
    public class PFXGenerator
    {
        //adapted from https://github.com/mono/mono/blob/master/mcs/tools/security/makecert.cs
        public static byte[] GeneratePfx(string certificateName, string password)
        {
            byte[] sn = GenerateSerialNumber();
            string subject = string.Format("CN={0}", certificateName);

            DateTime notBefore = DateTime.Now;
            DateTime notAfter = DateTime.Now.AddYears(20);

            RSA subjectKey = new RSACryptoServiceProvider(2048);


            string hashName = "SHA256";

            X509CertificateBuilder cb = new X509CertificateBuilder(3);
            cb.SerialNumber = sn;
            cb.IssuerName = subject;
            cb.NotBefore = notBefore;
            cb.NotAfter = notAfter;
            cb.SubjectName = subject;
            cb.SubjectPublicKey = subjectKey;
            cb.Hash = hashName;

            byte[] rawcert = cb.Sign(subjectKey);

            PKCS12 p12 = new PKCS12();
            p12.Password = password;

            Hashtable attributes = GetAttributes();

            p12.AddCertificate(new X509Certificate(rawcert), attributes);
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
            attributes.Add(PKCS9.localKeyId, list);
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

        public static byte[] GetCertificateForBytes(byte[] pfx, string password)
        {
            var pkcs = new PKCS12(pfx, password);
            var cert = pkcs.GetCertificate(GetAttributes());

            return cert.RawData;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

namespace Gosub.Viewtop
{
    class UserFile
    {
        const string USER_FILE_NAME = "Users.json";
        static readonly string sUserFilePath = Path.Combine(Application.CommonAppDataPath, USER_FILE_NAME);
        static object sLock = new object();

        public List<User> Users { get; set; } = new List<User>();

        public static UserFile Load()
        {
            lock (sLock)
            {
                if (!File.Exists(sUserFilePath))
                    return new UserFile();
                return JsonConvert.DeserializeObject<UserFile>(File.ReadAllText(sUserFilePath));
            }
        }

        public static void Save(UserFile users)
        {
            lock (sLock)
                File.WriteAllText(sUserFilePath, JsonConvert.SerializeObject(users));
        }

        public static bool ValidUserName(string userName)
        {
            foreach (char ch in userName)
                if (!char.IsDigit(ch) && !char.IsLetter(ch))
                    return false;
            return true;
        }

        public User Find(string userName)
        {
            userName = userName.ToUpper();
            return Users.Find(a => a.UserName.ToUpper() == userName);
        }
        public void Remove(string userName)
        {
            userName = userName.ToUpper();
            Users.RemoveAll(a => a.UserName.ToUpper() == userName);
        }
    }

    class User
    {
        public string UserName { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Password { get; set; } = "";  // ToHex(SHA256(salt+password))

        /// <summary>
        /// Create a new password with random salt, and password = SHA256 of salt+password
        /// </summary>
        public void ResetPassword(string password)
        {
            Salt = Util.GenerateSalt();
            using (var sha = new SHA256Managed())
                Password = Util.ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + password)));
        }

        public bool VerifyPassword(string challenge, string passwordHash)
        {
            using (var sha = new SHA256Managed())
                return passwordHash == Util.ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(challenge + Password)));
        }

    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

namespace Gosub.Viewtop
{
    public class UserFile
    {
        const string USER_FILE_NAME = "Users.json";
        static object sLock = new object();

        public List<User> Users { get; set; } = new List<User>();

        public static UserFile Load()
        {
            var fileName = Path.Combine(Application.CommonAppDataPath, USER_FILE_NAME);
            lock (sLock)
            {
                if (!File.Exists(fileName))
                    return new UserFile();
                return JsonConvert.DeserializeObject<UserFile>(File.ReadAllText(fileName));
            }
        }

        public static void Save(UserFile users)
        {
            var fileName = Path.Combine(Application.CommonAppDataPath, USER_FILE_NAME);
            lock (sLock)
            {
                File.WriteAllText(fileName, JsonConvert.SerializeObject(users));
                Util.SetWorldAccessControl(fileName);
            }
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

    public class User
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

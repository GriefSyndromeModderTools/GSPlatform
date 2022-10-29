using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSPlatformClient
{
    internal static unsafe class UserTokenStorage
    {
        private static readonly ConcurrentDictionary<string, string> _selectedUser = new ConcurrentDictionary<string, string>();

        //TODO name is no longer used as return value
        public static bool TryGetToken(string server, out string name, out string token, Form parentWindow)
        {
            CREDENTIAL** credentials = null;
            try
            {
                if (CredEnumerate($"gsplatform:{server}#*", 0, out var count, out credentials) == 0 ||
                    count == 0)
                {
                    //TODO log
                    name = null;
                    token = null;
                    return false;
                }

                if (count == 1)
                {
                    name = Marshal.PtrToStringUni(credentials[0]->UserName);
                    token = Marshal.PtrToStringAnsi(credentials[0]->CredentialBlob);
                    return true;
                }
                else
                {
                    var names = Enumerable.Range(0, count)
                        .Select(i => Marshal.PtrToStringUni(credentials[i]->UserName))
                        .ToArray();
                    var tokens = Enumerable.Range(0, count)
                        .Select(i => Marshal.PtrToStringAnsi(credentials[i]->CredentialBlob))
                        .ToArray();

                    int selectedIndex = -1;
                    if (_selectedUser.TryGetValue(server, out var selectedUser))
                    {
                        selectedIndex = Array.IndexOf(names, selectedUser);
                    }
                    if (selectedIndex == -1)
                    {
                        var dialog = new UserTokenSelectionDialog(names);
                        dialog.ShowDialog(parentWindow.Visible ? parentWindow : null);
                        selectedIndex = dialog.SelectedIndex;
                    }
                    name = names[selectedIndex];
                    token = tokens[selectedIndex];
                    _selectedUser[server] = name;
                    return true;
                }
            }
            catch
            {
                name = null;
                token = null;
                return false;
            }
            finally
            {
                if (credentials != null)
                {
                    CredFree(credentials);
                }
            }
        }

        public static void SetToken(string server, string userName, string token)
        {
            IntPtr serverPtr = IntPtr.Zero, namePtr = IntPtr.Zero, tokenPtr = IntPtr.Zero;
            try
            {
                var tokenBytes = Encoding.UTF8.GetBytes(token);
                var allocSize = Math.Max(tokenBytes.Length, 40);
                tokenPtr = Marshal.AllocHGlobal(allocSize);
                Marshal.Copy(tokenBytes, 0, tokenPtr, tokenBytes.Length);

                CREDENTIAL cred = new CREDENTIAL()
                {
                    Flags = 0,
                    Type = 1,
                    TargetName = (serverPtr = Marshal.StringToHGlobalUni($"gsplatform:{server}#0")),
                    CredentialBlob = tokenPtr,
                    CredentialBlobSize = allocSize,
                    Persist = 2,
                    AttributeCount = 0,
                    Attributes = IntPtr.Zero,
                    TargetAlias = IntPtr.Zero,
                    UserName = (namePtr = Marshal.StringToHGlobalUni(userName)),
                    Comment = IntPtr.Zero,
                    LastWritten = 0,
                };
                CredWrite(&cred, 0);
            }
            finally
            {
                if (serverPtr != IntPtr.Zero) Marshal.FreeHGlobal(serverPtr);
                if (namePtr != IntPtr.Zero) Marshal.FreeHGlobal(namePtr);
                if (tokenPtr != IntPtr.Zero) Marshal.FreeHGlobal(tokenPtr);
            }
        }

        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public ulong LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW")]
        private static extern int CredEnumerate([MarshalAs(UnmanagedType.LPWStr)] string filter,
            int flags, out int count, out CREDENTIAL** credential);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW")]
        private static extern int CredWrite(CREDENTIAL* credential, int flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(void* buffer);
    }
}

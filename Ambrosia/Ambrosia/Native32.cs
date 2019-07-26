
namespace mtcollections.persistent
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    using Microsoft.Win32.SafeHandles;
    using System.Threading;
    using System.Diagnostics;

    /// <summary>
    /// Interop with WINAPI for file I/O, threading, and NUMA functions.
    /// </summary>
    public static unsafe class Native32
    {
        #region io constants and flags

        public const uint INFINITE = unchecked((uint)-1);

        public const int ERROR_SUCCESS = 0;
        public const int ERROR_IO_PENDING = 997;
        public const uint ERROR_IO_INCOMPLETE = 996;
        public const uint ERROR_NOACCESS = 998;
        public const uint ERROR_HANDLE_EOF = 38;
        public const uint ERROR_PRIVILEGE_NOT_HELD = 1314;

        public const int ERROR_FILE_NOT_FOUND = 0x2;
        public const int ERROR_PATH_NOT_FOUND = 0x3;
        public const int ERROR_INVALID_DRIVE = 0x15;


        public const uint FILE_BEGIN = 0;
        public const uint FILE_CURRENT = 1;
        public const uint FILE_END = 2;

        public const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
        public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

        public const uint INVALID_HANDLE_VALUE = unchecked((uint)-1);

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint GENERIC_EXECUTE = 0x20000000;
        public const uint GENERIC_ALL = 0x10000000;

        public const uint READ_CONTROL = 0x00020000;
        public const uint FILE_READ_ATTRIBUTES = 0x0080;
        public const uint FILE_READ_DATA = 0x0001;
        public const uint FILE_READ_EA = 0x0008;
        public const uint STANDARD_RIGHTS_READ = READ_CONTROL;
        public const uint FILE_APPEND_DATA = 0x0004;
        public const uint FILE_WRITE_ATTRIBUTES = 0x0100;
        public const uint FILE_WRITE_DATA = 0x0002;
        public const uint FILE_WRITE_EA = 0x0010;
        public const uint STANDARD_RIGHTS_WRITE = READ_CONTROL;

        public const uint FILE_GENERIC_READ =
            FILE_READ_ATTRIBUTES
            | FILE_READ_DATA
            | FILE_READ_EA
            | STANDARD_RIGHTS_READ;
        public const uint FILE_GENERIC_WRITE =
            FILE_WRITE_ATTRIBUTES
            | FILE_WRITE_DATA
            | FILE_WRITE_EA
            | STANDARD_RIGHTS_WRITE
            | FILE_APPEND_DATA;

        public const uint FILE_SHARE_DELETE = 0x00000004;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;

        public const uint CREATE_ALWAYS = 2;
        public const uint CREATE_NEW = 1;
        public const uint OPEN_ALWAYS = 4;
        public const uint OPEN_EXISTING = 3;
        public const uint TRUNCATE_EXISTING = 5;

        public const uint FILE_FLAG_DELETE_ON_CLOSE = 0x04000000;
        public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        public const uint FILE_FLAG_OPEN_NO_RECALL = 0x00100000;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
        public const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;
        public const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;

        public const uint FILE_ATTRIBUTE_ENCRYPTED = 0x4000;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        const Int32 SE_PRIVILEGE_ENABLED = (Int32)(0x00000002L);
        const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
        const Int32 TOKEN_ADJUST_PRIVILEGES = (0x0020);

        /// <summary>
        /// Represents additional options for creating unbuffered overlapped file stream.
        /// </summary>
        [Flags]
        public enum UnbufferedFileOptions : uint
        {
            None = 0,
            WriteThrough = 0x80000000,
            DeleteOnClose = 0x04000000,
            OpenReparsePoint = 0x00200000,
            Overlapped = 0x40000000,
        }

        #endregion

        #region io functions

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(
            [In] string lpFileName,
            [In] UInt32 dwDesiredAccess,
            [In] UInt32 dwShareMode,
            [In] IntPtr lpSecurityAttributes,
            [In] UInt32 dwCreationDisposition,
            [In] UInt32 dwFlagsAndAttributes,
            [In] IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern void CloseHandle(
            [In] SafeHandle handle);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(
            [In] SafeFileHandle hFile,
            [Out] IntPtr lpBuffer,
            [In] UInt32 nNumberOfBytesToRead,
            [Out] out UInt32 lpNumberOfBytesRead,
            [In] NativeOverlapped* lpOverlapped);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(
            [In] SafeFileHandle hFile,
            [In] IntPtr lpBuffer,
            [In] UInt32 nNumberOfBytesToWrite,
            [Out] out UInt32 lpNumberOfBytesWritten,
            [In] NativeOverlapped* lpOverlapped);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(
            [In] SafeFileHandle hFile,
            [In] NativeOverlapped* lpOverlapped,
            [Out] out UInt32 lpNumberOfBytesTransferred,
            [In] bool bWait);


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct LUID
        {
            public Int32 LowPart;
            public Int32 HighPart;
        };

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public Int32 Attributes;
        };

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public Int32 PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        };


        public static bool EnableProcessPrivileges()
        {
            SafeFileHandle token;
            TOKEN_PRIVILEGES token_privileges = new TOKEN_PRIVILEGES();
            token_privileges.PrivilegeCount = 1;
            token_privileges.Privileges.Attributes = SE_PRIVILEGE_ENABLED;
            bool result = false;
            if (!LookupPrivilegeValue(null, SE_MANAGE_VOLUME_NAME, ref token_privileges.Privileges.Luid))
            {
                return false;
            }
            if (OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES, out token))
            {
                if (AdjustTokenPrivileges(token, false, ref token_privileges, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    uint rc = GetLastError();
                    if (rc == ERROR_SUCCESS)
                    {
                        result = true;
                    }
                }
                CloseHandle(token);
            }
            return result;
        }

        const Int32 USN_SOURCE_DATA_MANAGEMENT = (0x00000001);
        const Int32 MARK_HANDLE_PROTECT_CLUSTERS = (0x00000001);

        const Int32 FILE_DEVICE_FILE_SYSTEM = 0x00000009;
        const Int32 METHOD_BUFFERED = 0;
        const Int32 FILE_ANY_ACCESS = 0;

        static Int32 CTL_CODE(Int32 DeviceType, Int32 Function, Int32 Method, Int32 Access)
        {
            return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method);
        }


        public static bool EnableVolumePrivileges(string filename, SafeFileHandle file_handle)
        {
            string volume_string = "\\\\.\\" + filename.Substring(0, 2);
            SafeFileHandle volume_handle = CreateFileW(volume_string, 0, 0, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
            if (volume_handle.IsInvalid)
            {
                // std::cerr << "Error retrieving volume handle: " << FormatWin32AndHRESULT(::GetLastError());
                return false;
            }

            MARK_HANDLE_INFO mhi;
            mhi.UsnSourceInfo = USN_SOURCE_DATA_MANAGEMENT;
            mhi.VolumeHandle = volume_handle.DangerousGetHandle();
            mhi.HandleInfo = MARK_HANDLE_PROTECT_CLUSTERS;

            Int32 size = Marshal.SizeOf(mhi);
            IntPtr ptr = Marshal.AllocCoTaskMem(size);
            Marshal.StructureToPtr<MARK_HANDLE_INFO>(mhi, ptr, false);

            var FSCTL_MARK_HANDLE = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 63, METHOD_BUFFERED, FILE_ANY_ACCESS);
            Int32 bytes_returned = 0;
            bool result = DeviceIoControl(file_handle, FSCTL_MARK_HANDLE, ptr, size, IntPtr.Zero,
                0, out bytes_returned, IntPtr.Zero);

            CloseHandle(volume_handle);
            Marshal.FreeCoTaskMem(ptr);
            return true;
        }


        public static bool SetFileSize(SafeFileHandle file_handle, Int64 file_size)
        {
            LARGE_INTEGER li = new LARGE_INTEGER();
            li.QuadPart = file_size;

            LARGE_INTEGER li2 = new LARGE_INTEGER();

            bool result = SetFilePointerEx(file_handle, li, ref li2, FILE_BEGIN);
            if (!result)
            {
                Console.Error.WriteLine(string.Format("SetFilePointer failed with error: {0}", Marshal.GetHRForLastWin32Error()));
                return false;
            }

            // Set a fixed file length
            result = SetEndOfFile(file_handle);
            if (!result)
            {
                Console.Error.WriteLine(string.Format("SetEndOfFile failed with error: {0}", Marshal.GetHRForLastWin32Error()));
                return false;
            }

            result = SetFileValidData(file_handle, file_size);
            if (!result)
            {
                if (Marshal.GetLastWin32Error() == ERROR_PRIVILEGE_NOT_HELD)
                {
                    // please call EnableVolumePrivileges.
                    Console.Error.WriteLine("Please call EnableVolumePrivileges before SetFileSize");
                }
                else
                {
                    Console.Error.WriteLine(string.Format("SetFileValidData failed with error: {0}, hresult 0x{0:x}", Marshal.GetLastWin32Error(), Marshal.GetHRForLastWin32Error()));
                }
                return false;
            }
            return true;
        }



        public static bool CreateAndSetFileSize(string filename, Int64 file_size)
        {
            bool result = EnableProcessPrivileges();
            if (!result)
            {
                Console.Error.WriteLine("EnableProcessPrivileges failed with error: {0}, hresult {1:x}", Marshal.GetLastWin32Error(), Marshal.GetHRForLastWin32Error());
                return false;
            }

            uint desired_access = GENERIC_READ | GENERIC_WRITE;
            uint flags = FILE_FLAG_RANDOM_ACCESS | FILE_FLAG_NO_BUFFERING;
            uint create_disposition = CREATE_ALWAYS;
            uint shared_mode = FILE_SHARE_READ;

            // Create our test file
            SafeFileHandle file_handle = CreateFileW(filename, desired_access, shared_mode, IntPtr.Zero,
                create_disposition, flags, IntPtr.Zero);

            if (file_handle.IsInvalid)
            {
                Console.Error.WriteLine("CreateAndSetFileSize ({0}) file create failed: {1}, hresult {2:x}", filename, Marshal.GetLastWin32Error(), Marshal.GetHRForLastWin32Error());
                return false;
            }

            result = EnableVolumePrivileges(filename, file_handle);
            if (!result)
            {
                Console.Error.WriteLine("EnableVolumePrivileges ({0}) failed with error: {1}, hresult {2:x}", filename, Marshal.GetLastWin32Error(), Marshal.GetHRForLastWin32Error());
            }
            else
            {

                result = SetFileSize(file_handle, file_size);
                if (!result)
                {
                    Console.Error.WriteLine("SetFileSize ({0}) failed with error: {1}, hresult {2:x}", filename, Marshal.GetLastWin32Error(), Marshal.GetHRForLastWin32Error());
                }
            }

            CloseHandle(file_handle);
            return result;
        }


        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool SetFilePointerEx(
                [In] SafeFileHandle hFile,
                [In] LARGE_INTEGER liDistanceToMove,
                [In] ref LARGE_INTEGER lpNewFilePointer,
                [In] uint dwMoveMethod
                );

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct LARGE_INTEGER
        {
            public Int64 QuadPart;
        };

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            [In] SafeFileHandle hDevice,
            [In] Int32 dwIoControlCode,
            [In] IntPtr lpInBuffer,
            [In] Int32 nInBufferSize,
            [In] IntPtr lpOutBuffer,
            [In] Int32 nOutBufferSize,
            [Out] out Int32 lpBytesReturned,
            [In] IntPtr lpOverlapped
            );


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct MARK_HANDLE_INFO
        {
            public Int32 UsnSourceInfo;
            // Int32 CopyNumber; unioned with UsnSourceInfo
            public IntPtr VolumeHandle;
            public Int32 HandleInfo;
        };


        [DllImport("Advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(
            [In] SafeFileHandle TokenHandle,
            [In] bool DisableAllPrivileges,
            [In] ref TOKEN_PRIVILEGES NewState,
            [In] Int32 BufferLength,  // these are optional and IntPtr allows us to pass null.
            [In] IntPtr PreviousState,
            [In] IntPtr ReturnLength);

        [DllImport("Advapi32.dll", SetLastError = true)]
        public static extern bool LookupPrivilegeValue(
              [In] string lpSystemName,
              [In] string lpName,
              [In] ref LUID lpLuid);

        [DllImport("Advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(
            [In] IntPtr processHandle,
            [In] Int32 DesiredAccess,
            [Out] out SafeFileHandle TokenHandle);

        public enum EMoveMethod : uint
        {
            Begin = 0,
            Current = 1,
            End = 2
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetFilePointer(
              [In] SafeFileHandle hFile,
              [In] int lDistanceToMove,
              [In, Out] ref int lpDistanceToMoveHigh,
              [In] EMoveMethod dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetFilePointerEx(
              [In] SafeFileHandle hFile,
              [In] long lDistanceToMove,
              [In, Out] IntPtr lpDistanceToMoveHigh,
              [In] EMoveMethod dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetEndOfFile(
            [In] SafeFileHandle hFile);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetFileValidData(
            [In] SafeFileHandle hFile,
            [In] Int64 ValidDataLength);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateIoCompletionPort(
            [In] SafeFileHandle fileHandle,
            [In] IntPtr existingCompletionPort,
            [In] UInt32 completionKey,
            [In] UInt32 numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern UInt32 GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static unsafe extern bool GetQueuedCompletionStatus(
            [In] IntPtr completionPort,
            [Out] out UInt32 ptrBytesTransferred,
            [Out] out UInt32 ptrCompletionKey,
            [Out] NativeOverlapped** lpOverlapped,
            [In] UInt32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool PostQueuedCompletionStatus(
            [In] IntPtr completionPort,
            [In] UInt32 bytesTrasferred,
            [In] UInt32 completionKey,
            [In] IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetDiskFreeSpace(string lpRootPathName,
           out uint lpSectorsPerCluster,
           out uint lpBytesPerSector,
           out uint lpNumberOfFreeClusters,
           out uint lpTotalNumberOfClusters);
        #endregion

        #region thread and numa functions
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentThread();
        [DllImport("kernel32")]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetCurrentProcessorNumber();
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetActiveProcessorCount(uint count);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern ushort GetActiveProcessorGroupCount();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int SetThreadGroupAffinity(IntPtr hThread, ref GROUP_AFFINITY GroupAffinity, ref GROUP_AFFINITY PreviousGroupAffinity);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int GetThreadGroupAffinity(IntPtr hThread, ref GROUP_AFFINITY PreviousGroupAffinity);

        public static uint ALL_PROCESSOR_GROUPS = 0xffff;

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct GROUP_AFFINITY
        {
            public ulong Mask;
            public uint Group;
            public uint Reserved1;
            public uint Reserved2;
            public uint Reserved3;
        }

        /// <summary>
        /// Accepts thread id = 0, 1, 2, ... and sprays them round-robin
        /// across all cores (viewed as a flat space). On NUMA machines,
        /// this gives us [socket, core] ordering of affinitization. That is, 
        /// if there are N cores per socket, then thread indices of 0 to N-1 map
        /// to the range [socket 0, core 0] to [socket 0, core N-1].
        /// </summary>
        /// <param name="threadIdx">Index of thread (from 0 onwards)</param>
        public static void AffinitizeThreadRoundRobin(uint threadIdx)
        {
            uint nrOfProcessors = GetActiveProcessorCount(ALL_PROCESSOR_GROUPS);
            ushort nrOfProcessorGroups = GetActiveProcessorGroupCount();
            uint nrOfProcsPerGroup = nrOfProcessors / nrOfProcessorGroups;

            GROUP_AFFINITY groupAffinityThread = default(GROUP_AFFINITY);
            GROUP_AFFINITY oldAffinityThread = default(GROUP_AFFINITY);

            IntPtr thread = GetCurrentThread();
            GetThreadGroupAffinity(thread, ref groupAffinityThread);

            threadIdx = threadIdx % nrOfProcessors;

            groupAffinityThread.Mask = (ulong)1L << ((int)(threadIdx % (int)nrOfProcsPerGroup));
            groupAffinityThread.Group = (uint)(threadIdx / nrOfProcsPerGroup);

            if (SetThreadGroupAffinity(thread, ref groupAffinityThread, ref oldAffinityThread) == 0)
            {
                Console.WriteLine("Unable to set group affinity");
            }
        }
        #endregion
    }

    /// <summary>
    /// Methods to perform high-resolution low-overhead timing
    /// </summary>
    public static class HiResTimer
    {
        private const string lib = "kernel32.dll";
        [DllImport(lib)]
        [SuppressUnmanagedCodeSecurity]
        public static extern int QueryPerformanceCounter(ref Int64 count);

        [DllImport(lib)]
        [SuppressUnmanagedCodeSecurity]
        public static extern int QueryPerformanceFrequency(ref Int64 frequency);

        [DllImport(lib)]
        [SuppressUnmanagedCodeSecurity]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        [DllImport(lib)]
        [SuppressUnmanagedCodeSecurity]
        private static extern void GetSystemTimeAsFileTime(out long filetime);

        [DllImport("readtsc.dll")]
        [SuppressUnmanagedCodeSecurity]
        public static extern ulong rdtsc();

        public static long Freq;

        public static long EstimateCPUFrequency()
        {
            long oldCps = 0, cps = 0, startT, endT;
            ulong startC, endC;
            long accuracy = 500; // wait for consecutive measurements to get within 300 clock cycles

            int i = 0;
            while (i < 5)
            {
                GetSystemTimeAsFileTime(out startT);
                startC = rdtsc();

                while (true)
                {
                    GetSystemTimeAsFileTime(out endT);
                    endC = rdtsc();

                    if (endT - startT >= 10000000)
                    {
                        cps = (long)(10000000 * (endC - startC) / (double)(endT - startT));
                        break;
                    }
                }


                if ((oldCps > (cps - accuracy)) && (oldCps < (cps + accuracy)))
                {
                    Freq = cps;
                    return cps;
                }
                oldCps = cps;
                i++;
            }
            Freq = cps;
            return cps;
        }
    }
}

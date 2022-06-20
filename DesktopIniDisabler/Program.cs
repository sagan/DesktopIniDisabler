// RemoteDesktopIniDisabler (File: DesktopIniDisabler\Program.cs)
//
// Copyright (c) 2017 Justin Stenning
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// Please visit https://easyhook.github.io for more information
// about the project, latest updates and other tutorials.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace DesktopIniDisabler
{
    public static class ProcessWatcher
    {
        public static void DoWork()
        {
            WMI.Win32.ProcessWatcher procWatcher = new WMI.Win32.ProcessWatcher(Program.processes);
            procWatcher.ProcessCreated += new WMI.Win32.ProcessEventHandler(procWatcher_ProcessCreated);
            Console.WriteLine("--watcher start");
            procWatcher.Start();
            while (true)
            {
                procWatcher.WaitForNextEvent();
                if(Program.context.form.IsHandleCreated )
                {
                    Program.context.form.Invoke((MethodInvoker)delegate {
                        // Running on the UI thread
                        Program.context.form.refresh();
                    });
                }

            }
            procWatcher.Stop();
        }
        static void procWatcher_ProcessCreated(WMI.Win32.Win32_Process process)
        {
            //Console.WriteLine("---Created " + process.Name + " " + process.ProcessId + " " + "DateTime:" + DateTime.Now);
            Program.logger("detected process creation " + process.Name + " " + process.ProcessId);
            Program.Inject((int)process.ProcessId);
        }
    }
    public static class Program
    {
        public static String log { get; set; }

        public static string[] processes = { "explorer", "GoogleDriveFS" };

        public static MyCustomApplicationContext context;

        static void Main(string[] args)
        {
            log = "Start";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            context = new MyCustomApplicationContext();
            Thread thread1 = new Thread(ProcessWatcher.DoWork);
            thread1.Start();
            foreach (String process in processes)
            {
                var p = Process.GetProcessesByName(process);
                for (int i = 0; i < p.Length; i++)
                {
                    logger("find process " + process + " " + p[i].Id);
                    Inject(p[i].Id);
                }
            }
            Application.Run(context);
        }

        public static void Inject(Int32 targetPID)
        {
            if(targetPID == 0)
            {
                var p = Process.GetProcessesByName("explorer");
                if (p.Length > 0)
                {
                    targetPID = p[0].Id;
                    logger("found explorer.exe pid " + targetPID);
                }
                else
                {
                    logger("explorer.exe not found");
                }
            }
  
            // Will contain the name of the IPC server channel
            string channelName = null;
            // Create the IPC server using the DesktopIniDisablerIPC.ServiceInterface class as a singleton
            EasyHook.RemoteHooking.IpcCreateServer<DesktopIniDisablerHook.ServerInterface>(ref channelName, System.Runtime.Remoting.WellKnownObjectMode.Singleton);

            // Get the full path to the assembly we want to inject into the target process
            string injectionLibrary = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "DesktopIniDisablerHook.dll");

            try
            {
                // Injecting into existing process by Id
                if (targetPID > 0)
                {
                    logger("inject pid " + targetPID);
                    // inject into existing process
                    EasyHook.RemoteHooking.Inject(
                        targetPID,          // ID of process to inject into
                        injectionLibrary,   // 32-bit library to inject (if target is 32-bit)
                        injectionLibrary,   // 64-bit library to inject (if target is 64-bit)
                        channelName         // the parameters to pass into injected library
                                            // ...
                    );
                }
                else
                {
                   logger("Attempting to create and inject into explorer.exe");
                    // start and inject into a new process
                    EasyHook.RemoteHooking.CreateAndInject(
                       Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "/explorer.exe",          // executable to run
                        "",                 // command line arguments for target
                        0,                  // additional process creation flags to pass to CreateProcess
                        EasyHook.InjectionOptions.DoNotRequireStrongName, // allow injectionLibrary to be unsigned
                        injectionLibrary,   // 32-bit library to inject (if target is 32-bit)
                        injectionLibrary,   // 64-bit library to inject (if target is 64-bit)
                        out targetPID,      // retrieve the newly created process ID
                        channelName         // the parameters to pass into injected library
                                            // ...
                    );
                }
            }
            catch (Exception e)
            {
                logger("Error while injectint into target " + e.ToString());
            }

        }
        public static void logger(String msg)
        {
            Console.WriteLine(msg);
            log += "\r\n" + msg;
            if ( log.Length > 10000 )
            {
                log = log.Substring(5000);
            }
        }
    }

    // https://stackoverflow.com/questions/995195/how-can-i-make-a-net-windows-forms-application-that-only-runs-in-the-system-tra
    public class MyCustomApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        public Form1 form = new Form1();

        public MyCustomApplicationContext()
        {
            // Initialize Tray Icon
            trayIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.AppIcon,
                ContextMenu = new ContextMenu(new MenuItem[] {
                    new MenuItem("Show", Show),
                    new MenuItem("Inject", Inject),
                    new MenuItem("Exit", Exit)
                }),
                Text = "DesktopIniDisabler",
                Visible = true
            };
            trayIcon.Click += Show;
        }

        void Inject(object sender, EventArgs e)
        {
            Program.Inject(0);
            form.refresh();
        }
        void Show(object sender, EventArgs e)
        {
            form.refresh();
            form.Show();
            form.Activate();
        }
        void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;
            Application.Exit();
            Environment.Exit(Environment.ExitCode);
        }
    }
}

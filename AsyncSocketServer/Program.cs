using System;
using System.ServiceProcess;
//
using Common.Logging;
// for install and uninstall
using System.Configuration.Install;
using System.Reflection;

namespace AsyncSocketServer
{
    static class Program
    {
        private static ILog log = LogManager.GetLogger(typeof(Program));
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            if (System.Environment.UserInteractive)
            {
                string paramater = string.Concat(args);
                switch (paramater)
                {
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                        break;
                    case "--uninstall":
                        ManagedInstallerClass.InstallHelper(new String[] { "/u", Assembly.GetExecutingAssembly().Location });
                        break;
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] 
                { 
                    new AsyncService() 
                };
                ServiceBase.Run(ServicesToRun);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e != null && e.ExceptionObject != null)
            {
                log.Error("Error: " + e.ExceptionObject);
            }
        }
    }
}

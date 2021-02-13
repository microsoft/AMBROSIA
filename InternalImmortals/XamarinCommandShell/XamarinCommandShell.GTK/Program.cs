using System;
using Xamarin.Forms;
using Xamarin.Forms.Platform.GTK;
//using XamarinCommandShell;

namespace XamarinCommandShell.GTK
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
             Gtk.Application.Init();
            Forms.Init();
            var app = new App(new OSCommands());
            var window = new FormsWindow();
            window.LoadApplication(app);
            window.SetApplicationTitle("XamarinCommandShell");
            window.Show();
            Gtk.Application.Run();
        }
    }
}
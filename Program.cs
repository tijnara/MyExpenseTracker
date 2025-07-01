using System;
using System.Windows.Forms;

namespace MyExpenseTracker
{
    internal static class Program
    {
        /// <summary>  
        /// The main entry point for the application.  
        /// </summary>  
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Create an instance of Form1 to pass as an argument to Form2.  
            Form1 form1 = new Form1();

            // Start the app with Form2 as the main form, passing the required Form1 instance.  
            Application.Run(new Form2(form1));
        }
    }
}

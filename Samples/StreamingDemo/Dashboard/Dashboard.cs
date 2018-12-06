using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ambrosia;
using DashboardAPI;
using TwitterObservable;
using static Ambrosia.ServiceConfiguration;

namespace DashboardFancy
{
    public partial class Dashboard : Form
    {
        static internal Dashboard dash = null;

        IDisposable _container;
        public Dashboard()
        {
            dash = this;
            InitializeComponent();
            int receivePort = 1001;
            int sendPort = 1000;
            var myClient = new TwitterDashboard(this);
            new Thread(new ThreadStart(() => _container = AmbrosiaFactory.Deploy<IDashboard>(DashboardServiceName, myClient, receivePort, sendPort))).Start();
        }

        internal string getText()
        {
            return output_textbox.Text;
        }

        internal RichTextBox Output_box()
        {
            return output_textbox;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            throw new Exception("Exiting");
        }
    }

    [DataContract]
    class TwitterDashboard : Immortal, IDashboard
    {
        Dashboard _windowsDash;
        [DataMember]
        string _text;
       
        StringArgReturningVoidDelegate _appendText;
        VoidArgReturningStringDelegate _getText;

        public TwitterDashboard(Dashboard windowsDash)
        {
            _windowsDash = windowsDash;
            _appendText = new StringArgReturningVoidDelegate(_windowsDash.Output_box().AppendText);
            _getText = new VoidArgReturningStringDelegate(_windowsDash.getText);
        }

        protected override void OnRestore(Stream stream)
        {
            _windowsDash = Dashboard.dash;
            _appendText = new StringArgReturningVoidDelegate(_windowsDash.Output_box().AppendText);
            _getText = new VoidArgReturningStringDelegate(_windowsDash.getText);
            if (_text != null)
            {
                _windowsDash.Output_box().Invoke(_appendText, _text);
            }
        }

        protected override async Task<bool> OnFirstStart()
        {
            return true;
        }

        delegate void StringArgReturningVoidDelegate(string text);
        delegate string VoidArgReturningStringDelegate();

        public async Task OnNextAsync(AnalyticsResultString next)
        {
            if (_text == "")
            {
                _windowsDash.Output_box().Invoke(_appendText, "Timestamp\tTopic\tSentiment Score\n\n");
            }
            _windowsDash.Output_box().Invoke(_appendText, next.topkTopics + "\n");
            _text = (string)_windowsDash.Invoke(_getText);
        }
    }
}

namespace PhishingDataCollector
{
    partial class LaunchRibbon : Microsoft.Office.Tools.Ribbon.RibbonBase
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public LaunchRibbon()
            : base(Globals.Factory.GetRibbonFactory())
        {
            InitializeComponent();
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tab1 = this.Factory.CreateRibbonTab();
            this.group1 = this.Factory.CreateRibbonGroup();
            this.LaunchPluginBtn = this.Factory.CreateRibbonButton();
            this.StateBtn = this.Factory.CreateRibbonButton();
            this.AboutBtn = this.Factory.CreateRibbonButton();
            this.tab1.SuspendLayout();
            this.group1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tab1
            // 
            this.tab1.Groups.Add(this.group1);
            this.tab1.Label = "Dataset Collector";
            this.tab1.Name = "tab1";
            // 
            // group1
            // 
            this.group1.Items.Add(this.LaunchPluginBtn);
            this.group1.Items.Add(this.StateBtn);
            this.group1.Items.Add(this.AboutBtn);
            this.group1.Label = "Mail Data Collector";
            this.group1.Name = "group1";
            // 
            // LaunchPluginBtn
            // 
            this.LaunchPluginBtn.Image = global::PhishingDataCollector.Properties.Resources.uniba_logo;
            this.LaunchPluginBtn.Label = "Esegui Plugin";
            this.LaunchPluginBtn.Name = "LaunchPluginBtn";
            this.LaunchPluginBtn.ShowImage = true;
            this.LaunchPluginBtn.SuperTip = "Lancia il processo di collezionamento dati";
            this.LaunchPluginBtn.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.LaunchButton_Click);
            // 
            // StateBtn
            // 
            this.StateBtn.Image = global::PhishingDataCollector.Properties.Resources.uniba_logo;
            this.StateBtn.Label = "Stato avanzamento";
            this.StateBtn.Name = "StateBtn";
            this.StateBtn.ShowImage = true;
            this.StateBtn.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.StateButton_Click);
            // 
            // AboutBtn
            // 
            this.AboutBtn.Image = global::PhishingDataCollector.Properties.Resources.uniba_logo;
            this.AboutBtn.Label = "Mostra ID client";
            this.AboutBtn.Name = "AboutBtn";
            this.AboutBtn.ShowImage = true;
            this.AboutBtn.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.AboutButton_Click);
            // 
            // LaunchRibbon
            // 
            this.Name = "LaunchRibbon";
            this.RibbonType = "Microsoft.Outlook.Mail.Read";
            this.Tabs.Add(this.tab1);
            this.Load += new Microsoft.Office.Tools.Ribbon.RibbonUIEventHandler(this.LaunchRibbon_Load);
            this.tab1.ResumeLayout(false);
            this.tab1.PerformLayout();
            this.group1.ResumeLayout(false);
            this.group1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        internal Microsoft.Office.Tools.Ribbon.RibbonTab tab1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton LaunchPluginBtn;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup group1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton StateBtn;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton AboutBtn;
    }

    partial class ThisRibbonCollection
    {
        internal LaunchRibbon LaunchRibbon
        {
            get { return this.GetRibbon<LaunchRibbon>(); }
        }
    }
}

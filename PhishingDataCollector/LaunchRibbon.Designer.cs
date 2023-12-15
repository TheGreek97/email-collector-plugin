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
            this.options = this.Factory.CreateRibbonGroup();
            this.editBox1 = this.Factory.CreateRibbonEditBox();
            this.LimitResourcesCheckbox = this.Factory.CreateRibbonCheckBox();
            this.group2 = this.Factory.CreateRibbonGroup();
            this.button2 = this.Factory.CreateRibbonButton();
            this.button1 = this.Factory.CreateRibbonButton();
            this.tab1.SuspendLayout();
            this.group1.SuspendLayout();
            this.options.SuspendLayout();
            this.group2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tab1
            // 
            this.tab1.Groups.Add(this.group1);
            this.tab1.Groups.Add(this.options);
            this.tab1.Groups.Add(this.group2);
            this.tab1.Label = "Dataset Collector";
            this.tab1.Name = "tab1";
            // 
            // group1
            // 
            this.group1.Items.Add(this.LaunchPluginBtn);
            this.group1.Items.Add(this.StateBtn);
            this.group1.Items.Add(this.AboutBtn);
            this.group1.Label = "Comandi";
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
            // options
            // 
            this.options.Items.Add(this.editBox1);
            this.options.Items.Add(this.LimitResourcesCheckbox);
            this.options.Label = "Opzioni";
            this.options.Name = "options";
            // 
            // editBox1
            // 
            this.editBox1.Label = "Max. email da processare";
            this.editBox1.MaxLength = 5;
            this.editBox1.Name = "editBox1";
            this.editBox1.SuperTip = "Imposta il numero di email massimo da processare in un\'esecuzione del plugin (def" +
    "ault=10000). Se riscontri problemi o il processo impiega troppo tempo, modifica " +
    "questo valore con uno più basso";
            this.editBox1.Text = null;
            this.editBox1.TextChanged += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.editBox1_TextChanged);
            // 
            // LimitResourcesCheckbox
            // 
            this.LimitResourcesCheckbox.Label = "Esegui in modalità risparmio risorse";
            this.LimitResourcesCheckbox.Name = "LimitResourcesCheckbox";
            this.LimitResourcesCheckbox.ScreenTip = "Spunta questa casella se vuoi far utilizzare meno risorse hardware - utile se il " +
    "plugin causa un crash di Outlook (se spuntato, la velocità di esecuzione sarà li" +
    "mitata)";
            this.LimitResourcesCheckbox.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.LimitResourcesCheckbox_Click);
            // 
            // group2
            // 
            this.group2.Items.Add(this.button1);
            this.group2.Items.Add(this.button2);
            this.group2.Label = "Altro";
            this.group2.Name = "group2";
            // 
            // button2
            // 
            this.button2.Image = global::PhishingDataCollector.Properties.Resources.uniba_logo;
            this.button2.Label = "Manda Dati di Diagnostica";
            this.button2.Name = "button2";
            this.button2.ShowImage = true;
            this.button2.SuperTip = "Manda dati anonimi di diagnostica ai nostri server per migliorare il funzionament" +
    "o del programma";
            this.button2.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.Image = global::PhishingDataCollector.Properties.Resources.uniba_logo;
            this.button1.Label = "Manda email processate in precedenza";
            this.button1.Name = "button1";
            this.button1.ScreenTip = "Se hai già lanciato l\'addin in precedenza, ma la trasmissione non è stata complet" +
    "ata, usa questo tasto per mandare velocemente le mail già elaborate";
            this.button1.ShowImage = true;
            this.button1.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.button1_Click);
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
            this.options.ResumeLayout(false);
            this.options.PerformLayout();
            this.group2.ResumeLayout(false);
            this.group2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        internal Microsoft.Office.Tools.Ribbon.RibbonTab tab1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton LaunchPluginBtn;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup group1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton StateBtn;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton AboutBtn;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup options;
        internal Microsoft.Office.Tools.Ribbon.RibbonCheckBox LimitResourcesCheckbox;
        internal Microsoft.Office.Tools.Ribbon.RibbonGroup group2;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton button2;
        internal Microsoft.Office.Tools.Ribbon.RibbonEditBox editBox1;
        internal Microsoft.Office.Tools.Ribbon.RibbonButton button1;
    }

    partial class ThisRibbonCollection
    {
        internal LaunchRibbon LaunchRibbon
        {
            get { return this.GetRibbon<LaunchRibbon>(); }
        }
    }
}

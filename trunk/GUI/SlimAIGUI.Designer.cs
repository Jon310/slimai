namespace SlimAI.GUI
{
    partial class SlimAIGUI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.menuToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveExitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.Generaltab = new System.Windows.Forms.TabPage();
            this.ClassSpecifictab = new System.Windows.Forms.TabPage();
            this.Hotkeystab = new System.Windows.Forms.TabPage();
            this.GeneralpropertyGrid = new System.Windows.Forms.PropertyGrid();
            this.ClasspropertyGrid = new System.Windows.Forms.PropertyGrid();
            this.HotkeypropertyGrid = new System.Windows.Forms.PropertyGrid();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.menuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.Generaltab.SuspendLayout();
            this.ClassSpecifictab.SuspendLayout();
            this.Hotkeystab.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(308, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // menuToolStripMenuItem
            // 
            this.menuToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveExitToolStripMenuItem});
            this.menuToolStripMenuItem.Name = "menuToolStripMenuItem";
            this.menuToolStripMenuItem.Size = new System.Drawing.Size(50, 20);
            this.menuToolStripMenuItem.Text = "Menu";
            // 
            // saveExitToolStripMenuItem
            // 
            this.saveExitToolStripMenuItem.Name = "saveExitToolStripMenuItem";
            this.saveExitToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveExitToolStripMenuItem.Text = "Save + Exit";
            this.saveExitToolStripMenuItem.Click += new System.EventHandler(this.saveExitToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.Generaltab);
            this.tabControl1.Controls.Add(this.ClassSpecifictab);
            this.tabControl1.Controls.Add(this.Hotkeystab);
            this.tabControl1.Location = new System.Drawing.Point(0, 27);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(308, 390);
            this.tabControl1.TabIndex = 1;
            // 
            // Generaltab
            // 
            this.Generaltab.Controls.Add(this.GeneralpropertyGrid);
            this.Generaltab.Location = new System.Drawing.Point(4, 22);
            this.Generaltab.Name = "Generaltab";
            this.Generaltab.Padding = new System.Windows.Forms.Padding(3);
            this.Generaltab.Size = new System.Drawing.Size(300, 364);
            this.Generaltab.TabIndex = 0;
            this.Generaltab.Text = "General";
            this.Generaltab.UseVisualStyleBackColor = true;
            // 
            // ClassSpecifictab
            // 
            this.ClassSpecifictab.Controls.Add(this.ClasspropertyGrid);
            this.ClassSpecifictab.Location = new System.Drawing.Point(4, 22);
            this.ClassSpecifictab.Name = "ClassSpecifictab";
            this.ClassSpecifictab.Padding = new System.Windows.Forms.Padding(3);
            this.ClassSpecifictab.Size = new System.Drawing.Size(300, 364);
            this.ClassSpecifictab.TabIndex = 1;
            this.ClassSpecifictab.Text = "Class Specific";
            this.ClassSpecifictab.UseVisualStyleBackColor = true;
            // 
            // Hotkeystab
            // 
            this.Hotkeystab.Controls.Add(this.HotkeypropertyGrid);
            this.Hotkeystab.Location = new System.Drawing.Point(4, 22);
            this.Hotkeystab.Name = "Hotkeystab";
            this.Hotkeystab.Padding = new System.Windows.Forms.Padding(3);
            this.Hotkeystab.Size = new System.Drawing.Size(300, 364);
            this.Hotkeystab.TabIndex = 2;
            this.Hotkeystab.Text = "Hotkeys";
            this.Hotkeystab.UseVisualStyleBackColor = true;
            // 
            // GeneralpropertyGrid
            // 
            this.GeneralpropertyGrid.Location = new System.Drawing.Point(3, 3);
            this.GeneralpropertyGrid.Name = "GeneralpropertyGrid";
            this.GeneralpropertyGrid.Size = new System.Drawing.Size(294, 358);
            this.GeneralpropertyGrid.TabIndex = 0;
            // 
            // ClasspropertyGrid
            // 
            this.ClasspropertyGrid.Location = new System.Drawing.Point(3, 3);
            this.ClasspropertyGrid.Name = "ClasspropertyGrid";
            this.ClasspropertyGrid.Size = new System.Drawing.Size(294, 358);
            this.ClasspropertyGrid.TabIndex = 0;
            // 
            // HotkeypropertyGrid
            // 
            this.HotkeypropertyGrid.Location = new System.Drawing.Point(3, 3);
            this.HotkeypropertyGrid.Name = "HotkeypropertyGrid";
            this.HotkeypropertyGrid.Size = new System.Drawing.Size(294, 358);
            this.HotkeypropertyGrid.TabIndex = 0;
            // 
            // SlimAIGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(308, 419);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Name = "SlimAIGUI";
            this.Text = "SlimAIGUI";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfigurationForm_FormClosing);
            this.Load += new System.EventHandler(this.ConfigurationForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.Generaltab.ResumeLayout(false);
            this.ClassSpecifictab.ResumeLayout(false);
            this.Hotkeystab.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem menuToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveExitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage Generaltab;
        private System.Windows.Forms.PropertyGrid GeneralpropertyGrid;
        private System.Windows.Forms.TabPage ClassSpecifictab;
        private System.Windows.Forms.PropertyGrid ClasspropertyGrid;
        private System.Windows.Forms.TabPage Hotkeystab;
        private System.Windows.Forms.PropertyGrid HotkeypropertyGrid;
        private System.Windows.Forms.Timer timer1;
    }
}
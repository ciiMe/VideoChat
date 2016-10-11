namespace MF_BasicPlayback
{
    partial class Form1
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
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openUrlToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.videoEffectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.grayscaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.grayscaleAsyncToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.writeTextToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rotateAsyncToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.typeConverterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.frameCounterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.frameCounterAsyncToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.grayscaleCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.audioToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.normalToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.muteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.audioDelayAsyncToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.delayToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.videoEffectToolStripMenuItem,
            this.audioToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(284, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.openUrlToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.openToolStripMenuItem.Text = "&Open File";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // openUrlToolStripMenuItem
            // 
            this.openUrlToolStripMenuItem.Name = "openUrlToolStripMenuItem";
            this.openUrlToolStripMenuItem.ShowShortcutKeys = false;
            this.openUrlToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.openUrlToolStripMenuItem.Text = "Open &Url";
            this.openUrlToolStripMenuItem.Click += new System.EventHandler(this.openUrlToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(121, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // videoEffectToolStripMenuItem
            // 
            this.videoEffectToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.noneToolStripMenuItem,
            this.grayscaleToolStripMenuItem,
            this.grayscaleAsyncToolStripMenuItem,
            this.writeTextToolStripMenuItem,
            this.rotateAsyncToolStripMenuItem,
            this.typeConverterToolStripMenuItem,
            this.frameCounterToolStripMenuItem,
            this.frameCounterAsyncToolStripMenuItem,
            this.toolStripSeparator2,
            this.grayscaleCToolStripMenuItem});
            this.videoEffectToolStripMenuItem.Name = "videoEffectToolStripMenuItem";
            this.videoEffectToolStripMenuItem.Size = new System.Drawing.Size(82, 20);
            this.videoEffectToolStripMenuItem.Tag = "2137B262-D5D7-4F81-AE90-D3A2ECC66E14";
            this.videoEffectToolStripMenuItem.Text = "Video Effect";
            // 
            // noneToolStripMenuItem
            // 
            this.noneToolStripMenuItem.Checked = true;
            this.noneToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.noneToolStripMenuItem.Name = "noneToolStripMenuItem";
            this.noneToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.noneToolStripMenuItem.Text = "None";
            this.noneToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // grayscaleToolStripMenuItem
            // 
            this.grayscaleToolStripMenuItem.Name = "grayscaleToolStripMenuItem";
            this.grayscaleToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.grayscaleToolStripMenuItem.Tag = "69042198-8146-4735-90F0-BEFD5BFAEDB7";
            this.grayscaleToolStripMenuItem.Text = "Grayscale";
            this.grayscaleToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // grayscaleAsyncToolStripMenuItem
            // 
            this.grayscaleAsyncToolStripMenuItem.Name = "grayscaleAsyncToolStripMenuItem";
            this.grayscaleAsyncToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.grayscaleAsyncToolStripMenuItem.Tag = "E6AAA34E-A092-418A-A037-6634EED63CB5";
            this.grayscaleAsyncToolStripMenuItem.Text = "GrayscaleAsync";
            this.grayscaleAsyncToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // writeTextToolStripMenuItem
            // 
            this.writeTextToolStripMenuItem.Name = "writeTextToolStripMenuItem";
            this.writeTextToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.writeTextToolStripMenuItem.Tag = "FBC659ED-BACD-4F8F-9560-EC26319C935C";
            this.writeTextToolStripMenuItem.Text = "WriteText";
            this.writeTextToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // rotateAsyncToolStripMenuItem
            // 
            this.rotateAsyncToolStripMenuItem.Name = "rotateAsyncToolStripMenuItem";
            this.rotateAsyncToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.rotateAsyncToolStripMenuItem.Tag = "C2206097-AE39-44BD-99D0-4226205753FC";
            this.rotateAsyncToolStripMenuItem.Text = "RotateAsync";
            this.rotateAsyncToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // typeConverterToolStripMenuItem
            // 
            this.typeConverterToolStripMenuItem.Name = "typeConverterToolStripMenuItem";
            this.typeConverterToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.typeConverterToolStripMenuItem.Tag = "567C527F-9025-4057-BE42-527554D10ADE";
            this.typeConverterToolStripMenuItem.Text = "TypeConverterAsync";
            this.typeConverterToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // frameCounterToolStripMenuItem
            // 
            this.frameCounterToolStripMenuItem.Name = "frameCounterToolStripMenuItem";
            this.frameCounterToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.frameCounterToolStripMenuItem.Tag = "FC8AFE7E-2624-4437-A6B8-D071C862A52B";
            this.frameCounterToolStripMenuItem.Text = "FrameCounter";
            this.frameCounterToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // frameCounterAsyncToolStripMenuItem
            // 
            this.frameCounterAsyncToolStripMenuItem.Name = "frameCounterAsyncToolStripMenuItem";
            this.frameCounterAsyncToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.frameCounterAsyncToolStripMenuItem.Tag = "2137B262-D5D7-4F81-AE90-D3A2ECC66E14";
            this.frameCounterAsyncToolStripMenuItem.Text = "FrameCounterAsync";
            this.frameCounterAsyncToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(181, 6);
            // 
            // grayscaleCToolStripMenuItem
            // 
            this.grayscaleCToolStripMenuItem.Name = "grayscaleCToolStripMenuItem";
            this.grayscaleCToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.grayscaleCToolStripMenuItem.Tag = "2F3DBC05-C011-4a8f-B264-E42E35C67BF4";
            this.grayscaleCToolStripMenuItem.Text = "Grayscale c++";
            this.grayscaleCToolStripMenuItem.Click += new System.EventHandler(this.VideoToolStripMenuItem_Click);
            // 
            // audioToolStripMenuItem
            // 
            this.audioToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.normalToolStripMenuItem,
            this.muteToolStripMenuItem,
            this.audioDelayAsyncToolStripMenuItem,
            this.toolStripSeparator3,
            this.delayToolStripMenuItem});
            this.audioToolStripMenuItem.Name = "audioToolStripMenuItem";
            this.audioToolStripMenuItem.Size = new System.Drawing.Size(84, 20);
            this.audioToolStripMenuItem.Tag = "8F17BC18-4242-40E8-BEE8-06FD8B11EB33";
            this.audioToolStripMenuItem.Text = "Audio Effect";
            // 
            // normalToolStripMenuItem
            // 
            this.normalToolStripMenuItem.Checked = true;
            this.normalToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.normalToolStripMenuItem.Name = "normalToolStripMenuItem";
            this.normalToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.normalToolStripMenuItem.Text = "Normal";
            this.normalToolStripMenuItem.Click += new System.EventHandler(this.AudioToolStripMenuItem_Click);
            // 
            // muteToolStripMenuItem
            // 
            this.muteToolStripMenuItem.Name = "muteToolStripMenuItem";
            this.muteToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.muteToolStripMenuItem.Text = "Disabled";
            this.muteToolStripMenuItem.Click += new System.EventHandler(this.AudioToolStripMenuItem_Click);
            // 
            // audioDelayAsyncToolStripMenuItem
            // 
            this.audioDelayAsyncToolStripMenuItem.Name = "audioDelayAsyncToolStripMenuItem";
            this.audioDelayAsyncToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.audioDelayAsyncToolStripMenuItem.Tag = "8F17BC18-4242-40E8-BEE8-06FD8B11EB33";
            this.audioDelayAsyncToolStripMenuItem.Text = "AudioDelayAsync";
            this.audioDelayAsyncToolStripMenuItem.Click += new System.EventHandler(this.AudioToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(164, 6);
            // 
            // delayToolStripMenuItem
            // 
            this.delayToolStripMenuItem.Name = "delayToolStripMenuItem";
            this.delayToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.delayToolStripMenuItem.Tag = "5B91187F-3C42-409e-A8C9-7F637708D724";
            this.delayToolStripMenuItem.Text = "AudioDelay c++";
            this.delayToolStripMenuItem.Click += new System.EventHandler(this.AudioToolStripMenuItem_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 264);
            this.Controls.Add(this.menuStrip1);
            this.KeyPreview = true;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Form1_Paint);
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Form1_KeyPress);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openUrlToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem videoEffectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem grayscaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem grayscaleAsyncToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem grayscaleCToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem audioToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem muteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem typeConverterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem frameCounterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem frameCounterAsyncToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem normalToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem delayToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem audioDelayAsyncToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem writeTextToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem rotateAsyncToolStripMenuItem;
    }
}


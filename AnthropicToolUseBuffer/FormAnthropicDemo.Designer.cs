namespace AnthropicToolUseBuffer
{
    partial class FormAnthropicDemo
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
            rtbLog = new RichTextBox();
            btnSend = new Button();
            textRequest = new TextBox();
            menuStrip1 = new MenuStrip();
            toolStripMenuItem1 = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            exitToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // rtbLog
            // 
            rtbLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtbLog.BackColor = Color.FromArgb(213, 213, 213);
            rtbLog.BorderStyle = BorderStyle.None;
            rtbLog.Location = new Point(3, 27);
            rtbLog.Name = "rtbLog";
            rtbLog.Size = new Size(716, 179);
            rtbLog.TabIndex = 0;
            rtbLog.Text = "";
            // 
            // btnSend
            // 
            btnSend.Anchor = AnchorStyles.None;
            btnSend.FlatAppearance.MouseDownBackColor = Color.FromArgb(84, 86, 97);
            btnSend.FlatAppearance.MouseOverBackColor = Color.Silver;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.Location = new Point(73, 66);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(75, 32);
            btnSend.TabIndex = 1;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Visible = false;
            btnSend.Click += btnSend_Click;
            // 
            // textRequest
            // 
            textRequest.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textRequest.BackColor = Color.FromArgb(213, 213, 213);
            textRequest.Font = new Font("Segoe UI", 11F);
            textRequest.Location = new Point(2, 212);
            textRequest.Multiline = true;
            textRequest.Name = "textRequest";
            textRequest.ScrollBars = ScrollBars.Vertical;
            textRequest.Size = new Size(719, 48);
            textRequest.TabIndex = 2;
            textRequest.Text = "Try the 'tool_buffer_demo' tool.";
            textRequest.KeyDown += textRequest_KeyDown;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { toolStripMenuItem1 });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(721, 24);
            menuStrip1.TabIndex = 3;
            menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { toolStripSeparator1, exitToolStripMenuItem });
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(37, 20);
            toolStripMenuItem1.Text = "File";
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(89, 6);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(92, 22);
            exitToolStripMenuItem.Text = "&Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // FormAnthropicDemo
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(219, 219, 219);
            ClientSize = new Size(721, 263);
            Controls.Add(textRequest);
            Controls.Add(rtbLog);
            Controls.Add(menuStrip1);
            Controls.Add(btnSend);
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(547, 150);
            Name = "FormAnthropicDemo";
            Text = "AnthropicToolUseBuffer";
            FormClosing += FormAnthropicDemo_FormClosing;
            Load += FormAnthropicDemo_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RichTextBox rtbLog;
        private Button btnSend;
        private TextBox textRequest;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem exitToolStripMenuItem;
    }
}
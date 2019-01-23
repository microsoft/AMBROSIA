namespace GraphicalApp
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
            this.receivePortTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.sendPortTextBox = new System.Windows.Forms.TextBox();
            this.thisInstanceTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.remoteInstanceTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.startButton = new System.Windows.Forms.Button();
            this.client1PresetsButton = new System.Windows.Forms.Button();
            this.client2PresetsButton = new System.Windows.Forms.Button();
            this.craPortTextBox = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // receivePortTextBox
            // 
            this.receivePortTextBox.Location = new System.Drawing.Point(310, 147);
            this.receivePortTextBox.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.receivePortTextBox.Name = "receivePortTextBox";
            this.receivePortTextBox.Size = new System.Drawing.Size(228, 35);
            this.receivePortTextBox.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(135, 163);
            this.label1.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(151, 29);
            this.label1.TabIndex = 1;
            this.label1.Text = "Receive Port";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(166, 212);
            this.label2.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(120, 29);
            this.label2.TabIndex = 2;
            this.label2.Text = "Send Port";
            // 
            // sendPortTextBox
            // 
            this.sendPortTextBox.Location = new System.Drawing.Point(310, 205);
            this.sendPortTextBox.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.sendPortTextBox.Name = "sendPortTextBox";
            this.sendPortTextBox.Size = new System.Drawing.Size(228, 35);
            this.sendPortTextBox.TabIndex = 3;
            // 
            // thisInstanceTextBox
            // 
            this.thisInstanceTextBox.Location = new System.Drawing.Point(310, 263);
            this.thisInstanceTextBox.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.thisInstanceTextBox.Name = "thisInstanceTextBox";
            this.thisInstanceTextBox.Size = new System.Drawing.Size(228, 35);
            this.thisInstanceTextBox.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(60, 269);
            this.label3.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(226, 29);
            this.label3.TabIndex = 5;
            this.label3.Text = "This Instance Name";
            // 
            // remoteInstanceTextBox
            // 
            this.remoteInstanceTextBox.Location = new System.Drawing.Point(310, 321);
            this.remoteInstanceTextBox.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.remoteInstanceTextBox.Name = "remoteInstanceTextBox";
            this.remoteInstanceTextBox.Size = new System.Drawing.Size(228, 35);
            this.remoteInstanceTextBox.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(22, 327);
            this.label4.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(264, 29);
            this.label4.TabIndex = 7;
            this.label4.Text = "Remote Instance Name";
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(310, 520);
            this.startButton.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(175, 51);
            this.startButton.TabIndex = 8;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // client1PresetsButton
            // 
            this.client1PresetsButton.Location = new System.Drawing.Point(658, 143);
            this.client1PresetsButton.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.client1PresetsButton.Name = "client1PresetsButton";
            this.client1PresetsButton.Size = new System.Drawing.Size(287, 51);
            this.client1PresetsButton.TabIndex = 9;
            this.client1PresetsButton.Text = "Load client 1 presets";
            this.client1PresetsButton.UseVisualStyleBackColor = true;
            this.client1PresetsButton.Click += new System.EventHandler(this.client1PresetsButton_Click);
            // 
            // client2PresetsButton
            // 
            this.client2PresetsButton.Location = new System.Drawing.Point(658, 212);
            this.client2PresetsButton.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.client2PresetsButton.Name = "client2PresetsButton";
            this.client2PresetsButton.Size = new System.Drawing.Size(287, 51);
            this.client2PresetsButton.TabIndex = 10;
            this.client2PresetsButton.Text = "Load client 2 presets";
            this.client2PresetsButton.UseVisualStyleBackColor = true;
            this.client2PresetsButton.Click += new System.EventHandler(this.client2PresetsButton_Click);
            // 
            // craPortTextBox
            // 
            this.craPortTextBox.Location = new System.Drawing.Point(310, 422);
            this.craPortTextBox.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.craPortTextBox.Name = "craPortTextBox";
            this.craPortTextBox.Size = new System.Drawing.Size(228, 35);
            this.craPortTextBox.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(56, 428);
            this.label6.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(196, 29);
            this.label6.TabIndex = 14;
            this.label6.Text = "CRA Worker Port";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(14F, 29F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1867, 1004);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.craPortTextBox);
            this.Controls.Add(this.client2PresetsButton);
            this.Controls.Add(this.client1PresetsButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.remoteInstanceTextBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.thisInstanceTextBox);
            this.Controls.Add(this.sendPortTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.receivePortTextBox);
            this.DoubleBuffered = true;
            this.Margin = new System.Windows.Forms.Padding(7, 7, 7, 7);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Form1_Paint);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox receivePortTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox sendPortTextBox;
        private System.Windows.Forms.TextBox thisInstanceTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox remoteInstanceTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button client1PresetsButton;
        private System.Windows.Forms.Button client2PresetsButton;
        private System.Windows.Forms.TextBox craPortTextBox;
        private System.Windows.Forms.Label label6;
    }
}


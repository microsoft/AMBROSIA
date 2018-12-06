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
            this.thisServiceTextBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.remoteServiceTextBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.startButton = new System.Windows.Forms.Button();
            this.client1PresetsButton = new System.Windows.Forms.Button();
            this.client2PresetsButton = new System.Windows.Forms.Button();
            this.craPortTextBox = new System.Windows.Forms.TextBox();
            this.thisInstanceTextBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // receivePortTextBox
            // 
            this.receivePortTextBox.Location = new System.Drawing.Point(133, 66);
            this.receivePortTextBox.Name = "receivePortTextBox";
            this.receivePortTextBox.Size = new System.Drawing.Size(100, 20);
            this.receivePortTextBox.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(58, 73);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Receive Port";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(73, 95);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Send Port";
            // 
            // sendPortTextBox
            // 
            this.sendPortTextBox.Location = new System.Drawing.Point(133, 92);
            this.sendPortTextBox.Name = "sendPortTextBox";
            this.sendPortTextBox.Size = new System.Drawing.Size(100, 20);
            this.sendPortTextBox.TabIndex = 3;
            // 
            // thisServiceTextBox
            // 
            this.thisServiceTextBox.Location = new System.Drawing.Point(133, 118);
            this.thisServiceTextBox.Name = "thisServiceTextBox";
            this.thisServiceTextBox.Size = new System.Drawing.Size(100, 20);
            this.thisServiceTextBox.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(30, 121);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(97, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "This Service Name";
            // 
            // remoteServiceTextBox
            // 
            this.remoteServiceTextBox.Location = new System.Drawing.Point(133, 144);
            this.remoteServiceTextBox.Name = "remoteServiceTextBox";
            this.remoteServiceTextBox.Size = new System.Drawing.Size(100, 20);
            this.remoteServiceTextBox.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 147);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(114, 13);
            this.label4.TabIndex = 7;
            this.label4.Text = "Remote Service Name";
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(133, 254);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75, 23);
            this.startButton.TabIndex = 8;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // client1PresetsButton
            // 
            this.client1PresetsButton.Location = new System.Drawing.Point(282, 64);
            this.client1PresetsButton.Name = "client1PresetsButton";
            this.client1PresetsButton.Size = new System.Drawing.Size(123, 23);
            this.client1PresetsButton.TabIndex = 9;
            this.client1PresetsButton.Text = "Load client 1 presets";
            this.client1PresetsButton.UseVisualStyleBackColor = true;
            this.client1PresetsButton.Click += new System.EventHandler(this.client1PresetsButton_Click);
            // 
            // client2PresetsButton
            // 
            this.client2PresetsButton.Location = new System.Drawing.Point(282, 95);
            this.client2PresetsButton.Name = "client2PresetsButton";
            this.client2PresetsButton.Size = new System.Drawing.Size(123, 23);
            this.client2PresetsButton.TabIndex = 10;
            this.client2PresetsButton.Text = "Load client 2 presets";
            this.client2PresetsButton.UseVisualStyleBackColor = true;
            this.client2PresetsButton.Click += new System.EventHandler(this.client2PresetsButton_Click);
            // 
            // craPortTextBox
            // 
            this.craPortTextBox.Location = new System.Drawing.Point(133, 189);
            this.craPortTextBox.Name = "craPortTextBox";
            this.craPortTextBox.Size = new System.Drawing.Size(100, 20);
            this.craPortTextBox.TabIndex = 11;
            // 
            // instanceTextBox
            // 
            this.thisInstanceTextBox.Location = new System.Drawing.Point(133, 215);
            this.thisInstanceTextBox.Name = "instanceTextBox";
            this.thisInstanceTextBox.Size = new System.Drawing.Size(100, 20);
            this.thisInstanceTextBox.TabIndex = 12;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(24, 218);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(102, 13);
            this.label5.TabIndex = 13;
            this.label5.Text = "This Instance Name";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(24, 192);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(89, 13);
            this.label6.TabIndex = 14;
            this.label6.Text = "CRA Worker Port";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.thisInstanceTextBox);
            this.Controls.Add(this.craPortTextBox);
            this.Controls.Add(this.client2PresetsButton);
            this.Controls.Add(this.client1PresetsButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.remoteServiceTextBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.thisServiceTextBox);
            this.Controls.Add(this.sendPortTextBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.receivePortTextBox);
            this.DoubleBuffered = true;
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
        private System.Windows.Forms.TextBox thisServiceTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox remoteServiceTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button client1PresetsButton;
        private System.Windows.Forms.Button client2PresetsButton;
        private System.Windows.Forms.TextBox craPortTextBox;
        private System.Windows.Forms.TextBox thisInstanceTextBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
    }
}


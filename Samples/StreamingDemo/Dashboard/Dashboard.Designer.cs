namespace DashboardFancy
{
    partial class Dashboard
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
            this.output_textbox = new System.Windows.Forms.RichTextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // output_textbox
            // 
            this.output_textbox.Location = new System.Drawing.Point(34, 45);
            this.output_textbox.MaxLength = 1000000;
            this.output_textbox.Name = "output_textbox";
            this.output_textbox.ReadOnly = true;
            this.output_textbox.Size = new System.Drawing.Size(512, 334);
            this.output_textbox.TabIndex = 1;
            this.output_textbox.Text = "";
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button2.Location = new System.Drawing.Point(261, 481);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 50);
            this.button2.TabIndex = 3;
            this.button2.Text = "Quit";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // DashboardFancy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 576);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.output_textbox);
            this.Name = "DashboardFancy";
            this.Text = "Dashboard";
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.RichTextBox output_textbox;
        private System.Windows.Forms.Button button2;
    }
}


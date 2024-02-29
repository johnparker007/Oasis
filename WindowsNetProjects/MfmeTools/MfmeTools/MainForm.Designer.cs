
namespace MfmeTools
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.buttonStartExtraction = new System.Windows.Forms.Button();
            this.buttonStartInjection = new System.Windows.Forms.Button();
            this.groupBoxExtraction = new System.Windows.Forms.GroupBox();
            this.label1gygygy = new System.Windows.Forms.Label();
            this.textBoxMfmeSourceLayoutPath = new System.Windows.Forms.TextBox();
            this.checkBoxScrapeLamps9_12 = new System.Windows.Forms.CheckBox();
            this.checkBoxScrapeLamps5_8 = new System.Windows.Forms.CheckBox();
            this.checkBoxUseCachedReelImages = new System.Windows.Forms.CheckBox();
            this.checkBoxUseCachedLampImages = new System.Windows.Forms.CheckBox();
            this.groupBoxInjection = new System.Windows.Forms.GroupBox();
            this.checkBoxEnvironmentReflections = new System.Windows.Forms.CheckBox();
            this.checkBoxMachineReflections = new System.Windows.Forms.CheckBox();
            this.checkBoxBloom = new System.Windows.Forms.CheckBox();
            this.checkBoxEnvironmentLampBleed = new System.Windows.Forms.CheckBox();
            this.checkBoxEnvironmentBackground = new System.Windows.Forms.CheckBox();
            this.labelOasisSourcelLayoutPath = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxMfmeTargetLayoutPath = new System.Windows.Forms.TextBox();
            this.groupBoxExtraction.SuspendLayout();
            this.groupBoxInjection.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonStartExtraction
            // 
            this.buttonStartExtraction.Location = new System.Drawing.Point(6, 19);
            this.buttonStartExtraction.Name = "buttonStartExtraction";
            this.buttonStartExtraction.Size = new System.Drawing.Size(165, 29);
            this.buttonStartExtraction.TabIndex = 0;
            this.buttonStartExtraction.Text = "Start Extraction";
            this.buttonStartExtraction.UseVisualStyleBackColor = true;
            this.buttonStartExtraction.Click += new System.EventHandler(this.OnButtonStartExtractionClick);
            // 
            // buttonStartInjection
            // 
            this.buttonStartInjection.Location = new System.Drawing.Point(6, 19);
            this.buttonStartInjection.Name = "buttonStartInjection";
            this.buttonStartInjection.Size = new System.Drawing.Size(165, 29);
            this.buttonStartInjection.TabIndex = 1;
            this.buttonStartInjection.Text = "Start Injection";
            this.buttonStartInjection.UseVisualStyleBackColor = true;
            // 
            // groupBoxExtraction
            // 
            this.groupBoxExtraction.Controls.Add(this.label1gygygy);
            this.groupBoxExtraction.Controls.Add(this.textBoxMfmeSourceLayoutPath);
            this.groupBoxExtraction.Controls.Add(this.checkBoxScrapeLamps9_12);
            this.groupBoxExtraction.Controls.Add(this.checkBoxScrapeLamps5_8);
            this.groupBoxExtraction.Controls.Add(this.checkBoxUseCachedReelImages);
            this.groupBoxExtraction.Controls.Add(this.checkBoxUseCachedLampImages);
            this.groupBoxExtraction.Controls.Add(this.buttonStartExtraction);
            this.groupBoxExtraction.Location = new System.Drawing.Point(12, 12);
            this.groupBoxExtraction.Name = "groupBoxExtraction";
            this.groupBoxExtraction.Size = new System.Drawing.Size(632, 182);
            this.groupBoxExtraction.TabIndex = 2;
            this.groupBoxExtraction.TabStop = false;
            this.groupBoxExtraction.Text = "Extraction";
            // 
            // label1gygygy
            // 
            this.label1gygygy.AutoSize = true;
            this.label1gygygy.Location = new System.Drawing.Point(9, 61);
            this.label1gygygy.Name = "label1gygygy";
            this.label1gygygy.Size = new System.Drawing.Size(128, 13);
            this.label1gygygy.TabIndex = 6;
            this.label1gygygy.Text = "MFME source layout path";
            // 
            // textBoxMfmeSourceLayoutPath
            // 
            this.textBoxMfmeSourceLayoutPath.Location = new System.Drawing.Point(143, 54);
            this.textBoxMfmeSourceLayoutPath.Name = "textBoxMfmeSourceLayoutPath";
            this.textBoxMfmeSourceLayoutPath.Size = new System.Drawing.Size(477, 20);
            this.textBoxMfmeSourceLayoutPath.TabIndex = 5;
            // 
            // checkBoxScrapeLamps9_12
            // 
            this.checkBoxScrapeLamps9_12.AutoSize = true;
            this.checkBoxScrapeLamps9_12.Location = new System.Drawing.Point(12, 149);
            this.checkBoxScrapeLamps9_12.Name = "checkBoxScrapeLamps9_12";
            this.checkBoxScrapeLamps9_12.Size = new System.Drawing.Size(114, 17);
            this.checkBoxScrapeLamps9_12.TabIndex = 4;
            this.checkBoxScrapeLamps9_12.Text = "Scrape lamps 9-12";
            this.checkBoxScrapeLamps9_12.UseVisualStyleBackColor = true;
            // 
            // checkBoxScrapeLamps5_8
            // 
            this.checkBoxScrapeLamps5_8.AutoSize = true;
            this.checkBoxScrapeLamps5_8.Location = new System.Drawing.Point(12, 126);
            this.checkBoxScrapeLamps5_8.Name = "checkBoxScrapeLamps5_8";
            this.checkBoxScrapeLamps5_8.Size = new System.Drawing.Size(108, 17);
            this.checkBoxScrapeLamps5_8.TabIndex = 3;
            this.checkBoxScrapeLamps5_8.Text = "Scrape lamps 5-8";
            this.checkBoxScrapeLamps5_8.UseVisualStyleBackColor = true;
            // 
            // checkBoxUseCachedReelImages
            // 
            this.checkBoxUseCachedReelImages.AutoSize = true;
            this.checkBoxUseCachedReelImages.Location = new System.Drawing.Point(12, 103);
            this.checkBoxUseCachedReelImages.Name = "checkBoxUseCachedReelImages";
            this.checkBoxUseCachedReelImages.Size = new System.Drawing.Size(140, 17);
            this.checkBoxUseCachedReelImages.TabIndex = 2;
            this.checkBoxUseCachedReelImages.Text = "Use cached reel images";
            this.checkBoxUseCachedReelImages.UseVisualStyleBackColor = true;
            // 
            // checkBoxUseCachedLampImages
            // 
            this.checkBoxUseCachedLampImages.AutoSize = true;
            this.checkBoxUseCachedLampImages.Location = new System.Drawing.Point(12, 80);
            this.checkBoxUseCachedLampImages.Name = "checkBoxUseCachedLampImages";
            this.checkBoxUseCachedLampImages.Size = new System.Drawing.Size(145, 17);
            this.checkBoxUseCachedLampImages.TabIndex = 1;
            this.checkBoxUseCachedLampImages.Text = "Use cached lamp images";
            this.checkBoxUseCachedLampImages.UseVisualStyleBackColor = true;
            // 
            // groupBoxInjection
            // 
            this.groupBoxInjection.Controls.Add(this.checkBoxEnvironmentReflections);
            this.groupBoxInjection.Controls.Add(this.checkBoxMachineReflections);
            this.groupBoxInjection.Controls.Add(this.checkBoxBloom);
            this.groupBoxInjection.Controls.Add(this.checkBoxEnvironmentLampBleed);
            this.groupBoxInjection.Controls.Add(this.checkBoxEnvironmentBackground);
            this.groupBoxInjection.Controls.Add(this.labelOasisSourcelLayoutPath);
            this.groupBoxInjection.Controls.Add(this.textBox1);
            this.groupBoxInjection.Controls.Add(this.label1);
            this.groupBoxInjection.Controls.Add(this.buttonStartInjection);
            this.groupBoxInjection.Controls.Add(this.textBoxMfmeTargetLayoutPath);
            this.groupBoxInjection.Location = new System.Drawing.Point(12, 200);
            this.groupBoxInjection.Name = "groupBoxInjection";
            this.groupBoxInjection.Size = new System.Drawing.Size(632, 232);
            this.groupBoxInjection.TabIndex = 3;
            this.groupBoxInjection.TabStop = false;
            this.groupBoxInjection.Text = "Injection";
            // 
            // checkBoxEnvironmentReflections
            // 
            this.checkBoxEnvironmentReflections.AutoSize = true;
            this.checkBoxEnvironmentReflections.Location = new System.Drawing.Point(12, 199);
            this.checkBoxEnvironmentReflections.Name = "checkBoxEnvironmentReflections";
            this.checkBoxEnvironmentReflections.Size = new System.Drawing.Size(136, 17);
            this.checkBoxEnvironmentReflections.TabIndex = 15;
            this.checkBoxEnvironmentReflections.Text = "Environment reflections";
            this.checkBoxEnvironmentReflections.UseVisualStyleBackColor = true;
            // 
            // checkBoxMachineReflections
            // 
            this.checkBoxMachineReflections.AutoSize = true;
            this.checkBoxMachineReflections.Location = new System.Drawing.Point(12, 176);
            this.checkBoxMachineReflections.Name = "checkBoxMachineReflections";
            this.checkBoxMachineReflections.Size = new System.Drawing.Size(118, 17);
            this.checkBoxMachineReflections.TabIndex = 14;
            this.checkBoxMachineReflections.Text = "Machine reflections";
            this.checkBoxMachineReflections.UseVisualStyleBackColor = true;
            // 
            // checkBoxBloom
            // 
            this.checkBoxBloom.AutoSize = true;
            this.checkBoxBloom.Location = new System.Drawing.Point(13, 153);
            this.checkBoxBloom.Name = "checkBoxBloom";
            this.checkBoxBloom.Size = new System.Drawing.Size(55, 17);
            this.checkBoxBloom.TabIndex = 13;
            this.checkBoxBloom.Text = "Bloom";
            this.checkBoxBloom.UseVisualStyleBackColor = true;
            // 
            // checkBoxEnvironmentLampBleed
            // 
            this.checkBoxEnvironmentLampBleed.AutoSize = true;
            this.checkBoxEnvironmentLampBleed.Location = new System.Drawing.Point(12, 130);
            this.checkBoxEnvironmentLampBleed.Name = "checkBoxEnvironmentLampBleed";
            this.checkBoxEnvironmentLampBleed.Size = new System.Drawing.Size(81, 17);
            this.checkBoxEnvironmentLampBleed.TabIndex = 12;
            this.checkBoxEnvironmentLampBleed.Text = "Lamp bleed";
            this.checkBoxEnvironmentLampBleed.UseVisualStyleBackColor = true;
            // 
            // checkBoxEnvironmentBackground
            // 
            this.checkBoxEnvironmentBackground.AutoSize = true;
            this.checkBoxEnvironmentBackground.Location = new System.Drawing.Point(12, 107);
            this.checkBoxEnvironmentBackground.Name = "checkBoxEnvironmentBackground";
            this.checkBoxEnvironmentBackground.Size = new System.Drawing.Size(145, 17);
            this.checkBoxEnvironmentBackground.TabIndex = 11;
            this.checkBoxEnvironmentBackground.Text = "Environment background";
            this.checkBoxEnvironmentBackground.UseVisualStyleBackColor = true;
            // 
            // labelOasisSourcelLayoutPath
            // 
            this.labelOasisSourcelLayoutPath.AutoSize = true;
            this.labelOasisSourcelLayoutPath.Location = new System.Drawing.Point(9, 62);
            this.labelOasisSourcelLayoutPath.Name = "labelOasisSourcelLayoutPath";
            this.labelOasisSourcelLayoutPath.Size = new System.Drawing.Size(123, 13);
            this.labelOasisSourcelLayoutPath.TabIndex = 10;
            this.labelOasisSourcelLayoutPath.Text = "Oasis source layout path";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(143, 55);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(477, 20);
            this.textBox1.TabIndex = 9;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 88);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "MFME target layout path";
            // 
            // textBoxMfmeTargetLayoutPath
            // 
            this.textBoxMfmeTargetLayoutPath.Location = new System.Drawing.Point(143, 81);
            this.textBoxMfmeTargetLayoutPath.Name = "textBoxMfmeTargetLayoutPath";
            this.textBoxMfmeTargetLayoutPath.Size = new System.Drawing.Size(477, 20);
            this.textBoxMfmeTargetLayoutPath.TabIndex = 7;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(656, 450);
            this.Controls.Add(this.groupBoxInjection);
            this.Controls.Add(this.groupBoxExtraction);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "Oasis - MFME Tools";
            this.groupBoxExtraction.ResumeLayout(false);
            this.groupBoxExtraction.PerformLayout();
            this.groupBoxInjection.ResumeLayout(false);
            this.groupBoxInjection.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonStartExtraction;
        private System.Windows.Forms.Button buttonStartInjection;
        private System.Windows.Forms.GroupBox groupBoxExtraction;
        private System.Windows.Forms.GroupBox groupBoxInjection;
        private System.Windows.Forms.CheckBox checkBoxScrapeLamps9_12;
        private System.Windows.Forms.CheckBox checkBoxScrapeLamps5_8;
        private System.Windows.Forms.CheckBox checkBoxUseCachedReelImages;
        private System.Windows.Forms.CheckBox checkBoxUseCachedLampImages;
        private System.Windows.Forms.Label label1gygygy;
        private System.Windows.Forms.TextBox textBoxMfmeSourceLayoutPath;
        private System.Windows.Forms.Label labelOasisSourcelLayoutPath;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxMfmeTargetLayoutPath;
        private System.Windows.Forms.CheckBox checkBoxBloom;
        private System.Windows.Forms.CheckBox checkBoxEnvironmentLampBleed;
        private System.Windows.Forms.CheckBox checkBoxEnvironmentBackground;
        private System.Windows.Forms.CheckBox checkBoxEnvironmentReflections;
        private System.Windows.Forms.CheckBox checkBoxMachineReflections;
    }
}


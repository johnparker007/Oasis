
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
            this.buttonExtractSourcePath = new System.Windows.Forms.Button();
            this.labelExtractSourcePath = new System.Windows.Forms.Label();
            this.textBoxExtractSourcePath = new System.Windows.Forms.TextBox();
            this.checkBoxScrapeLamps9_12 = new System.Windows.Forms.CheckBox();
            this.checkBoxScrapeLamps5_8 = new System.Windows.Forms.CheckBox();
            this.checkBoxUseCachedReelImages = new System.Windows.Forms.CheckBox();
            this.checkBoxUseCachedLampImages = new System.Windows.Forms.CheckBox();
            this.groupBoxInjection = new System.Windows.Forms.GroupBox();
            this.buttonInjectTargetPath = new System.Windows.Forms.Button();
            this.buttonInjectSourcePath = new System.Windows.Forms.Button();
            this.checkBoxEnvironmentReflections = new System.Windows.Forms.CheckBox();
            this.checkBoxMachineReflections = new System.Windows.Forms.CheckBox();
            this.checkBoxBloom = new System.Windows.Forms.CheckBox();
            this.checkBoxEnvironmentLampBleed = new System.Windows.Forms.CheckBox();
            this.checkBoxEnvironmentBackground = new System.Windows.Forms.CheckBox();
            this.labelInjectSourcePath = new System.Windows.Forms.Label();
            this.textBoxInjectSourcePath = new System.Windows.Forms.TextBox();
            this.labelInjectTargetPath = new System.Windows.Forms.Label();
            this.textBoxInjectTargetPath = new System.Windows.Forms.TextBox();
            this.richTextBoxOutputLog = new System.Windows.Forms.RichTextBox();
            this.groupBoxOutputLog = new System.Windows.Forms.GroupBox();
            this.checkBoxAnimatedButtons = new System.Windows.Forms.CheckBox();
            this.groupBoxExtraction.SuspendLayout();
            this.groupBoxInjection.SuspendLayout();
            this.groupBoxOutputLog.SuspendLayout();
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
            this.groupBoxExtraction.Controls.Add(this.buttonExtractSourcePath);
            this.groupBoxExtraction.Controls.Add(this.labelExtractSourcePath);
            this.groupBoxExtraction.Controls.Add(this.textBoxExtractSourcePath);
            this.groupBoxExtraction.Controls.Add(this.checkBoxScrapeLamps9_12);
            this.groupBoxExtraction.Controls.Add(this.checkBoxScrapeLamps5_8);
            this.groupBoxExtraction.Controls.Add(this.checkBoxUseCachedReelImages);
            this.groupBoxExtraction.Controls.Add(this.checkBoxUseCachedLampImages);
            this.groupBoxExtraction.Controls.Add(this.buttonStartExtraction);
            this.groupBoxExtraction.Location = new System.Drawing.Point(12, 12);
            this.groupBoxExtraction.Name = "groupBoxExtraction";
            this.groupBoxExtraction.Size = new System.Drawing.Size(716, 182);
            this.groupBoxExtraction.TabIndex = 2;
            this.groupBoxExtraction.TabStop = false;
            this.groupBoxExtraction.Text = "Extraction";
            // 
            // buttonExtractSourcePath
            // 
            this.buttonExtractSourcePath.Location = new System.Drawing.Point(627, 52);
            this.buttonExtractSourcePath.Name = "buttonExtractSourcePath";
            this.buttonExtractSourcePath.Size = new System.Drawing.Size(75, 23);
            this.buttonExtractSourcePath.TabIndex = 7;
            this.buttonExtractSourcePath.Text = "Browse...";
            this.buttonExtractSourcePath.UseVisualStyleBackColor = true;
            this.buttonExtractSourcePath.Click += new System.EventHandler(this.OnButtonExtractSourcePathClick);
            // 
            // labelExtractSourcePath
            // 
            this.labelExtractSourcePath.AutoSize = true;
            this.labelExtractSourcePath.Location = new System.Drawing.Point(9, 61);
            this.labelExtractSourcePath.Name = "labelExtractSourcePath";
            this.labelExtractSourcePath.Size = new System.Drawing.Size(128, 13);
            this.labelExtractSourcePath.TabIndex = 6;
            this.labelExtractSourcePath.Text = "MFME source layout path";
            // 
            // textBoxExtractSourcePath
            // 
            this.textBoxExtractSourcePath.Location = new System.Drawing.Point(143, 54);
            this.textBoxExtractSourcePath.Name = "textBoxExtractSourcePath";
            this.textBoxExtractSourcePath.Size = new System.Drawing.Size(477, 20);
            this.textBoxExtractSourcePath.TabIndex = 5;
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
            this.groupBoxInjection.Controls.Add(this.checkBoxAnimatedButtons);
            this.groupBoxInjection.Controls.Add(this.buttonInjectTargetPath);
            this.groupBoxInjection.Controls.Add(this.buttonInjectSourcePath);
            this.groupBoxInjection.Controls.Add(this.checkBoxEnvironmentReflections);
            this.groupBoxInjection.Controls.Add(this.checkBoxMachineReflections);
            this.groupBoxInjection.Controls.Add(this.checkBoxBloom);
            this.groupBoxInjection.Controls.Add(this.checkBoxEnvironmentLampBleed);
            this.groupBoxInjection.Controls.Add(this.checkBoxEnvironmentBackground);
            this.groupBoxInjection.Controls.Add(this.labelInjectSourcePath);
            this.groupBoxInjection.Controls.Add(this.textBoxInjectSourcePath);
            this.groupBoxInjection.Controls.Add(this.labelInjectTargetPath);
            this.groupBoxInjection.Controls.Add(this.buttonStartInjection);
            this.groupBoxInjection.Controls.Add(this.textBoxInjectTargetPath);
            this.groupBoxInjection.Location = new System.Drawing.Point(12, 200);
            this.groupBoxInjection.Name = "groupBoxInjection";
            this.groupBoxInjection.Size = new System.Drawing.Size(716, 232);
            this.groupBoxInjection.TabIndex = 3;
            this.groupBoxInjection.TabStop = false;
            this.groupBoxInjection.Text = "Injection";
            // 
            // buttonInjectTargetPath
            // 
            this.buttonInjectTargetPath.Location = new System.Drawing.Point(627, 79);
            this.buttonInjectTargetPath.Name = "buttonInjectTargetPath";
            this.buttonInjectTargetPath.Size = new System.Drawing.Size(75, 23);
            this.buttonInjectTargetPath.TabIndex = 16;
            this.buttonInjectTargetPath.Text = "Browse...";
            this.buttonInjectTargetPath.UseVisualStyleBackColor = true;
            // 
            // buttonInjectSourcePath
            // 
            this.buttonInjectSourcePath.Location = new System.Drawing.Point(627, 53);
            this.buttonInjectSourcePath.Name = "buttonInjectSourcePath";
            this.buttonInjectSourcePath.Size = new System.Drawing.Size(75, 23);
            this.buttonInjectSourcePath.TabIndex = 8;
            this.buttonInjectSourcePath.Text = "Browse...";
            this.buttonInjectSourcePath.UseVisualStyleBackColor = true;
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
            this.checkBoxBloom.Location = new System.Drawing.Point(12, 153);
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
            // labelInjectSourcePath
            // 
            this.labelInjectSourcePath.AutoSize = true;
            this.labelInjectSourcePath.Location = new System.Drawing.Point(9, 62);
            this.labelInjectSourcePath.Name = "labelInjectSourcePath";
            this.labelInjectSourcePath.Size = new System.Drawing.Size(123, 13);
            this.labelInjectSourcePath.TabIndex = 10;
            this.labelInjectSourcePath.Text = "Oasis source layout path";
            // 
            // textBoxInjectSourcePath
            // 
            this.textBoxInjectSourcePath.Location = new System.Drawing.Point(143, 55);
            this.textBoxInjectSourcePath.Name = "textBoxInjectSourcePath";
            this.textBoxInjectSourcePath.Size = new System.Drawing.Size(477, 20);
            this.textBoxInjectSourcePath.TabIndex = 9;
            // 
            // labelInjectTargetPath
            // 
            this.labelInjectTargetPath.AutoSize = true;
            this.labelInjectTargetPath.Location = new System.Drawing.Point(9, 88);
            this.labelInjectTargetPath.Name = "labelInjectTargetPath";
            this.labelInjectTargetPath.Size = new System.Drawing.Size(123, 13);
            this.labelInjectTargetPath.TabIndex = 8;
            this.labelInjectTargetPath.Text = "MFME target layout path";
            // 
            // textBoxInjectTargetPath
            // 
            this.textBoxInjectTargetPath.Location = new System.Drawing.Point(143, 81);
            this.textBoxInjectTargetPath.Name = "textBoxInjectTargetPath";
            this.textBoxInjectTargetPath.Size = new System.Drawing.Size(477, 20);
            this.textBoxInjectTargetPath.TabIndex = 7;
            // 
            // richTextBoxOutputLog
            // 
            this.richTextBoxOutputLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBoxOutputLog.Location = new System.Drawing.Point(6, 19);
            this.richTextBoxOutputLog.Name = "richTextBoxOutputLog";
            this.richTextBoxOutputLog.ReadOnly = true;
            this.richTextBoxOutputLog.Size = new System.Drawing.Size(703, 134);
            this.richTextBoxOutputLog.TabIndex = 4;
            this.richTextBoxOutputLog.Text = "";
            // 
            // groupBoxOutputLog
            // 
            this.groupBoxOutputLog.Controls.Add(this.richTextBoxOutputLog);
            this.groupBoxOutputLog.Location = new System.Drawing.Point(13, 439);
            this.groupBoxOutputLog.Name = "groupBoxOutputLog";
            this.groupBoxOutputLog.Size = new System.Drawing.Size(715, 159);
            this.groupBoxOutputLog.TabIndex = 5;
            this.groupBoxOutputLog.TabStop = false;
            this.groupBoxOutputLog.Text = "Output Log";
            // 
            // checkBoxAnimatedButtons
            // 
            this.checkBoxAnimatedButtons.AutoSize = true;
            this.checkBoxAnimatedButtons.Location = new System.Drawing.Point(290, 108);
            this.checkBoxAnimatedButtons.Name = "checkBoxAnimatedButtons";
            this.checkBoxAnimatedButtons.Size = new System.Drawing.Size(108, 17);
            this.checkBoxAnimatedButtons.TabIndex = 17;
            this.checkBoxAnimatedButtons.Text = "Animated buttons";
            this.checkBoxAnimatedButtons.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(738, 610);
            this.Controls.Add(this.groupBoxOutputLog);
            this.Controls.Add(this.groupBoxInjection);
            this.Controls.Add(this.groupBoxExtraction);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "Oasis - MFME Tools";
            this.groupBoxExtraction.ResumeLayout(false);
            this.groupBoxExtraction.PerformLayout();
            this.groupBoxInjection.ResumeLayout(false);
            this.groupBoxInjection.PerformLayout();
            this.groupBoxOutputLog.ResumeLayout(false);
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
        private System.Windows.Forms.Label labelExtractSourcePath;
        private System.Windows.Forms.TextBox textBoxExtractSourcePath;
        private System.Windows.Forms.Label labelInjectSourcePath;
        private System.Windows.Forms.TextBox textBoxInjectSourcePath;
        private System.Windows.Forms.Label labelInjectTargetPath;
        private System.Windows.Forms.TextBox textBoxInjectTargetPath;
        private System.Windows.Forms.CheckBox checkBoxBloom;
        private System.Windows.Forms.CheckBox checkBoxEnvironmentLampBleed;
        private System.Windows.Forms.CheckBox checkBoxEnvironmentBackground;
        private System.Windows.Forms.CheckBox checkBoxEnvironmentReflections;
        private System.Windows.Forms.CheckBox checkBoxMachineReflections;
        private System.Windows.Forms.Button buttonExtractSourcePath;
        private System.Windows.Forms.Button buttonInjectTargetPath;
        private System.Windows.Forms.Button buttonInjectSourcePath;
        private System.Windows.Forms.RichTextBox richTextBoxOutputLog;
        private System.Windows.Forms.GroupBox groupBoxOutputLog;
        private System.Windows.Forms.CheckBox checkBoxAnimatedButtons;
    }
}



namespace Oasis.RomTools
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
            this.groupBoxChecksum = new System.Windows.Forms.GroupBox();
            this.buttonCreatPatchedRom = new System.Windows.Forms.Button();
            this.groupBoxBlockOptionsLow = new System.Windows.Forms.GroupBox();
            this.labelMinBlockLength = new System.Windows.Forms.Label();
            this.textBoxMinBlockLengthLow = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.radioButtonBlockFromEndLow = new System.Windows.Forms.RadioButton();
            this.radioButtonBlockFromStartLow = new System.Windows.Forms.RadioButton();
            this.label14 = new System.Windows.Forms.Label();
            this.textboxBlockUseNthFoundLow = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.textboxBlockFillByteLow = new System.Windows.Forms.TextBox();
            this.buttonWorkingRomPath = new System.Windows.Forms.Button();
            this.labelWorkingRomPath = new System.Windows.Forms.Label();
            this.textBoxWorkingRomPath = new System.Windows.Forms.TextBox();
            this.buttonOriginalRomPath = new System.Windows.Forms.Button();
            this.labelOriginalRomPath = new System.Windows.Forms.Label();
            this.textBoxOriginalRomPath = new System.Windows.Forms.TextBox();
            this.groupBoxOutputLog = new System.Windows.Forms.GroupBox();
            this.groupBoxBlockOptionsHigh = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxMinBlockLengthHigh = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.radioButtonBlockFromEndHigh = new System.Windows.Forms.RadioButton();
            this.radioButtonBlockFromStartHigh = new System.Windows.Forms.RadioButton();
            this.label3 = new System.Windows.Forms.Label();
            this.textboxBlockUseNthFoundHigh = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textboxBlockFillByteHigh = new System.Windows.Forms.TextBox();
            this.groupBoxChecksum.SuspendLayout();
            this.groupBoxBlockOptionsLow.SuspendLayout();
            this.groupBoxBlockOptionsHigh.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBoxChecksum
            // 
            this.groupBoxChecksum.Controls.Add(this.groupBoxBlockOptionsHigh);
            this.groupBoxChecksum.Controls.Add(this.buttonCreatPatchedRom);
            this.groupBoxChecksum.Controls.Add(this.groupBoxBlockOptionsLow);
            this.groupBoxChecksum.Controls.Add(this.buttonWorkingRomPath);
            this.groupBoxChecksum.Controls.Add(this.labelWorkingRomPath);
            this.groupBoxChecksum.Controls.Add(this.textBoxWorkingRomPath);
            this.groupBoxChecksum.Controls.Add(this.buttonOriginalRomPath);
            this.groupBoxChecksum.Controls.Add(this.labelOriginalRomPath);
            this.groupBoxChecksum.Controls.Add(this.textBoxOriginalRomPath);
            this.groupBoxChecksum.Location = new System.Drawing.Point(12, 12);
            this.groupBoxChecksum.Name = "groupBoxChecksum";
            this.groupBoxChecksum.Size = new System.Drawing.Size(713, 328);
            this.groupBoxChecksum.TabIndex = 0;
            this.groupBoxChecksum.TabStop = false;
            this.groupBoxChecksum.Text = "Checksum";
            // 
            // buttonCreatPatchedRom
            // 
            this.buttonCreatPatchedRom.Location = new System.Drawing.Point(9, 19);
            this.buttonCreatPatchedRom.Name = "buttonCreatPatchedRom";
            this.buttonCreatPatchedRom.Size = new System.Drawing.Size(165, 29);
            this.buttonCreatPatchedRom.TabIndex = 21;
            this.buttonCreatPatchedRom.Text = "Create Patched ROM";
            this.buttonCreatPatchedRom.UseVisualStyleBackColor = true;
            this.buttonCreatPatchedRom.Click += new System.EventHandler(this.buttonCreatPatchedRom_Click);
            // 
            // groupBoxBlockOptionsLow
            // 
            this.groupBoxBlockOptionsLow.Controls.Add(this.labelMinBlockLength);
            this.groupBoxBlockOptionsLow.Controls.Add(this.textBoxMinBlockLengthLow);
            this.groupBoxBlockOptionsLow.Controls.Add(this.label6);
            this.groupBoxBlockOptionsLow.Controls.Add(this.radioButtonBlockFromEndLow);
            this.groupBoxBlockOptionsLow.Controls.Add(this.radioButtonBlockFromStartLow);
            this.groupBoxBlockOptionsLow.Controls.Add(this.label14);
            this.groupBoxBlockOptionsLow.Controls.Add(this.textboxBlockUseNthFoundLow);
            this.groupBoxBlockOptionsLow.Controls.Add(this.label13);
            this.groupBoxBlockOptionsLow.Controls.Add(this.textboxBlockFillByteLow);
            this.groupBoxBlockOptionsLow.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBoxBlockOptionsLow.Location = new System.Drawing.Point(9, 152);
            this.groupBoxBlockOptionsLow.Name = "groupBoxBlockOptionsLow";
            this.groupBoxBlockOptionsLow.Size = new System.Drawing.Size(283, 167);
            this.groupBoxBlockOptionsLow.TabIndex = 20;
            this.groupBoxBlockOptionsLow.TabStop = false;
            this.groupBoxBlockOptionsLow.Text = "Block Options (Low)";
            // 
            // labelMinBlockLength
            // 
            this.labelMinBlockLength.AutoSize = true;
            this.labelMinBlockLength.Location = new System.Drawing.Point(7, 52);
            this.labelMinBlockLength.Name = "labelMinBlockLength";
            this.labelMinBlockLength.Size = new System.Drawing.Size(93, 13);
            this.labelMinBlockLength.TabIndex = 22;
            this.labelMinBlockLength.Text = "Min. Block Length";
            // 
            // textBoxMinBlockLengthLow
            // 
            this.textBoxMinBlockLengthLow.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textBoxMinBlockLengthLow.Location = new System.Drawing.Point(110, 49);
            this.textBoxMinBlockLengthLow.MaxLength = 2;
            this.textBoxMinBlockLengthLow.Name = "textBoxMinBlockLengthLow";
            this.textBoxMinBlockLengthLow.Size = new System.Drawing.Size(41, 20);
            this.textBoxMinBlockLengthLow.TabIndex = 21;
            this.textBoxMinBlockLengthLow.Text = "16";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(157, 84);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(65, 13);
            this.label6.TabIndex = 9;
            this.label6.Text = "(zero-based)";
            // 
            // radioButtonBlockFromEndLow
            // 
            this.radioButtonBlockFromEndLow.AutoSize = true;
            this.radioButtonBlockFromEndLow.Checked = true;
            this.radioButtonBlockFromEndLow.Location = new System.Drawing.Point(10, 135);
            this.radioButtonBlockFromEndLow.Name = "radioButtonBlockFromEndLow";
            this.radioButtonBlockFromEndLow.Size = new System.Drawing.Size(69, 17);
            this.radioButtonBlockFromEndLow.TabIndex = 8;
            this.radioButtonBlockFromEndLow.TabStop = true;
            this.radioButtonBlockFromEndLow.Text = "From end";
            this.radioButtonBlockFromEndLow.UseVisualStyleBackColor = true;
            // 
            // radioButtonBlockFromStartLow
            // 
            this.radioButtonBlockFromStartLow.AutoSize = true;
            this.radioButtonBlockFromStartLow.Location = new System.Drawing.Point(10, 112);
            this.radioButtonBlockFromStartLow.Name = "radioButtonBlockFromStartLow";
            this.radioButtonBlockFromStartLow.Size = new System.Drawing.Size(71, 17);
            this.radioButtonBlockFromStartLow.TabIndex = 7;
            this.radioButtonBlockFromStartLow.Text = "From start";
            this.radioButtonBlockFromStartLow.UseVisualStyleBackColor = true;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(7, 84);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(79, 13);
            this.label14.TabIndex = 6;
            this.label14.Text = "Use Nth Found";
            // 
            // textboxBlockUseNthFoundLow
            // 
            this.textboxBlockUseNthFoundLow.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textboxBlockUseNthFoundLow.Location = new System.Drawing.Point(110, 81);
            this.textboxBlockUseNthFoundLow.MaxLength = 2;
            this.textboxBlockUseNthFoundLow.Name = "textboxBlockUseNthFoundLow";
            this.textboxBlockUseNthFoundLow.Size = new System.Drawing.Size(41, 20);
            this.textboxBlockUseNthFoundLow.TabIndex = 5;
            this.textboxBlockUseNthFoundLow.Text = "0";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(7, 22);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(42, 13);
            this.label13.TabIndex = 4;
            this.label13.Text = "Fill byte";
            // 
            // textboxBlockFillByteLow
            // 
            this.textboxBlockFillByteLow.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textboxBlockFillByteLow.Location = new System.Drawing.Point(110, 19);
            this.textboxBlockFillByteLow.MaxLength = 2;
            this.textboxBlockFillByteLow.Name = "textboxBlockFillByteLow";
            this.textboxBlockFillByteLow.Size = new System.Drawing.Size(41, 20);
            this.textboxBlockFillByteLow.TabIndex = 3;
            this.textboxBlockFillByteLow.Text = "00";
            // 
            // buttonWorkingRomPath
            // 
            this.buttonWorkingRomPath.Location = new System.Drawing.Point(624, 106);
            this.buttonWorkingRomPath.Name = "buttonWorkingRomPath";
            this.buttonWorkingRomPath.Size = new System.Drawing.Size(75, 23);
            this.buttonWorkingRomPath.TabIndex = 13;
            this.buttonWorkingRomPath.Text = "Browse...";
            this.buttonWorkingRomPath.UseVisualStyleBackColor = true;
            this.buttonWorkingRomPath.Click += new System.EventHandler(this.buttonWorkingRomPath_Click);
            // 
            // labelWorkingRomPath
            // 
            this.labelWorkingRomPath.AutoSize = true;
            this.labelWorkingRomPath.Location = new System.Drawing.Point(6, 115);
            this.labelWorkingRomPath.Name = "labelWorkingRomPath";
            this.labelWorkingRomPath.Size = new System.Drawing.Size(100, 13);
            this.labelWorkingRomPath.TabIndex = 12;
            this.labelWorkingRomPath.Text = "Working ROM Path";
            // 
            // textBoxWorkingRomPath
            // 
            this.textBoxWorkingRomPath.Location = new System.Drawing.Point(140, 108);
            this.textBoxWorkingRomPath.Name = "textBoxWorkingRomPath";
            this.textBoxWorkingRomPath.Size = new System.Drawing.Size(477, 20);
            this.textBoxWorkingRomPath.TabIndex = 11;
            // 
            // buttonOriginalRomPath
            // 
            this.buttonOriginalRomPath.Location = new System.Drawing.Point(624, 77);
            this.buttonOriginalRomPath.Name = "buttonOriginalRomPath";
            this.buttonOriginalRomPath.Size = new System.Drawing.Size(75, 23);
            this.buttonOriginalRomPath.TabIndex = 10;
            this.buttonOriginalRomPath.Text = "Browse...";
            this.buttonOriginalRomPath.UseVisualStyleBackColor = true;
            this.buttonOriginalRomPath.Click += new System.EventHandler(this.buttonOriginalRomPath_Click);
            // 
            // labelOriginalRomPath
            // 
            this.labelOriginalRomPath.AutoSize = true;
            this.labelOriginalRomPath.Location = new System.Drawing.Point(6, 86);
            this.labelOriginalRomPath.Name = "labelOriginalRomPath";
            this.labelOriginalRomPath.Size = new System.Drawing.Size(95, 13);
            this.labelOriginalRomPath.TabIndex = 9;
            this.labelOriginalRomPath.Text = "Original ROM Path";
            // 
            // textBoxOriginalRomPath
            // 
            this.textBoxOriginalRomPath.Location = new System.Drawing.Point(140, 79);
            this.textBoxOriginalRomPath.Name = "textBoxOriginalRomPath";
            this.textBoxOriginalRomPath.Size = new System.Drawing.Size(477, 20);
            this.textBoxOriginalRomPath.TabIndex = 8;
            // 
            // groupBoxOutputLog
            // 
            this.groupBoxOutputLog.Location = new System.Drawing.Point(12, 346);
            this.groupBoxOutputLog.Name = "groupBoxOutputLog";
            this.groupBoxOutputLog.Size = new System.Drawing.Size(713, 152);
            this.groupBoxOutputLog.TabIndex = 1;
            this.groupBoxOutputLog.TabStop = false;
            this.groupBoxOutputLog.Text = "Output Log";
            // 
            // groupBoxBlockOptionsHigh
            // 
            this.groupBoxBlockOptionsHigh.Controls.Add(this.label1);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.textBoxMinBlockLengthHigh);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.label2);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.radioButtonBlockFromEndHigh);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.radioButtonBlockFromStartHigh);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.label3);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.textboxBlockUseNthFoundHigh);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.label4);
            this.groupBoxBlockOptionsHigh.Controls.Add(this.textboxBlockFillByteHigh);
            this.groupBoxBlockOptionsHigh.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBoxBlockOptionsHigh.Location = new System.Drawing.Point(298, 152);
            this.groupBoxBlockOptionsHigh.Name = "groupBoxBlockOptionsHigh";
            this.groupBoxBlockOptionsHigh.Size = new System.Drawing.Size(283, 167);
            this.groupBoxBlockOptionsHigh.TabIndex = 23;
            this.groupBoxBlockOptionsHigh.TabStop = false;
            this.groupBoxBlockOptionsHigh.Text = "Block options (High)";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 52);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(93, 13);
            this.label1.TabIndex = 22;
            this.label1.Text = "Min. Block Length";
            // 
            // textBoxMinBlockLengthHigh
            // 
            this.textBoxMinBlockLengthHigh.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textBoxMinBlockLengthHigh.Location = new System.Drawing.Point(110, 49);
            this.textBoxMinBlockLengthHigh.MaxLength = 2;
            this.textBoxMinBlockLengthHigh.Name = "textBoxMinBlockLengthHigh";
            this.textBoxMinBlockLengthHigh.Size = new System.Drawing.Size(41, 20);
            this.textBoxMinBlockLengthHigh.TabIndex = 21;
            this.textBoxMinBlockLengthHigh.Text = "32";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(157, 84);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(65, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "(zero-based)";
            // 
            // radioButtonBlockFromEndHigh
            // 
            this.radioButtonBlockFromEndHigh.AutoSize = true;
            this.radioButtonBlockFromEndHigh.Checked = true;
            this.radioButtonBlockFromEndHigh.Location = new System.Drawing.Point(10, 135);
            this.radioButtonBlockFromEndHigh.Name = "radioButtonBlockFromEndHigh";
            this.radioButtonBlockFromEndHigh.Size = new System.Drawing.Size(69, 17);
            this.radioButtonBlockFromEndHigh.TabIndex = 8;
            this.radioButtonBlockFromEndHigh.TabStop = true;
            this.radioButtonBlockFromEndHigh.Text = "From end";
            this.radioButtonBlockFromEndHigh.UseVisualStyleBackColor = true;
            // 
            // radioButtonBlockFromStartHigh
            // 
            this.radioButtonBlockFromStartHigh.AutoSize = true;
            this.radioButtonBlockFromStartHigh.Location = new System.Drawing.Point(10, 112);
            this.radioButtonBlockFromStartHigh.Name = "radioButtonBlockFromStartHigh";
            this.radioButtonBlockFromStartHigh.Size = new System.Drawing.Size(71, 17);
            this.radioButtonBlockFromStartHigh.TabIndex = 7;
            this.radioButtonBlockFromStartHigh.Text = "From start";
            this.radioButtonBlockFromStartHigh.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 84);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(79, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Use Nth Found";
            // 
            // textboxBlockUseNthFoundHigh
            // 
            this.textboxBlockUseNthFoundHigh.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textboxBlockUseNthFoundHigh.Location = new System.Drawing.Point(110, 81);
            this.textboxBlockUseNthFoundHigh.MaxLength = 2;
            this.textboxBlockUseNthFoundHigh.Name = "textboxBlockUseNthFoundHigh";
            this.textboxBlockUseNthFoundHigh.Size = new System.Drawing.Size(41, 20);
            this.textboxBlockUseNthFoundHigh.TabIndex = 5;
            this.textboxBlockUseNthFoundHigh.Text = "0";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 22);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(42, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "Fill byte";
            // 
            // textboxBlockFillByteHigh
            // 
            this.textboxBlockFillByteHigh.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textboxBlockFillByteHigh.Location = new System.Drawing.Point(110, 19);
            this.textboxBlockFillByteHigh.MaxLength = 2;
            this.textboxBlockFillByteHigh.Name = "textboxBlockFillByteHigh";
            this.textboxBlockFillByteHigh.Size = new System.Drawing.Size(41, 20);
            this.textboxBlockFillByteHigh.TabIndex = 3;
            this.textboxBlockFillByteHigh.Text = "FF";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(732, 509);
            this.Controls.Add(this.groupBoxOutputLog);
            this.Controls.Add(this.groupBoxChecksum);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "Oasis - ROM Tools";
            this.groupBoxChecksum.ResumeLayout(false);
            this.groupBoxChecksum.PerformLayout();
            this.groupBoxBlockOptionsLow.ResumeLayout(false);
            this.groupBoxBlockOptionsLow.PerformLayout();
            this.groupBoxBlockOptionsHigh.ResumeLayout(false);
            this.groupBoxBlockOptionsHigh.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxChecksum;
        private System.Windows.Forms.GroupBox groupBoxOutputLog;
        private System.Windows.Forms.Button buttonWorkingRomPath;
        private System.Windows.Forms.Label labelWorkingRomPath;
        private System.Windows.Forms.TextBox textBoxWorkingRomPath;
        private System.Windows.Forms.Button buttonOriginalRomPath;
        private System.Windows.Forms.Label labelOriginalRomPath;
        private System.Windows.Forms.TextBox textBoxOriginalRomPath;
        private System.Windows.Forms.GroupBox groupBoxBlockOptionsLow;
        private System.Windows.Forms.Label labelMinBlockLength;
        private System.Windows.Forms.TextBox textBoxMinBlockLengthLow;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.RadioButton radioButtonBlockFromEndLow;
        private System.Windows.Forms.RadioButton radioButtonBlockFromStartLow;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox textboxBlockUseNthFoundLow;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox textboxBlockFillByteLow;
        private System.Windows.Forms.Button buttonCreatPatchedRom;
        private System.Windows.Forms.GroupBox groupBoxBlockOptionsHigh;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxMinBlockLengthHigh;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RadioButton radioButtonBlockFromEndHigh;
        private System.Windows.Forms.RadioButton radioButtonBlockFromStartHigh;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textboxBlockUseNthFoundHigh;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textboxBlockFillByteHigh;
    }
}


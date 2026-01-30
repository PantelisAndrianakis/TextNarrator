using System.Windows.Forms;

namespace TextNarrator
{
    public sealed partial class MainForm
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
            if (disposing)
            {
                // Dispose in reverse order of creation
                // 1. Stop playback controller first (stops any ongoing operations)
                try
                {
                    _playbackController?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing playback controller: {ex.Message}");
                }
                
                // 2. Dispose speech engines
                try
                {
                    _systemSpeechEngine?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing system speech engine: {ex.Message}");
                }
                
                try
                {
                    _winRtSpeechEngine?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing WinRT speech engine: {ex.Message}");
                }
                
                // 3. Dispose standard components last
                if (components != null)
                {
                    components.Dispose();
                }
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
            richTextBox = new RichTextBox();
            panelControls = new Panel();
            btnStop = new Button();
            btnRestart = new Button();
            btnPause = new Button();
            btnPlay = new Button();
            comboVoices = new ComboBox();
            labelVoice = new Label();
            panelControls.SuspendLayout();
            SuspendLayout();
            // 
            // richTextBox
            // 
            richTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            richTextBox.Font = new Font("Georgia", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            richTextBox.Location = new Point(12, 12);
            richTextBox.Name = "richTextBox";
            richTextBox.Size = new Size(776, 365);
            richTextBox.TabIndex = 0;
            richTextBox.Text = "";
            // 
            // panelControls
            // 
            panelControls.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panelControls.Controls.Add(btnStop);
            panelControls.Controls.Add(btnRestart);
            panelControls.Controls.Add(btnPause);
            panelControls.Controls.Add(btnPlay);
            panelControls.Controls.Add(comboVoices);
            panelControls.Controls.Add(labelVoice);
            panelControls.Location = new Point(12, 383);
            panelControls.Name = "panelControls";
            panelControls.Size = new Size(776, 55);
            panelControls.TabIndex = 1;
            // 
            // btnStop
            // 
            btnStop.Font = new Font("Georgia", 12F);
            btnStop.Location = new Point(606, 3);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(100, 49);
            btnStop.TabIndex = 5;
            btnStop.Text = "■ Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnRestart
            // 
            btnRestart.Font = new Font("Georgia", 12F);
            btnRestart.Location = new Point(500, 3);
            btnRestart.Name = "btnRestart";
            btnRestart.Size = new Size(100, 49);
            btnRestart.TabIndex = 4;
            btnRestart.Text = "⟲ Restart";
            btnRestart.UseVisualStyleBackColor = true;
            btnRestart.Click += btnRestart_Click;
            // 
            // btnPause
            // 
            btnPause.Font = new Font("Georgia", 12F);
            btnPause.Location = new Point(394, 3);
            btnPause.Name = "btnPause";
            btnPause.Size = new Size(100, 49);
            btnPause.TabIndex = 3;
            btnPause.Text = "⏸ Pause";
            btnPause.UseVisualStyleBackColor = true;
            btnPause.Click += btnPause_Click;
            // 
            // btnPlay
            // 
            btnPlay.Font = new Font("Georgia", 12F);
            btnPlay.Location = new Point(288, 3);
            btnPlay.Name = "btnPlay";
            btnPlay.Size = new Size(100, 49);
            btnPlay.TabIndex = 2;
            btnPlay.Text = "▶ Play";
            btnPlay.UseVisualStyleBackColor = true;
            btnPlay.Click += btnPlay_Click;
            // 
            // comboVoices
            // 
            comboVoices.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVoices.Font = new Font("Georgia", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            comboVoices.FormattingEnabled = true;
            comboVoices.Location = new Point(49, 14);
            comboVoices.Name = "comboVoices";
            comboVoices.Size = new Size(221, 24);
            comboVoices.TabIndex = 1;
            comboVoices.SelectedIndexChanged += ComboVoices_SelectedIndexChanged;
            // 
            // labelVoice
            // 
            labelVoice.AutoSize = true;
            labelVoice.Font = new Font("Georgia", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            labelVoice.Location = new Point(3, 17);
            labelVoice.Name = "labelVoice";
            labelVoice.Size = new Size(47, 16);
            labelVoice.TabIndex = 0;
            labelVoice.Text = "Voice:";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(panelControls);
            Controls.Add(richTextBox);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Text Narrator";
            panelControls.ResumeLayout(false);
            panelControls.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBox;
        private System.Windows.Forms.Panel panelControls;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnRestart;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.ComboBox comboVoices;
        private System.Windows.Forms.Label labelVoice;
    }
}

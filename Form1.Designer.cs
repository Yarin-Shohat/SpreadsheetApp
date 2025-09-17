namespace SpreadsheetApp
{
    partial class main_form
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(main_form));
            grid = new DataGridView();
            load_button = new Button();
            save_button = new Button();
            defaultImage = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)defaultImage).BeginInit();
            SuspendLayout();
            // 
            // grid
            // 
            grid.BackgroundColor = SystemColors.InactiveBorder;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.GridColor = SystemColors.MenuHighlight;
            grid.Location = new Point(25, 24);
            grid.Name = "grid";
            grid.RowHeadersWidth = 51;
            grid.Size = new Size(736, 332);
            grid.TabIndex = 0;
            grid.CellEndEdit += DataGridView_CellValueChanged;
            // 
            // load_button
            // 
            load_button.Location = new Point(113, 385);
            load_button.Name = "load_button";
            load_button.Size = new Size(222, 29);
            load_button.TabIndex = 1;
            load_button.Text = "Load";
            load_button.UseVisualStyleBackColor = true;
            load_button.Click += load_button_Click;
            // 
            // save_button
            // 
            save_button.Location = new Point(450, 385);
            save_button.Name = "save_button";
            save_button.Size = new Size(222, 29);
            save_button.TabIndex = 2;
            save_button.Text = "Save";
            save_button.UseVisualStyleBackColor = true;
            save_button.Click += save_button_Click;
            // 
            // defaultImage
            // 
            defaultImage.BackgroundImage = (Image)resources.GetObject("defaultImage.BackgroundImage");
            defaultImage.BackgroundImageLayout = ImageLayout.Stretch;
            defaultImage.ErrorImage = null;
            defaultImage.InitialImage = Properties.Resources.premium_photo_1673688152102_b24caa6e8725;
            defaultImage.Location = new Point(26, 24);
            defaultImage.Name = "defaultImage";
            defaultImage.Size = new Size(735, 332);
            defaultImage.TabIndex = 3;
            defaultImage.TabStop = false;
            // 
            // main_form
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.ButtonHighlight;
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            ClientSize = new Size(800, 450);
            Controls.Add(defaultImage);
            Controls.Add(save_button);
            Controls.Add(load_button);
            Controls.Add(grid);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "main_form";
            Text = "BGU-SpreadSheet";
            Load += Form_Load;
            Resize += Form_Resize;
            ((System.ComponentModel.ISupportInitialize)grid).EndInit();
            ((System.ComponentModel.ISupportInitialize)defaultImage).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView grid;
        private Button load_button;
        private Button save_button;
        private PictureBox defaultImage;
    }
}

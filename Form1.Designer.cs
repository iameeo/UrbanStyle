namespace urban_style_auto_regist
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        const string AppName = "UrbanStyle";

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
            ShopList = new ComboBox();
            BtnStart = new Button();
            BtnAll = new Button();
            BtmImgStart = new Button();
            SuspendLayout();
            // 
            // ShopList
            // 
            ShopList.FormattingEnabled = true;
            ShopList.ImeMode = ImeMode.NoControl;
            ShopList.Location = new Point(349, 203);
            ShopList.Name = "ShopList";
            ShopList.Size = new Size(121, 23);
            ShopList.TabIndex = 0;
            // 
            // BtnStart
            // 
            BtnStart.Location = new Point(372, 232);
            BtnStart.Name = "BtnStart";
            BtnStart.Size = new Size(75, 23);
            BtnStart.TabIndex = 1;
            BtnStart.Text = "START";
            BtnStart.UseVisualStyleBackColor = true;
            BtnStart.Click += BtnStart_Click;
            // 
            // BtnAll
            // 
            BtnAll.Location = new Point(372, 261);
            BtnAll.Name = "BtnAll";
            BtnAll.Size = new Size(75, 23);
            BtnAll.TabIndex = 2;
            BtnAll.Text = "ALL START";
            BtnAll.UseVisualStyleBackColor = true;
            BtnAll.Click += BtnAll_Click;
            // 
            // BtmImgStart
            // 
            BtmImgStart.Location = new Point(372, 290);
            BtmImgStart.Name = "BtmImgStart";
            BtmImgStart.Size = new Size(75, 23);
            BtmImgStart.TabIndex = 3;
            BtmImgStart.Text = "ImgStart";
            BtmImgStart.UseVisualStyleBackColor = true;
            BtmImgStart.Click += BtmImgStart_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(BtmImgStart);
            Controls.Add(BtnAll);
            Controls.Add(BtnStart);
            Controls.Add(ShopList);
            Name = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private ComboBox ShopList;
        private Button BtnStart;
        private Button BtnAll;
        private Button BtmImgStart;
    }
}

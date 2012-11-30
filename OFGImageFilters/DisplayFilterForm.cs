using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ObjectFilterGraph.ImageFilters
{
    public partial class DisplayFilterForm : Form
    {
        private Image img = new Bitmap(200, 200);

        public DisplayFilterForm()
        {
            InitializeComponent();
        }

        public void SetPicture(Image img)
        {
            this.img = (Image)img.Clone();
            this.Invalidate();
        }

        private void DisplayFilterForm_Paint(object sender, PaintEventArgs e)
        {
            if (img != null)
            {
                this.Width = img.Width;
                this.Height = img.Height;
                e.Graphics.DrawImage(img, 0, 0);
            }
        }
    }
}

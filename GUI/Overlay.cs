using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Styx.WoWInternals.WoWObjects;

namespace SlimAI.GUI
{
    public partial class Overlay : Form
    {

        #region Form Dragging API Support
        //The SendMessage function sends a message to a window or windows.
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        //ReleaseCapture releases a mouse capture
        [DllImportAttribute("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern bool ReleaseCapture();
        #endregion

        private static LocalPlayer Me { get { return Styx.StyxWoW.Me; } }
        private static Overlay myOverlay;
        private static string labelText;
        private static bool toggleOverlay = true;
        public static bool OverlayShown = false;

        public Overlay()
        {
            InitializeComponent();
        }

        private void Overlay_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xa1, 0x2, 0);
            }
        }

        private void transparentMessagePanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        public static void ShowOverlay()
        {
            if (myOverlay == null || myOverlay.IsDisposed || myOverlay.Disposing)
                myOverlay = new Overlay();

            OverlayShown = true;

            if (SlimAI.ShowOverlay)
            {
                if (toggleOverlay)
                {
                    myOverlay.Show();
                    toggleOverlay = false;
                }
                else
                {
                    myOverlay.Hide();
                    toggleOverlay = true;
                }
            }
        }

        public static void UpdateOverlayText()
        {
            if (myOverlay == null || myOverlay.IsDisposed || myOverlay.Disposing)
            {
                myOverlay = new Overlay();
            }

            if (SlimAI.AOE)
            {
                myOverlay.label5.Text = "Enabled";
                myOverlay.label5.ForeColor = SystemColors.MenuHighlight;
            }
            else
            {
                myOverlay.label5.Text = "Disabled";
                myOverlay.label5.ForeColor = SystemColors.InactiveCaption;
            }

            if (SlimAI.Burst)
            {
                myOverlay.label6.Text = "Enabled";
                myOverlay.label6.ForeColor = SystemColors.MenuHighlight;
            }
            else
            {
                myOverlay.label6.Text = "Disabled";
                myOverlay.label6.ForeColor = SystemColors.InactiveCaption;
            }

            if (SlimAI.PvPRotation)
            {
                myOverlay.label8.Text = "Enabled";
                myOverlay.label8.ForeColor = SystemColors.MenuHighlight;
            }
            else
            {
                myOverlay.label8.Text = "Disabled";
                myOverlay.label8.ForeColor = SystemColors.InactiveCaption;
            }

            //if (Me.Combat && Me.GotTarget && Me.CurrentTarget.Attackable)
            //{
            //    myOverlay.label8.Text = "Melee";
            //    if (Me.CurrentTarget.IsWithinMeleeRange)
            //        myOverlay.label8.ForeColor = System.Drawing.Color.Green;
            //    else
            //        myOverlay.label8.ForeColor = System.Drawing.Color.Red;
            //}

            //else
            //    myOverlay.label8.Text = "";
        }

    }
}

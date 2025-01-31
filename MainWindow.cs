﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace SGB_Settings_Editor
{
    public partial class MainWindow : Form
    {
        private int activePaletteSlot = 0;

        private Color[] ActivePalette = new Color[]
        {
            Color.White, Color.LightGray, Color.DarkGray, Color.Black
        };

        private List<(Bitmap image, Color[] colors)> screenshots = new List<(Bitmap, Color[])>
        {
            /*( Properties.Resources.Tetris, defaultPalette),
            ( Properties.Resources.Mario, defaultPalette),
            ( Properties.Resources.MysticQuest, defaultPalette),
            ( Properties.Resources.MetroidII, defaultPalette),
            ( Properties.Resources.NinjaGaiden, defaultPalette),
            ( Properties.Resources.Mario2, defaultPalette),
            ( Properties.Resources.TripWorld, defaultPalette),
            ( Properties.Resources.Zelda, defaultPalette),
            ( Properties.Resources.WarioLand, defaultPalette)*/
        };

        private (Bitmap image, Color[] colors) fallbackScreenshot = (
            Properties.Resources.fallback,
            new Color[] { Color.FromArgb(245, 245, 245), Color.FromArgb(172, 171, 177), Color.FromArgb(82, 83, 98), Color.FromArgb(12, 12, 12) }
        );

        static internal int sgb_rev = 0;
        private string loaded_rom_file = "";

        private DateTime timer = new DateTime();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Form_Load(object sender, EventArgs e)
        {
            getPalette(0);
            setColorinputs(ActivePalette[0]);
            loadScreenshots();
            refreshPresetData();
        }

        // #####################################################################################
        // Color input change events

        private void rgb24value_Change(object sender, EventArgs e)
        {
            if (!textboxRGB.Focused)
                return;
            try
            {
                string rgb_input = textboxRGB.Text;
                if (rgb_input.Length == 0)
                    return;
                if (rgb_input.Substring(0, 1) == "#")
                {
                    rgb_input = rgb_input.Substring(1);
                }
                if (rgb_input.Length < 6)
                {
                    rgb_input = rgb_input.PadRight(6, '0');
                }
                else if (rgb_input.Length == 7)
                {
                    rgb_input = rgb_input.Substring(0, 6);
                    textboxRGB.Text = rgb_input;
                    textboxRGB.Select(6, 0);
                }
                int rgb24 = int.Parse(rgb_input, System.Globalization.NumberStyles.HexNumber);
                int r = (rgb24 >> 16) % 256;
                int g = (rgb24 >> 8) % 256;
                int b = rgb24 % 256;
                setColorinputs(Color.FromArgb(r, g, b));
            }
            catch
            {
                Color color = panelActiveColor.BackColor;
                textboxRGB.Text = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            }
        }

        private void bgr15value_Change(object sender, EventArgs e)
        {
            if (!textboxBGR15.Focused)
                return;
            try
            {
                string bgr15_input = textboxBGR15.Text;
                if (bgr15_input.Length == 0)
                    return;
                else if (bgr15_input.Length < 4)
                    bgr15_input = bgr15_input.PadRight(4, '0');
                int bgr15 = int.Parse(bgr15_input, System.Globalization.NumberStyles.HexNumber);
                if (bgr15 > 0x7FFF)
                {
                    bgr15 = 0x7FFF;
                    if (textboxBGR15.Text.Length == 4)
                    {
                        textboxBGR15.Text = "7FFF";
                    }
                }
                (int r, int g, int b) = Program.ConvertSFCtoRGB(bgr15);
                setColorinputs(Color.FromArgb(r, g, b), true);
            }
            catch
            {
                Color color = panelActiveColor.BackColor;
                textboxBGR15.Text = Program.ConvertRGBtoSFC(color.R, color.G, color.B).ToString("X4");
            }
        }

        // RGB color inputs

        private void rgbsliderChange(object sender, EventArgs e)
        {
            if (!((TrackBar)sender).Focused)
                return;
            setColorinputs(Color.FromArgb(trackBarRed.Value, trackBarGreen.Value, trackBarBlue.Value));
        }

        private void rgbdecBox_TextChanged(object sender, EventArgs e)
        {
            TextBox activeTextBox = (TextBox)sender;
            if (!activeTextBox.Focused)
                return;
            try
            {
                setColorinputs(Color.FromArgb(int.Parse(textBoxRDec.Text), int.Parse(textBoxGDec.Text), int.Parse(textBoxBDec.Text)));
            }
            catch
            {
                try
                {
                    int value = int.Parse(activeTextBox.Text);
                    if (value < 0)
                        activeTextBox.Text = "0";
                    else
                        activeTextBox.Text = "255";
                }
                catch
                {
                    activeTextBox.Text = "0";
                }
            }
        }

        private void rgbhexBox_TextChanged(object sender, EventArgs e)
        {
            TextBox activeTextBox = (TextBox)sender;
            if (!activeTextBox.Focused)
                return;
            try
            {
                setColorinputs(Color.FromArgb(int.Parse(textBoxRHex.Text, System.Globalization.NumberStyles.HexNumber), int.Parse(textBoxGHex.Text, System.Globalization.NumberStyles.HexNumber), int.Parse(textBoxBHex.Text, System.Globalization.NumberStyles.HexNumber)));
            }
            catch
            {
                activeTextBox.Text = "00";
            }
        }

        // HSV color inputs

        private void trackBarHSV_ValueChanged(object sender, EventArgs e)
        {
            if (!((TrackBar)sender).Focused)
                return;
            Color color = Program.ColorFromHSV((double)trackBarH.Value, (double)trackBarS.Value / 100, (double)trackBarV.Value / 100);
            setColorinputs(color);
        }

        private void textBoxHSV_TextChanged(object sender, EventArgs e)
        {
            TextBox activeTextBox = (TextBox)sender;
            if (!activeTextBox.Focused)
                return;
            try
            {
                Color color = Program.ColorFromHSV(Double.Parse(textBoxH.Text), Double.Parse(textBoxS.Text) / 100, Double.Parse(textBoxV.Text) / 100);
                setColorinputs(color);
            }
            catch
            {
                try
                {
                    int value = Int32.Parse(activeTextBox.Text);
                    if (value < 0)
                        activeTextBox.Text = "0";
                    else if (activeTextBox == textBoxH)
                        activeTextBox.Text = "360";
                    else
                        activeTextBox.Text = "100";
                }
                catch
                {
                    activeTextBox.Text = "0";
                }
            }
        }

        private void panelActiveColor_Click(object sender, EventArgs e)
        {
            colorDialog.Color = panelActiveColor.BackColor;
            DialogResult result = colorDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                setColorinputs(colorDialog.Color);
            }
        }

        // Update all other input fields after a color change
        private void setColorinputs(Color color, bool safe = false, bool all = false)
        {
            if (!textboxRGB.Focused || all)
                textboxRGB.Text = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            if (!textboxBGR15.Focused || all)
                textboxBGR15.Text = Program.ConvertRGBtoSFC(color.R, color.G, color.B).ToString("X4");
            if (!trackBarRed.Focused || all)
                trackBarRed.Value = color.R;
            if (!trackBarGreen.Focused || all)
                trackBarGreen.Value = color.G;
            if (!trackBarBlue.Focused || all)
                trackBarBlue.Value = color.B;
            if (!textBoxRDec.Focused || all)
                textBoxRDec.Text = color.R.ToString();
            if (!textBoxRHex.Focused || all)
                textBoxRHex.Text = color.R.ToString("X2");
            if (!textBoxGDec.Focused || all)
                textBoxGDec.Text = color.G.ToString();
            if (!textBoxGHex.Focused || all)
                textBoxGHex.Text = color.G.ToString("X2");
            if (!textBoxBDec.Focused || all)
                textBoxBDec.Text = color.B.ToString();
            if (!textBoxBHex.Focused || all)
                textBoxBHex.Text = color.B.ToString("X2");

            (double hue, double saturation, double value) = Program.ColorToHSV(color);
            if (!trackBarH.Focused || all)
                trackBarH.Value = (int)Math.Round(hue);
            if (!trackBarS.Focused || all)
                trackBarS.Value = (int)Math.Round(saturation * 100);
            if (!trackBarV.Focused || all)
                trackBarV.Value = (int)Math.Round(value * 100);
            if (!textBoxH.Focused || all)
                textBoxH.Text = trackBarH.Value.ToString();
            if (!textBoxS.Focused || all)
                textBoxS.Text = trackBarS.Value.ToString();
            if (!textBoxV.Focused || all)
                textBoxV.Text = trackBarV.Value.ToString();

            if (!safe)
            { // display SFC safe color, but show 24 bit values in input fields to avoid confusion
                (int r, int g, int b) = Program.ConvertSFCtoRGB(Program.ConvertRGBtoSFC(color.R, color.G, color.B));
                color = Color.FromArgb(r, g, b);
            }
            panelActiveColor.BackColor = color;
        }

        // #####################################################################################
        // Palette

        // Load palette from Program.palettes
        private void getPalette(int i)
        {
            (int r, int g, int b)[] palette = Program.GetPaletteRGB(i);
            for (int j = 0; j < 4; j++)
            {
                ActivePalette[j] = Color.FromArgb(palette[j].r, palette[j].g, palette[j].b);
                panelPalettebg.Controls[j].BackColor = ActivePalette[j];
            }

            pictureBox.Refresh();
            updatePaletteTextBox();
            buttonResetPalette.Enabled = false;
        }

        // Save palette in Program.palettes
        private bool setPalette(int i)
        {
            if (buttonResetPalette.Enabled)
            {
                (int r, int g, int b)[] palette = new (int, int, int)[4];
                for (int j = 0; j < 4; j++)
                {
                    palette[j] = (ActivePalette[j].R, ActivePalette[j].G, ActivePalette[j].B);
                }
                Program.SetPaletteRGB(i, palette);
                displayStatusText("Saved changes to palette " + comboBoxPaletteslot.Items[activePaletteSlot], 5000);
                buttonResetPalette.Enabled = false;
                return true;
            }
            return false;
        }

        // Update palette string field
        private void updatePaletteTextBox()
        {
            textBoxCurrentPalette.Text = $"{Program.ConvertColortoSFC(ActivePalette[0]):X4}" +
                $"{Program.ConvertColortoSFC(ActivePalette[1]):X4}" +
                $"{Program.ConvertColortoSFC(ActivePalette[2]):X4}" +
                $"{Program.ConvertColortoSFC(ActivePalette[3]):X4}";
        }

        // Move edited color to palette
        private void buttonSetColor_Click(object sender, EventArgs e)
        {
            int i = ((Button)sender).Name[11] - '1';
            ActivePalette[i] = panelActiveColor.BackColor;
            panelPalettebg.Controls[i].BackColor = panelActiveColor.BackColor;
            pictureBox.Refresh();
            updatePaletteTextBox();
            buttonResetPalette.Enabled = true;
        }

        // Move colors from palette to edit
        private void panelColor_Click(object sender, EventArgs e)
        {
            setColorinputs(ActivePalette[((Panel)sender).Name[10] - '1'], true, true);
        }

        // Palette slot selection changed
        private void comboBoxPaletteslot_SelectedValueChanged(object sender, EventArgs e)
        {
            setPalette(activePaletteSlot);
            activePaletteSlot = comboBoxPaletteslot.SelectedIndex;
            getPalette(activePaletteSlot);
        }

        // Undo changes
        private void buttonResetPalette_Click(object sender, EventArgs e)
        {
            getPalette(activePaletteSlot);
        }

        // #####################################################################################
        // Clipboard

        // Load / store from swatch
        private void storagepanelClick(object sender, EventArgs e)
        {
            if (groupBoxClipboard.Text == "Clipboard: Load")
                setColorinputs(((Panel)sender).BackColor, true, true);
            else
                ((Panel)sender).BackColor = panelActiveColor.BackColor;
        }

        // Switch clipboard modes
        private void buttonToggle_Click(object sender, EventArgs e)
        {
            string newMode = groupBoxClipboard.Text == "Clipboard: Load" ? "Store" : "Load";
            groupBoxClipboard.Text = "Clipboard: " + newMode;
            displayStatusText("Clipboard switched to " + newMode + " mode.", 4200);
        }

        // Load 4 colors from clipboard to active palette
        private void buttonLoadPalette_Click(object sender, EventArgs e)
        {
            int i = ((Button)sender).Name[10] - '1';
            for (int j = 0; j < 4; j++)
            {
                ActivePalette[j] = groupBoxClipboard.Controls[i * 4 + j].BackColor;
                panelPalettebg.Controls[j].BackColor = ActivePalette[j];
            }
            buttonResetPalette.Enabled = true;
            pictureBox.Refresh();
            updatePaletteTextBox();
        }

        // Save active palette to clipboard
        private void buttonStorePalette_Click(object sender, EventArgs e)
        {
            int i = ((Button)sender).Name[11] - '1';
            for (int j = 0; j < 4; j++)
            {
                groupBoxClipboard.Controls[i * 4 + j].BackColor = panelPalettebg.Controls[j].BackColor;
            }
        }

        // #####################################################################################
        // Preview screenshot

        // Game selection changed
        private void comboBoxGame_SelectedValueChanged(object sender, EventArgs e)
        { // switch screenshot
            pictureBox.Refresh();
        }

        // Replace colors with currently active palette while drawing the screenshot
        private void pictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (comboBoxGame.SelectedIndex < 0)
                comboBoxGame.SelectedIndex = 0;
            Graphics g = e.Graphics;
            Bitmap image = screenshots[comboBoxGame.SelectedIndex].image;

            Color[] colors = screenshots[comboBoxGame.SelectedIndex].colors;
            ColorMap[] colorMap = new ColorMap[] {
                new ColorMap {
                    OldColor = colors[0],
                    NewColor = ActivePalette[0]
                }, new ColorMap {
                    OldColor = colors[1],
                    NewColor = ActivePalette[1]
                }, new ColorMap {
                    OldColor = colors[2],
                    NewColor = ActivePalette[2]
                }, new ColorMap {
                    OldColor = colors[3],
                    NewColor = ActivePalette[3]
                }
            };
            ImageAttributes attr = new ImageAttributes();
            attr.SetRemapTable(colorMap);
            Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);
            g.DrawImage(image, rect, 0, 0, rect.Width, rect.Height, GraphicsUnit.Pixel, attr);
        }

        // Easter egg
        private void pictureBox_DoubleClick(object sender, EventArgs e)
        {
            if (comboBoxGame.SelectedIndex < 0)
                comboBoxGame.SelectedIndex = 0;
            screenshots[comboBoxGame.SelectedIndex] = fallbackScreenshot;
            pictureBox.Refresh();
        }

        // #####################################################################################
        // Load screenshots

        // Load screenshots from screenshot folder
        private void loadScreenshots(bool append = true)
        {
            if (!append)
            {
                screenshots = new List<(Bitmap, Color[])> { };
                comboBoxGame.Items.Clear();
            }

            try
            {
                DirectoryInfo screenshotFolder = new DirectoryInfo("gb_screenshots/");
                string[] filetypes = new[] { "*.png", "*.bmp" };
                List<FileInfo> files = filetypes.SelectMany(screenshotFolder.EnumerateFiles).Take(256).OrderBy(o => o.Name).ToList();

                foreach (FileInfo file in files)
                {
                    if (file.Name.Length > 4 && file.Length > 500 && file.Length <= 69174) // 69174 = uncompressed 24 bit bmp
                    {
                        try
                        {
                            Bitmap screenshot = new Bitmap(file.FullName);
                            if (screenshot.Width == 160 && screenshot.Height == 144)
                            {
                                Color[] screenshotColors = getBitmapColors(screenshot).OrderByDescending(c => c.GetBrightness()).ToArray();
                                screenshots.Add((screenshot, screenshotColors));
                                comboBoxGame.Items.Add(file.Name.Substring(0, file.Name.Length - 4));
                                if (file.Name == "Tetris.png")
                                    comboBoxGame.SelectedIndex = comboBoxGame.Items.Count - 1;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch
            {
                try
                {
                    System.IO.Directory.CreateDirectory("gb_screenshots");
                    System.IO.File.WriteAllText("gb_screenshots/add_screenshots_here.txt", "Add your own screenshots here!\r\n\r\nFormat: 160 x 144 pixels png or bmp\r\n");
                }
                catch { }
            }

            if (screenshots.Count == 0)
            {
                comboBoxGame.Items.Add("No screenshots found");
                screenshots.Add(fallbackScreenshot);
            }

            if (comboBoxGame.SelectedIndex < 0)
                comboBoxGame.SelectedIndex = 0;

            pictureBox.Refresh();
        }

        // Get a list of the first 4 colors used in bitmap
        private List<Color> getBitmapColors(Bitmap b)
        {
            List<Color> colors = new List<Color> { };
            for (int x = 0; x < b.Width; x++)
            {
                for (int y = 0; y < b.Height; y++)
                {
                    Color pixelColor = b.GetPixel(x, y);
                    if (!colors.Contains(pixelColor))
                    {
                        colors.Add(pixelColor);
                        if (colors.Count == 4)
                            return colors;
                    }
                }
            }
            if (colors.Count < 4) // pad list with invisible color
            {
                colors.AddRange(Enumerable.Repeat(Color.FromArgb(0, 1, 2, 3), 4 - colors.Count));
            }
            return colors;
        }

        // #####################################################################################
        // Import / Export section

        // Sync controls check box with menu 
        private void checkBoxControls_CheckedChanged(object sender, EventArgs e)
        {
            controlTypeAToolStripMenuItem.Checked = checkBoxControls.Checked;
        }

        // Import data from SGB rom file
        private void buttonImport_Click(object sender, EventArgs e)
        {
            openFileDialog.Title = "Select Super Game Boy rom file.";
            openFileDialog.Filter = "SNES ROM files|*.sfc; *.bin|All files|*.*";
            openFileDialog.FileName = "";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UseWaitCursor = true;
                (bool success, bool buttonTypeA, int border, string text) = Program.LoadDatafromFile(openFileDialog.FileName);
                if (success)
                {
                    getPalette(activePaletteSlot);
                    displayStatusText("Successfully loaded data from file.");
                    checkBoxControls.Checked = buttonTypeA;
                    refreshPresetData();
                    refreshBorderCombobox();
                    loaded_rom_file = Path.GetFileName(openFileDialog.FileName);
                    if (border < comboBoxBorder.Items.Count)
                    {
                        comboBoxBorder.SelectedIndex = border;
                    }
                    else
                    {
                        displayStatusText("Error loading border.");
                        comboBoxBorder.SelectedIndex = 0;
                    }
                }
                else
                {
                    displayStatusText("Error loading data from file: " + text);
                }
                UseWaitCursor = false;
            }
        }

        // Import border images from SGB rom file without changing any settings
        private void buttonImportImages_Click(object sender, EventArgs e)
        {
            openFileDialog.Title = "Select Super Game Boy rom file.";
            openFileDialog.Filter = "SNES ROM files|*.sfc; *.bin|All files|*.*";
            openFileDialog.FileName = "";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UseWaitCursor = true;
#if DEBUG
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
#endif
                bool success = Program.loadImagesfromFile(openFileDialog.FileName);
#if DEBUG
                stopwatch.Stop();
                Console.WriteLine($"Loading data took {stopwatch.ElapsedMilliseconds}ms.");
#endif
                if (success)
                {
                    refreshBorderCombobox();
                    displayStatusText("Successfully loaded border images.");
                }
                else
                {
                    displayStatusText("Error loading border images.");
                }
                UseWaitCursor = false;
            }
        }

        // Export as IPS patch
        private void buttonIps_Click(object sender, EventArgs e)
        {
            setPalette(activePaletteSlot); // make sure current palette is saved
            ConfirmationDialog dialog = new ConfirmationDialog();
            dialog.ShowDialog();
            if (dialog.DialogResult != DialogResult.Cancel)
            {
                (bool success, string message) = Program.SaveIPS(sgb_rev, checkBoxControls.Checked, Program.loadedBorders.Count > 0 ? Program.loadedBorders[comboBoxBorder.SelectedIndex].i : comboBoxBorder.SelectedIndex);
                displayStatusText(success ? "Saved patch as \"" + message + "\"." : message);
            }
            else
                displayStatusText("Action cancelled, patch was not saved.");
        }

        // Modify SGB rom file with palette and control mode
        private void buttonModify_Click(object sender, EventArgs e)
        {
            setPalette(activePaletteSlot); // make sure current palette is saved
            saveFileDialog.Title = "Select Super Game Boy rom file - FILE WILL BE MODIFIED";
            saveFileDialog.Filter = "SNES ROM files|*.sfc; *.bin|All files|*.*";
            saveFileDialog.FileName = loaded_rom_file;
            DialogResult result = saveFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                (bool success, string text) = Program.SavetoFile(saveFileDialog.FileName, checkBoxControls.Checked, Program.loadedBorders.Count > 0 ? Program.loadedBorders[comboBoxBorder.SelectedIndex].i : comboBoxBorder.SelectedIndex);
                if (success)
                {
                    toolStripStatusLabel.Text = "Successfully saved changes to file. " + text;
                }
                else
                {
                    toolStripStatusLabel.ForeColor = Color.Red;
                    toolStripStatusLabel.Text = "Error while writing to file: " + text;
                }
                resetStatusText();
            }
        }

        // Parse palette string
        private void textBoxCurrentPalette_TextChanged(object sender, EventArgs e)
        {
            if (!((TextBox)sender).Focused || textBoxCurrentPalette.Text.Length != 16)
                return;

            try
            {
                if (!long.TryParse(textBoxCurrentPalette.Text, System.Globalization.NumberStyles.HexNumber, null, out long dontcare))
                    return;

                bool palette_change = false;

                for (int i = 0; i < 4; i++)
                {
                    Color c = Program.ConvertSFCtoColor(int.Parse(textBoxCurrentPalette.Text.Substring(i * 4, 4), System.Globalization.NumberStyles.HexNumber));
                    if (c != ActivePalette[i])
                    {
                        ActivePalette[i] = c;
                        panelPalettebg.Controls[i].BackColor = ActivePalette[i];
                        palette_change = true;
                    }
                }

                if (palette_change)
                {
                    pictureBox.Refresh();
                    updatePaletteTextBox();
                    textBoxCurrentPalette.Select(16, 0);
                    buttonResetPalette.Enabled = true;
                    displayStatusText("Palette ok", 5000);
                }
            }
            catch { }
        }

        // Export palettes to .pal file
        private void exportPalettesToolStripMenuItem_Click(object sender, EventArgs e)
        {

            saveFileDialog.Title = "Save As";
            saveFileDialog.Filter = "Palette files|*.pal|All files|*.*";
            saveFileDialog.FileName = "sgb_palettes.pal";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                setPalette(activePaletteSlot); // save current palette

                byte[] palettes = new byte[256];
                for (int i = 0; i < 32; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        byte[] p = BitConverter.GetBytes(Program.GetPaletteValue(i, j));
                        palettes[i * 8 + j * 2] = p[0];
                        palettes[i * 8 + j * 2 + 1] = p[1];
                    }
                }
                try
                {
                    File.WriteAllBytes(saveFileDialog.FileName, palettes.ToArray());
                    displayStatusText("Exported palettes to file.", 5000);
                }
                catch
                {
                    displayStatusText("Could not write to file.", 5000);
                }
            }
        }

        // Export palettes as .csv
        private void exportPalettescsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog.Title = "Save As";
            saveFileDialog.Filter = "CSV files|*.csv; *.txt|All files|*.*";
            saveFileDialog.FileName = "sgb_palettes.csv";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                setPalette(activePaletteSlot); // save current palette

                String palettes_export = "color1,color2,color3,color4\r\n";
                for (int i = 0; i < 32; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        Color c = Program.ConvertSFCtoColor(Program.GetPaletteValue(i, j));
                        palettes_export += $"#{c.R:X2}{c.G:X2}{c.B:X2}{(j < 3 ? "," : (i < 31 ? "\r\n" : ""))}";
                    }
                }
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, palettes_export);
                    displayStatusText("Exported palettes to csv file.", 5000);
                }
                catch
                {
                    displayStatusText("Could not write to file.", 5000);
                }
            }
        }

        // Import palettes from .pal file
        private void importPalettesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog.Title = "Open sgb_palettes.pal";
            openFileDialog.Filter = "Palette files|*.pal|All files|*.*";
            openFileDialog.FileName = "sgb_palettes.pal";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (comboBoxPaletteslot.SelectedIndex < 0)
                    comboBoxPaletteslot.SelectedIndex = 0;

                using (FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read))
                {
                    bool success = Program.loadPalettesfromFileStream(fs, 0x0000);

                    if (success)
                        displayStatusText("Imported palettes from file.", 5000);
                    else
                        displayStatusText("Could not import palettes from file.", 5000);

                    getPalette(comboBoxPaletteslot.SelectedIndex);
                }
            }
        }

        // #####################################################################################
        // Menu strip

        private void importToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Import palettes, game presets, button config and border data from an SGB rom file.", 60000);
        }

        private void modifyToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Modify SGB rom file with your custom palettes, game presets, button config and border selection.", 60000);
        }

        private void savePatchToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Generate an ips patch and share it! (Custom border images are not included)", 60000);
        }

        private void importPalettesToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Import all palettes from a .pal file.", 60000);
        }

        private void exportPalettesToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Export all palettes as a .pal file.", 60000);
        }
        private void exportPalettescsvToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Export all palettes as a .csv file.", 60000);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void paletteEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControlMain.SelectedIndex = 0;
            paletteEditorToolStripMenuItem.Checked = true;
            presetsToolStripMenuItem.Checked = false;
            startupBorderToolStripMenuItem.Checked = false;
            palettePasswordsToolStripMenuItem.Checked = false;
        }

        private void presetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControlMain.SelectedIndex = 1;
            paletteEditorToolStripMenuItem.Checked = false;
            presetsToolStripMenuItem.Checked = true;
            startupBorderToolStripMenuItem.Checked = false;
            palettePasswordsToolStripMenuItem.Checked = false;
        }

        private void startupBorderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //refreshBorderCombobox();
            pictureBoxGameinBorder.Image = pictureBox.Image;
            tabControlMain.SelectedIndex = 2;
            paletteEditorToolStripMenuItem.Checked = false;
            presetsToolStripMenuItem.Checked = false;
            startupBorderToolStripMenuItem.Checked = true;
            palettePasswordsToolStripMenuItem.Checked = false;
        }

        private void palettePasswordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            setPalette(activePaletteSlot);
            ConvertPalettePassword();
            tabControlMain.SelectedIndex = 3;
            paletteEditorToolStripMenuItem.Checked = false;
            presetsToolStripMenuItem.Checked = false;
            startupBorderToolStripMenuItem.Checked = false;
            palettePasswordsToolStripMenuItem.Checked = true;
            //textBoxPasswords.Focus();
        }

        private void controlTypeAToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxControls.Checked = controlTypeAToolStripMenuItem.Checked;
        }

        private void controlTypeAToolStripMenuItem_MouseEnter(object sender, EventArgs e)
        {
            displayStatusText("Change default controls to Type A.", 60000);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox().ShowDialog();
        }

        private void toolStripMenuItem_MouseLeave(object sender, EventArgs e)
        {
            hideStatusText();
        }

        // #####################################################################################
        // Status bar

        private void displayStatusText(string msg, int duration = 6000)
        {
            toolStripStatusLabel.Text = msg;
            resetStatusText(duration);
        }

        private void hideStatusText()
        {
            toolStripStatusLabel.Text = "";
            timer = DateTime.Now;
        }

        // Reset status bar text without blocking the UI
        internal async Task resetStatusText(int delay = 6000)
        {
            timer = DateTime.Now.AddSeconds(delay / 1000 - 1);
            await Task.Delay(delay);
            if (DateTime.Now > timer)
                toolStripStatusLabel.Text = "";
            toolStripStatusLabel.ForeColor = Color.Black;
        }

        // #####################################################################################
        // Game presets

        // Write game presets to the text boxes
        private void refreshPresetData()
        {
            for (int i = 0; i < Program.gamePresets.Count; i++)
            {
                groupBoxPresets.Controls[2 * i + 1].Text = (Program.gamePresets[i].n - 1) / 8 + 1 + "-" + (char)('A' + ((Program.gamePresets[i].n - 1) % 8));
                groupBoxPresets.Controls[2 * i].Text = Program.gamePresets[i].game;
            };
            for (int i = Program.gamePresets.Count; i < 36; i++)
            {
                groupBoxPresets.Controls[2 * i + 1].Text = "";
                groupBoxPresets.Controls[2 * i].Text = "";
            }
        }

        // Handle changes to the titles
        private void textBoxTitle_TextChanged(object sender, EventArgs e)
        {
            try
            {
                int i = groupBoxPresets.Controls.IndexOfKey(((TextBox)sender).Name);
                int presetslot = i / 2;

                if (((TextBox)sender).Text == "")
                    groupBoxPresets.Controls[i + 1].Text = "";
                else if (groupBoxPresets.Controls[i + 1].Text == "")
                    groupBoxPresets.Controls[i + 1].Text = "1-A";

                if (!((TextBox)sender).Focused)
                    return;

                if (presetslot > Program.gamePresets.Count())
                {
                    if (((TextBox)sender).Text != "")
                    {
                        TextBox nextPresetTextbox = (TextBox)groupBoxPresets.Controls[2 * Program.gamePresets.Count()];
                        nextPresetTextbox.Focus();
                        nextPresetTextbox.Text = ((TextBox)sender).Text;
                        nextPresetTextbox.Select(nextPresetTextbox.Text.Length, 0);
                        ((TextBox)sender).Text = "";
                    }
                    return;
                }

                if (((TextBox)sender).Text == "")
                {
                    Program.RemoveGamePreset(presetslot);
                    groupBoxPresets.Controls[2 * Program.gamePresets.Count()].Text = "";
                    refreshPresetData();
                }
                else
                {
                    if (presetslot < Program.gamePresets.Count())
                        Program.SetGamePreset((groupBoxPresets.Controls[i].Text, Program.ConvertSlottoNumber(groupBoxPresets.Controls[i + 1].Text)), presetslot);
                    else
                        Program.SetGamePreset((groupBoxPresets.Controls[i].Text, Program.ConvertSlottoNumber(groupBoxPresets.Controls[i + 1].Text)));
                }
            }
            catch { }
        }

        // Save preset slot
        private void textBoxPreset_TextChanged(object sender, EventArgs e)
        {
            TextBox slotBox = (TextBox)sender;
            if (((TextBox)sender).Focused && slotBox.Text.Length == 3)
            {
                int i = groupBoxPresets.Controls.IndexOfKey((slotBox).Name);
                int presetslot = (i - 1) / 2;
                slotBox.Text = slotBox.Text.ToUpper();
                int convertedNumber = Program.ConvertSlottoNumber(slotBox.Text);
                if (convertedNumber < 1)
                    convertedNumber = 1;
                else if (convertedNumber > 32)
                    convertedNumber = 32;
                slotBox.Text = (convertedNumber - 1) / 8 + 1 + "-" + (char)('A' + ((convertedNumber - 1) % 8));
                slotBox.Select(3, 0);
                if (presetslot < Program.gamePresets.Count()) // ignore if there's no game title
                    Program.gamePresets[presetslot] = (groupBoxPresets.Controls[i - 1].Text, convertedNumber);
            }
        }

        // Reset preset slot text box on focus leave
        private void textBoxPreset_Leave(object sender, EventArgs e)
        {
            TextBox slotBox = (TextBox)sender;
            int i = groupBoxPresets.Controls.IndexOfKey((slotBox).Name);
            int presetslot = (i - 1) / 2;
            if (presetslot < Program.gamePresets.Count() && groupBoxPresets.Controls[i - 1].Text != "")
                slotBox.Text = (Program.gamePresets[presetslot].n - 1) / 8 + 1 + "-" + (char)('A' + ((Program.gamePresets[presetslot].n - 1) % 8));
            else
                slotBox.Text = "";
        }

        // Read game title from internal header in .gb or .gbc rom file
        private void buttonReadGB_Click(object sender, EventArgs e)
        {
            openFileDialog.Title = "Select Game Boy file";
            openFileDialog.Filter = "Game Boy ROM files|*.gb; *.gbc|All files|*.*";
            openFileDialog.FileName = "";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                (bool success, string gbTitle) = Program.ReadGBName(openFileDialog.FileName);
                if (!success)
                {
                    displayStatusText("Unsupported game: " + gbTitle);
                }
                else
                {
                    for (int i = 0; i < 32; i++)
                    {
                        if (groupBoxPresets.Controls[2 * i].Text == gbTitle)
                        {
                            displayStatusText("Game already in list.");
                            return;
                        }
                        if (groupBoxPresets.Controls[2 * i].Text == "")
                        {
                            groupBoxPresets.Controls[2 * i].Text = gbTitle;
                            groupBoxPresets.Controls[2 * i + 1].Text = "1-A";
                            Program.SetGamePreset((gbTitle, 1));
                            break;
                        }
                    }
                }
            }
        }

        // #####################################################################################
        // Borders

        // Update combo box if loadedBorders changed
        private void refreshBorderCombobox()
        {
            string[] borderNames = Program.loadedBorders.Select(b => b.name).ToArray();
            int selectedBorderIndex = Program.loadedBorders.FindIndex(b => b.name == (string)comboBoxBorder.SelectedItem);
            if (Program.loadedBorders.Count > 0)
            {
                if (!Enumerable.SequenceEqual(comboBoxBorder.Items.OfType<string>(), borderNames))
                {
                    comboBoxBorder.Items.Clear();
                    comboBoxBorder.Items.AddRange(borderNames);
                }
                comboBoxBorder.SelectedIndex = selectedBorderIndex < 0 ? 0 : selectedBorderIndex;
                drawBorder();
            }
        }

        // Display selected border
        private void comboBoxBorder_SelectedIndexChanged(object sender, EventArgs e)
        {
            drawBorder();
        }

        private void drawBorder()
        {
            //pictureBoxBorder.Image = (Bitmap)Properties.Resources.ResourceManager.GetObject("sgb_borders"+(((ComboBox)sender).SelectedIndex + 1));
            if (Program.loadedBorders.Count > comboBoxBorder.SelectedIndex && Program.loadedBorders[comboBoxBorder.SelectedIndex].image != null)
                pictureBoxBorder.Image = Program.loadedBorders[comboBoxBorder.SelectedIndex].image;
        }

        // Save currently displayed border as png file
        private void buttonSaveBorderpng_Click(object sender, EventArgs e)
        {
            if(pictureBoxBorder.Image == null)
            {
                displayStatusText("Error: No border loaded.");
                return;
            }

            saveFileDialog.Title = "Save as";
            saveFileDialog.Filter = "PNG Image|*.png";
            saveFileDialog.FileName = "border.png";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    pictureBoxBorder.Image.Save(saveFileDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                }
                catch
                {
                    displayStatusText("Error: Could not save file.");
                    return;
                }
                displayStatusText($"Image saved as { saveFileDialog.FileName }");
            }
        }

        // #####################################################################################
        // Palette Passwords

        // Convert password input and update color panels
        private void ConvertPalettePassword()
        {
            var (valid, paletteDependant, palette) = Passwords.ConvertPassword(textBoxPasswords.Text, checkBoxPasswordCustom.Checked);

            int dashes = textBoxPasswords.Text.Count(c => c == '-');
            if (textBoxPasswords.Text.Length - dashes > 12)
            {
                textBoxPasswords.Text = textBoxPasswords.Text.Substring(0, 12 + dashes);
                textBoxPasswords.Select(12 + dashes, 0);
            }

            if (valid)
            {
                if (dashes == 0)
                {
                    textBoxPasswords.Text = textBoxPasswords.Text.Substring(0, 4) + "-" + textBoxPasswords.Text.Substring(4, 4) + "-" + textBoxPasswords.Text.Substring(8, 4);
                    textBoxPasswords.Select(14, 0);
                }

                Panel[] colorPanels = groupBoxPasswords.Controls.OfType<Panel>().ToArray();
                for (int i = 0; i < 4; i++)
                {
                    Color c = Program.ConvertSFCtoColor(palette[i]);
                    colorPanels[i].Controls[0].BackColor = c;
                    toolTip.SetToolTip(colorPanels[i].Controls[0], "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"));
                }
                labelPasswordWarning.Visible = paletteDependant;
            }
        }

        private void textBoxPasswords_TextChanged(object sender, EventArgs e)
        {
            ConvertPalettePassword();
        }

        private void checkBoxPasswordCustom_CheckedChanged(object sender, EventArgs e)
        {
            ConvertPalettePassword();
        }

        // Copy colors from password panels to active palette
        private void buttonPasswordSetActivePalette_Click(object sender, EventArgs e)
        {
            Panel[] colorPanels = groupBoxPasswords.Controls.OfType<Panel>().ToArray();
            for (int i = 0; i < 4; i++)
            {
                ActivePalette[i] = colorPanels[i].Controls[0].BackColor;
                panelPalettebg.Controls[i].BackColor = ActivePalette[i];
            }

            pictureBox.Refresh();
            updatePaletteTextBox();
            buttonResetPalette.Enabled = true;

            paletteEditorToolStripMenuItem_Click(sender, e);
        }

        // Move clicked on color to edit slot
        private void panelPasswordColor_Click(object sender, EventArgs e)
        {
            setColorinputs(((Panel)sender).BackColor, true, true);
            paletteEditorToolStripMenuItem_Click(sender, e);
        }
    }
}

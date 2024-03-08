using System.Globalization;
using System.IO;
using System.Windows;

namespace CS2MinecraftMapOptimizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // NOCHECKIN remove this
            //GameDirTextBox.Text = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike Global Offensive\\game\\csgo";
            //VmapTextBox.Text    = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike Global Offensive\\content\\csgo_addons\\minecraft\\maps\\minecraft.vmap";

            // Set default settings
            MinecraftBlockSizeTextBox.Text = "48.0";
            OceanOptiCheckBox.IsChecked = true;
            OceanZCoordTextBox.Text = "-472.0";

            // Disable some elements on startup
            CopyOutputsToClipboardButton.IsEnabled = false;
        }

        // Returns true if the backup was created, false otherwise
        private bool CreateBackupOfFile(string filePath)
        {
            try
            {
                string vmapPath = filePath;                                   // e.g. "C:\\map_data\\mymap.vmap"
                string vmapDir  = Path.GetDirectoryName           (vmapPath); // e.g. "C:\\map_data"
                string vmapName = Path.GetFileNameWithoutExtension(vmapPath); // e.g. "mymap"
                string vmapExt  = Path.GetExtension               (vmapPath); // e.g. ".vmap"

                // Add timestamp to backup file name
                string currentTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH\\hmm\\mss\\s");
                string backupVmapName = vmapName + "_BACKUP_" + currentTimestamp;
                string backupVmapPath = Path.Combine(vmapDir, backupVmapName + vmapExt);

                // Copy VMAP file. Will not overwrite if the destination file already exists.
                File.Copy(vmapPath, backupVmapPath, false);
            }
            catch (Exception e) // All kinds of exceptions can be thrown here
            {
                Console.WriteLine("ERROR: Failed to create backup of VMAP file: " + e.GetType().ToString() + ": " + e.Message);
                return false; // Failure
            }
            return true; // Success
        }

        private void GameDirButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new();
            dialog.Multiselect = false;
            dialog.Title = "Select Counter-Strike 2's game\\csgo\\ folder";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string fullPathToFolder = dialog.FolderName;
                string folderNameOnly = dialog.SafeFolderName;
                GameDirTextBox.Text = fullPathToFolder;
            }
        }

        private void VmapButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "YOUR-MAP.vmap"; // Default file name
            dialog.DefaultExt = ".vmap"; // Default file extension
            dialog.Filter = "CS2 Map Source File|*.vmap"; // Filter files by extension

            bool? result = dialog.ShowDialog();
            if (result == true)
                VmapTextBox.Text = dialog.FileName;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // This method is called when the checkbox is checked
            OceanZCoordLabel  .IsEnabled = true;
            OceanZCoordTextBox.IsEnabled = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // This method is called when the checkbox is unchecked
            OceanZCoordLabel  .IsEnabled = false;
            OceanZCoordTextBox.IsEnabled = false;
        }
        private void AddOptiButton_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = GameDirTextBox.Text;
            string vmapPath = VmapTextBox.Text;

            // Disable button if it was enabled from previous runs
            CopyOutputsToClipboardButton.IsEnabled = false;

            // Create backup of user's VMAP file before we modify it.
            if (!CreateBackupOfFile(vmapPath)) // If backup creation failed, abort.
                return;

            // Parse minecraft block size value
            string blockSizeStr = MinecraftBlockSizeTextBox.Text;
            float blockSize = float.Parse(blockSizeStr, CultureInfo.InvariantCulture.NumberFormat);

            // Parse ocean height value
            string oceanZCoordStr = OceanZCoordTextBox.Text;
            float oceanZCoord = float.Parse(oceanZCoordStr, CultureInfo.InvariantCulture.NumberFormat);

            bool success = Optimizer.AddOptimizations(gameDir, vmapPath, blockSize, (bool)OceanOptiCheckBox.IsChecked, oceanZCoord);
            if (!success)
            {
                Console.WriteLine("ERROR! Failed to add optimizations to VMAP, something went wrong.");
                return;
            }

            // If successful, enable further buttons
            CopyOutputsToClipboardButton.IsEnabled = true;
        }

        private void RemoveOptiButton_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = GameDirTextBox.Text;
            string vmapPath = VmapTextBox.Text;

            // Create backup of user's VMAP file before we modify it.
            if (!CreateBackupOfFile(vmapPath)) // If backup creation failed, abort.
                return;

            Optimizer.RemoveOptimizations(gameDir, vmapPath);
        }

        private void CopyOutputsToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(Optimizer.logicAutoOutputs);
        }
    }
}
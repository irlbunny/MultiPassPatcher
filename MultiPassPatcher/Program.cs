using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MultiPassPatcher
{
    public class Program
    {
        public enum RenderMode
        {
            MultiPass,
            SinglePassInstanced
        }

        // TODO: other platforms.
        private static readonly List<string> _assetNames = new List<string>
        {
            "Standalone"
        };

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Select your Beat Saber installation folder.");

            var beatSaberDir = new FolderBrowserDialog();
            if (beatSaberDir.ShowDialog() != DialogResult.OK)
            {
                Console.WriteLine("Beat Saber installation folder was not selected, not patching anything! Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var beatSaberDataPath = Path.Combine(beatSaberDir.SelectedPath, "Beat Saber_Data");
            var globalGameManagersTempPath = Path.Combine(beatSaberDataPath, "globalgamemanagers.assets.tmp");
            var globalGameManagersPath = Path.Combine(beatSaberDataPath, "globalgamemanagers.assets");
            var managedPath = Path.Combine(beatSaberDataPath, "Managed");

            var replacers = new List<AssetsReplacer>();
            var manager = new AssetsManager();
            manager.MonoTempGenerator = new MonoCecilTempGenerator(managedPath);
            manager.LoadClassPackage("classdata.tpk");

            var assetFileInstance = manager.LoadAssetsFile(globalGameManagersPath, true);
            var assetFile = assetFileInstance.file;

            manager.LoadClassDatabaseFromPackage(assetFile.Metadata.UnityVersion);

            AssetFileInfo openXrSettingsInfo = null;
            AssetTypeValueField openXrSettings = null;
            foreach (var info in assetFile.GetAssetsOfType(AssetClassID.MonoBehaviour))
            {
                var monoBehaviour = manager.GetBaseField(assetFileInstance, info);
                var baseBehaviour = manager.GetBaseField(assetFileInstance, assetFile.GetAssetInfo(monoBehaviour["m_Script"]["m_PathID"].AsLong));
                if (baseBehaviour["m_Name"].AsString == "OpenXRSettings" && _assetNames.Contains(monoBehaviour["m_Name"].AsString))
                {
                    openXrSettingsInfo = info;
                    openXrSettings = monoBehaviour;
                    break;
                }
            }

            Console.WriteLine("Patching globalgamemanagers OpenXR settings...");

            if (openXrSettingsInfo != null && openXrSettings != null)
            {
                Console.WriteLine($"OpenXR settings for \"{openXrSettings["m_Name"].AsString}\" found!");

                var currentRenderMode = (RenderMode)openXrSettings["m_renderMode"].AsInt;
                Console.WriteLine($"Current Render Mode = {currentRenderMode}");

                if (currentRenderMode == RenderMode.SinglePassInstanced)
                    openXrSettings["m_renderMode"].AsInt = (int)RenderMode.MultiPass;
                else if (currentRenderMode == RenderMode.MultiPass)
                    openXrSettings["m_renderMode"].AsInt = (int)RenderMode.SinglePassInstanced;

                Console.WriteLine($"New Render Mode = {(RenderMode)openXrSettings["m_renderMode"].AsInt}");

                replacers.Add(new AssetsReplacerFromMemory(assetFile, openXrSettingsInfo, openXrSettings));
            }
            else
                Console.WriteLine("OpenXR settings could not be found, Multi-Pass Patcher currently only supports Beat Saber on PC using Mono.");

            using (var writer = new AssetsFileWriter(globalGameManagersTempPath))
            {
                assetFile.Write(writer, 0, replacers);
            }

            assetFile.Close();

            File.Delete(globalGameManagersPath);
            File.Move(globalGameManagersTempPath, globalGameManagersPath);

            Console.WriteLine("Patched globalgamemanagers OpenXR settings, your game should now use Multi-Pass rendering! Press any key to exit...");
            Console.ReadKey();
        }
    }
}

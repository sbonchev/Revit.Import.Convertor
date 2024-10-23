using System.Diagnostics;
using System.Runtime.Versioning;
using AdWin = Autodesk.Windows;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Autodesk.Revit.UI;
using System.Windows;
using System.IO;
using System.Reflection;
using Revit.Import.Convertor.UI;


namespace Revit.Import.Convertor.App
{
    [SupportedOSPlatform("windows")]
    public class RevitApp : IExternalApplication
    {
        Result IExternalApplication.OnShutdown(UIControlledApplication app)
        {
            return Result.Succeeded;
        }

        Result IExternalApplication.OnStartup(UIControlledApplication app)
        {
            const string tabName = "CustomAddInsTab";
            const string tabTitle = "Format Convertor";
            const string panName = "CustomPanel";
            try
            {
                app.CreateRibbonTab(tabName);
                var tab = AdWin.ComponentManager.Ribbon.Tabs.Single(t => t.Name == tabName);
                tab.Title = tabTitle;
                var asmName = Assembly.GetExecutingAssembly().Location;
                var img = ImageSourceForBitmap(Resource.cad_xyz_32);
                var btnPush = new PushButtonData("btnPush", "Converters", asmName, "Revit.Import.Convertor.App.Command")
                {
                    LargeImage = img,
                    Image = img,
                    ToolTip = "Files Format Converter",
                    ToolTipImage = img,
                };
                var panel = app.CreateRibbonPanel(tabName, panName);
                panel.Title = "Files Convertors";
                panel.AddItem(btnPush);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return Result.Failed;
            }

            return AddInRibbonButton(app);
        }

        /// <summary>
        /// Avoid unmanage leak
        /// </summary>
        /// <param name="hObject"></param>
        /// <returns></returns>
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject([In] IntPtr hObject);

        private static ImageSource ImageSourceForBitmap(byte[] msBmp)
        {
            IntPtr handle;
            using (var ms = new MemoryStream(msBmp))
            {
                var bmp = new Bitmap(ms);
                handle = bmp.GetHbitmap();
            }
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        private AdWin.RibbonPanel? GetRibbonPanel(UIControlledApplication app)
        {
            const string tabId = "Add-Ins";
            const string panName = "CustomPanel";
            try
            {
                var rbs = AdWin.ComponentManager.Ribbon;
                var tab = rbs.Tabs.FirstOrDefault(t => t.Id == tabId || t.Name == tabId);
                if (tab == null)
                {
                    app.CreateRibbonTab(tabId);
                    tab = rbs.Tabs.Single(t => t.Id == tabId || t.Name == tabId);
                }
                tab.Name = tabId;
                tab.IsEnabled = true;
                var pan = tab.Panels?.FirstOrDefault(p => p.Id == panName);
                if (pan == null)
                {
                    var panelSource = new AdWin.RibbonPanelSource
                    {
                        Title = "Files Converter",
                    };
                    pan = new AdWin.RibbonPanel
                    {
                        Source = panelSource,
                        IsEnabled = true
                    };
                }
                tab.Panels!.Add(pan);
                pan.IsEnabled = true;
                pan.IsVisible = true;

                return pan;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        private Result AddInRibbonButton(UIControlledApplication app)
        {
            var panel = GetRibbonPanel(app);
            if (panel == null)
                return Result.Failed;


            var fileProc = new FileProcessing();
            var extEvent = ExternalEvent.Create(fileProc);

            var img = ImageSourceForBitmap(Resource.cad_xyz_32);
            var tt = new AdWin.RibbonToolTip { Image = img, Title = "Files Format Converter" };
            var pushBtn = new AdWin.RibbonButton
            {
                Text = "Converters",
                LargeImage = img,
                Image = img,
                ShowImage = true,
                ShowText = true,
                IsEnabled = true,
                Size = AdWin.RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                ResizeStyle = AdWin.RibbonItemResizeStyles.Collapse,
                ToolTip = tt,
                CommandHandler = new RelayRibbonCommand((_) => new FormatConvertorWindow(fileProc, extEvent), (_) => true)
            };
            panel.Source.Items.Add(pushBtn);

            return Result.Succeeded;
        }

    }

}

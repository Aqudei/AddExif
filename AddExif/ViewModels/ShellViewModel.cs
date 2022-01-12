using AddExif.Models;
using CsvHelper;
using MahApps.Metro.Controls.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace AddExif.ViewModels
{
    class ShellViewModel : BindableBase
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public string Title => "ExifAdder App";


        private DelegateCommand _chooseFolderCommand;
        private string _inputFolder;
        private string _inputCsv;
        private DelegateCommand _chooseCsvCommand;
        private readonly IDialogCoordinator _dialogCoordinator;
        private readonly Dispatcher Dispatcher;
        private ObservableCollection<ImageInfo> _images = new ObservableCollection<ImageInfo>();
        private DelegateCommand _runCommand;
        private string _logs;

        public string Logs
        {
            get { return _logs; }
            set { SetProperty(ref _logs, value); }
        }

        public ICollectionView Images => CollectionViewSource.GetDefaultView(_images);

        public DelegateCommand RunCommand
        {
            get
            {
                _runCommand = _runCommand ?? new DelegateCommand(DoRun, CanRun).ObservesProperty(() => Images.IsEmpty);
                return _runCommand;
            }
        }

        private bool CanRun()
        {
            return !Images.IsEmpty;
        }

        public DelegateCommand ChooseFolderCommand
        {
            get
            {
                _chooseFolderCommand = _chooseFolderCommand ?? new DelegateCommand(ChooseFolder, CanChooseFolder).ObservesProperty(() => InputCsv);
                return _chooseFolderCommand;
            }
        }

        private bool CanChooseFolder()
        {
            return !string.IsNullOrWhiteSpace(InputCsv);
        }

        public DelegateCommand ChooseCsvCommand
        {
            get
            {
                _chooseCsvCommand = _chooseCsvCommand ?? new DelegateCommand(ChooseCsv);
                return _chooseCsvCommand;
            }
        }

        public string InputFolder { get => _inputFolder; set => SetProperty(ref _inputFolder, value); }
        public string InputCsv { get => _inputCsv; set => SetProperty(ref _inputCsv, value); }

        public ShellViewModel(IDialogCoordinator dialogCoordinator)
        {
            _dialogCoordinator = dialogCoordinator;
            Dispatcher = Application.Current.Dispatcher;



        }
        private async void ChooseFolder()
        {
            try
            {
                var selectFolderDialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Multiselect = false
                };

                var dlgResult = selectFolderDialog.ShowDialog();
                if (dlgResult == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                {
                    InputFolder = selectFolderDialog.FileName;
                    await ReadImages();
                }

            }
            catch (Exception ex)
            {
                await _dialogCoordinator.ShowMessageAsync(this, "Something went wrong", $"{ex.Message}\n\n{ex.StackTrace}");
                Logger.Error(ex);
            }
        }

        private Task ReadImages()
        {
            return Task.Run(() =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _images.Clear();
                }));

                using (var reader = new StreamReader(InputCsv))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var files = Directory.EnumerateFiles(InputFolder, "*.*").Where(f => f.ToLower().EndsWith(".png") || f.ToLower().EndsWith(".jpg")).Select(f => Path.GetFileName(f).ToLower());

                    var records = csv.GetRecords<ImageInfo>();
                    foreach (var rec in records)
                    {
                        if (files.Contains(rec.FileName.ToLower()))
                        {
                            rec.Status = "PENDING";
                        }
                        else
                        {
                            rec.Status = "ERROR - NOT FOUND IN CSV";
                        }

                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            _images.Add(rec);
                        }));
                    }
                }

            });
        }

        private void ChooseCsv()
        {
            var selectFolderDialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
            {
                IsFolderPicker = false,
                Multiselect = false,
            };
            selectFolderDialog.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("CSV File", "*.csv"));
            selectFolderDialog.Filters.Add(new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter("All Files", "*.*"));

            var dlgResult = selectFolderDialog.ShowDialog();
            if (dlgResult == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
            {
                InputCsv = selectFolderDialog.FileName;
            }
        }

        private Task UpdateMeta(string filename, ImageInfo imgInfo)
        {
            return Task.Run(() =>
            {
                Logs += $"Processing \"{filename}\"{Environment.NewLine}";

                var tags = imgInfo.Tags.Split(",".ToCharArray()).Select(t => t.Trim());
                var keywords = "";
                foreach (var t in tags)
                {
                    keywords += $"-keywords=\"{t}\" ";
                }

                var procInfo = new ProcessStartInfo
                {
                    FileName = "exiftool.exe",
                    Arguments = $"-title=\"{imgInfo.Title}\" -XPComment={imgInfo.Comments} {keywords} -author=\"{imgInfo.Author}\" -XPSubject=\"{imgInfo.Subject}\" -rating={imgInfo.Rating} -exif:GPSLatitude={imgInfo.Latitude} -exif:GPSLongitude={imgInfo.Longitude} \"{filename}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(procInfo))
                {
                    var message = process.StandardOutput.ReadToEnd();
                    Logs += $"{message}{Environment.NewLine}";
                    process.WaitForExit();
                }
            });
        }

        private async void DoRun()
        {
            var progress = await _dialogCoordinator.ShowProgressAsync(this, "Processing...", "Please wait while I process your images.");
            progress.SetIndeterminate();

            foreach (var imageInfo in _images.Where(i => i.Status.StartsWith("PENDING")))
            {
                try
                {
                    var fullPath = Path.Combine(InputFolder, imageInfo.FileName);
                    await UpdateMeta(fullPath, imageInfo);
                    await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        imageInfo.Status = $"SUCCESS";
                    }));
                }
                catch (Exception ex)
                {
                    await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                     {
                         imageInfo.Status = $"ERROR - {ex.Message}";
                     }));
                }
            }

            await progress.CloseAsync();
        }
    }
}

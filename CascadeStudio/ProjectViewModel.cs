﻿namespace CascadeStudio
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using Gu.Reactive;
    using Gu.State;
    using Gu.Wpf.Reactive;
    using Ookii.Dialogs.Wpf;

    public sealed class ProjectViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly PropertiesSettings ChangeTrackerSettings = new PropertiesSettingsBuilder().IgnoreType<ICommand>()
                                                                                                          .IgnoreProperty<RectangleViewModel>(x => x.SourceRect)
                                                                                                          .CreateSettings();

        private readonly System.Reactive.Disposables.CompositeDisposable disposable;

        private string infoFileName;
        private string vecFileName;
        private string negativesIndexFileName;
        private string rootDirectory;
        private InfoFile infoFile;
        private string runBatFileName;
        private object selectedNode;
        private bool isOpening;
        private bool disposed;

        private ProjectViewModel()
        {
            this.Nodes = new object[]
                         {
                             this.Positives,
                             this.Negatives,
                         };

            this.CreateNewCommand = new RelayCommand(this.OpenOrCreate);
            this.OpenCommand = new RelayCommand(this.OpenOrCreate);

            this.SavePositivesAsSeparateFilesCommand = new RelayCommand(
                this.SavePositivesAsSeparateFiles,
                () => File.Exists(this.infoFileName));

            this.CreateNegIndexCommand = new RelayCommand(
                this.SaveNegativesIndex,
                () => !string.IsNullOrWhiteSpace(this.Negatives.Path) &&
                      Directory.EnumerateFiles(this.Negatives.Path).Any());

            var positivesTracker = Gu.State.Track.Changes(this.Positives, ChangeTrackerSettings);
            var negativesTracker = Gu.State.Track.Changes(this.Negatives, ChangeTrackerSettings);
            this.disposable = new System.Reactive.Disposables.CompositeDisposable()
                              {
                                  positivesTracker,
                                  positivesTracker.ObservePropertyChangedSlim(x => x.Changes)
                                                  .Where(_ => !this.isOpening && !string.IsNullOrEmpty(this.infoFileName))
                                                  .Throttle(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
                                                  .Subscribe(
                                                      _ =>
                                                      {
                                                          File.Delete(this.vecFileName);
                                                          this.SaveInfo();
                                                      }),
                                  negativesTracker,
                                  negativesTracker.ObservePropertyChangedSlim(x => x.Changes)
                                                  .Where(_ => !this.isOpening && !string.IsNullOrEmpty(this.negativesIndexFileName))
                                                  .Throttle(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
                                                  .Subscribe(_ => this.SaveNegativesIndex()),
                              };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static ProjectViewModel Instance { get; } = new ProjectViewModel();

        public ICommand CreateNewCommand { get; }

        public ICommand OpenCommand { get; }

        public ICommand SavePositivesAsSeparateFilesCommand { get; }

        public ICommand CreateNegIndexCommand { get; }

        public IReadOnlyList<object> Nodes { get; }

        public PositivesDirectory Positives { get; } = new PositivesDirectory();

        public NegativesDirectory Negatives { get; } = new NegativesDirectory();

        public string DataDirectory => string.IsNullOrEmpty(this.rootDirectory)
            ? null
            : Path.Combine(this.rootDirectory, "data");

        public InfoFile InfoFile
        {
            get => this.infoFile;

            private set
            {
                if (ReferenceEquals(value, this.infoFile))
                {
                    return;
                }

                this.infoFile = value;
                this.OnPropertyChanged();
            }
        }

        public string RootDirectory
        {
            get => this.rootDirectory;

            set
            {
                if (value == this.rootDirectory)
                {
                    return;
                }

                this.rootDirectory = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.DataDirectory));
            }
        }

        public string InfoFileName
        {
            get => this.infoFileName;

            set
            {
                if (value == this.infoFileName)
                {
                    return;
                }

                this.infoFileName = value;
                this.OnPropertyChanged();
            }
        }

        public string VecFileName
        {
            get => this.vecFileName;

            set
            {
                if (value == this.vecFileName)
                {
                    return;
                }

                this.vecFileName = value;
                this.OnPropertyChanged();
            }
        }

        public string NegativesIndexFileName
        {
            get => this.negativesIndexFileName;

            set
            {
                if (value == this.negativesIndexFileName)
                {
                    return;
                }

                this.negativesIndexFileName = value;
                this.OnPropertyChanged();
            }
        }

        public string RunBatFileName
        {
            get => this.runBatFileName;

            set
            {
                if (value == this.runBatFileName)
                {
                    return;
                }

                this.runBatFileName = value;
                this.OnPropertyChanged();
            }
        }

        public object SelectedNode
        {
            get => this.selectedNode;

            set
            {
                if (ReferenceEquals(value, this.selectedNode))
                {
                    return;
                }

                this.selectedNode = value;
                this.OnPropertyChanged();
                DetectorViewModel.Instance.ImageFile = (value as ImageViewModel)?.FileName;
            }
        }

        public string CascadeFileName => this.DataDirectory == null
            ? null
            : Path.Combine(this.DataDirectory, "cascade.xml");

        public string GetFileNameRelativeToNegIndex(string fileName) => this.GetRelativeFileName(this.negativesIndexFileName, fileName);

        public string GetFileNameRelativeToInfo(string fileName) => this.GetRelativeFileName(this.infoFileName, fileName);

        public string GetRelativeFileName(string fileName, string fileNameToTrim)
        {
            var directoryName = Path.GetDirectoryName(fileName);
            return fileNameToTrim.Replace(directoryName + "\\", string.Empty);
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.disposable.Dispose();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        private void OpenOrCreate()
        {
            this.isOpening = true;
            try
            {
                var dialog = new VistaFolderBrowserDialog();
                if (dialog.ShowDialog(Application.Current.MainWindow) == true)
                {
                    this.Positives.Images.Clear();
                    this.Negatives.Images.Clear();
                    this.RootDirectory = dialog.SelectedPath;
                    this.InfoFileName = Path.Combine(dialog.SelectedPath, "positives.info");
                    this.vecFileName = Path.ChangeExtension(this.infoFileName, ".vec");
                    this.NegativesIndexFileName = Path.Combine(dialog.SelectedPath, "bg.txt");
                    this.RunBatFileName = Path.Combine(dialog.SelectedPath, "run.bat");

                    this.Positives.Path = Path.Combine(dialog.SelectedPath, "Positives");
                    if (!Directory.Exists(this.Positives.Path))
                    {
                        Directory.CreateDirectory(this.Positives.Path);
                    }

                    this.Negatives.Path = Path.Combine(dialog.SelectedPath, "Negatives");
                    if (!Directory.Exists(this.Negatives.Path))
                    {
                        Directory.CreateDirectory(this.Negatives.Path);
                    }
                    else
                    {
                        foreach (var negative in Directory.EnumerateFiles(this.Negatives.Path))
                        {
                            this.Negatives.Images.Add(new ImageViewModel(negative));
                        }

                        this.SaveNegativesIndex();
                    }

                    if (File.Exists(this.infoFileName))
                    {
                        var info = InfoFile.Load(this.infoFileName);
                        this.InfoFile = info;
                        this.Positives.Images.AddRange(info.Lines.Select(x => new PositiveViewModel(Path.Combine(this.rootDirectory, x.ImageFileName), x.Rectangles)));
                        foreach (var positive in Directory.EnumerateFiles(this.Positives.Path)
                                                          .Where(x => this.Positives.Images.All(p => p.FileName != x)))
                        {
                            this.Positives.Images.Add(new PositiveViewModel(positive, new RectangleInfo[0]));
                        }
                    }
                    else
                    {
                        this.InfoFile = null;
                        File.WriteAllText(this.infoFileName, string.Empty);
                        foreach (var positive in Directory.EnumerateFiles(this.Positives.Path))
                        {
                            this.Positives.Images.Add(new PositiveViewModel(positive, new RectangleInfo[0]));
                        }
                    }
                }
            }
            finally
            {
                this.isOpening = false;
            }
        }

        private void SaveInfo()
        {
            File.WriteAllLines(
                this.infoFileName,
                this.Positives.Images.Where(x => x.Rectangles.Any())
                                     .Select(image => $"{this.GetRelativeFileName(this.infoFileName, image.FileName)} {image.Rectangles.Count} {string.Join(" ", image.Rectangles.Select(p => $"{p.Info.X} {p.Info.Y} {p.Info.Width} {p.Info.Height}"))}"));

            // Not optimal here :)
            this.InfoFile = InfoFile.Load(this.infoFileName);
        }

        private void SavePositivesAsSeparateFiles()
        {
            throw new NotImplementedException("Split up files and remove original");
            throw new NotImplementedException("Update and save info file");
            ////var n = 0;
            ////var index = new StringBuilder();

            ////var directoryName = this.Positives.Path + "_SeparateFiles";
            ////Directory.CreateDirectory(directoryName);
            ////var lines = InfoFile.Load(this.infoFileName);
            ////foreach (var line in lines.Lines)
            ////{
            ////    var sourceFileName = line.ImageFileName;
            ////    using (var image = new Bitmap(Path.Combine(this.RootDirectory, sourceFileName)))
            ////    {
            ////        foreach (var rectangleInfo in line.Rectangles)
            ////        {
            ////            using (var target = new Bitmap(rectangleInfo.Width, rectangleInfo.Height))
            ////            {
            ////                using (var graphics = Graphics.FromImage(target))
            ////                {
            ////                    graphics.DrawImage(
            ////                        image,
            ////                        new Rectangles(0, 0, target.Width, target.Width),
            ////                        new Rectangles(rectangleInfo.X, rectangleInfo.Y, rectangleInfo.Width, rectangleInfo.Height),
            ////                        GraphicsUnit.Pixel);
            ////                    var fileName = Path.Combine(directoryName, $"{n}.bmp");
            ////                    index.AppendLine($"{this.GetFileNameRelativeToInfo(fileName)} 1 0 0 24 24");
            ////                    target.Save(fileName, ImageFormat.Bmp);
            ////                    n++;
            ////                }
            ////            }
            ////        }
            ////    }
            ////}

            ////File.WriteAllText(
            ////    Path.Combine(Path.GetDirectoryName(this.infoFileName), Path.GetFileNameWithoutExtension(this.infoFileName) + "_SeparateFiles.info"),
            ////    index.ToString());
        }

        private void SaveNegativesIndex()
        {
            File.WriteAllLines(
                this.negativesIndexFileName,
                Directory.EnumerateFiles(this.Negatives.Path).Select(x => $"{this.GetFileNameRelativeToNegIndex(x)}"));
        }
    }
}

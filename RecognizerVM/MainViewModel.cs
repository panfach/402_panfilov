﻿using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using YOLO;
using DataBase;

namespace RecognizerVM
{
    public interface IUIServices
    {
        bool ChooseDirectory(ref string filename, string dirPath);
        void ConfirmError(string errorText, string errorTitle);
        bool? ConfirmWarning(string text, string title);
    }

    public class MainViewModel : ViewModelBase
    {
        string inputPath = "";
        bool processing;
        float progress;
        CancellationTokenSource tokenSource = new();
        DataBaseManager dbManager = new();
        BufferBlock<IReadOnlyList<YoloResult>> output = new();
        IUIServices svc;
        ClassListView classListView;
        ImageListView imageListView;

        public ClassListView ClassListView => classListView;
        public ImageListView ImageListView => imageListView;
        public DataBaseManager DBManager => dbManager;
        public string ProgressPercentsAmount => $"{(Progress * 100f):F1}%";
        public string InputPath
        {
            get => inputPath;
            set
            {
                inputPath = value;
                RaisePropertyChanged();
            }
        }
        public float Progress
        {
            get => progress;
            set
            {
                progress = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ProgressPercentsAmount));
            }
        }


        public MainViewModel(IUIServices _svc)
        {
            svc = _svc;
            classListView = new(this);
            imageListView = new(this);
        }


        public void ChooseDirectoryHandler()
        {
            string filename = string.Empty;
            string dirPath = System.IO.Path.Combine(@"..\..\..\..\Assets\");
            if (svc.ChooseDirectory(ref filename, dirPath))
            {
                InputPath = filename;
            }
        }

        public void StopHandler()
        {
            tokenSource.Cancel();
        }

        public void ClearHandler()
        {
            dbManager.Clear();
        }

        public void SelectionChangedHandler(string arg)
        {
            if (arg == null) return;

            imageListView.SetSelectedClass(arg.Substring(arg.IndexOf(' ') + 1));
            imageListView.RaiseCollectionChanged();
        }

        public void ExectueHandler()
        {
            if (processing)
            {
                svc.ConfirmError("Обработка уже началась", "");
                return;
            }

            if (inputPath == "")
            {
                svc.ConfirmError("Выберите папку с изображениями для обработки", "Ошибка");
                return;
            }

            _ = DetectObjectsAsync();
        }


        async Task DetectObjectsAsync()
        {
            processing = true;

            var t = Detector.ExecuteAsync(inputPath, tokenSource.Token, output);

            await DetectionProcessingAsync(output);

            processing = false;
        }

        async Task DetectionProcessingAsync(ISourceBlock<IReadOnlyList<YoloResult>> source)
        {
            int imageAmount = Detector.GetImageFilenames(inputPath).Length;
            int i = 0;

            Progress = 0f;
            while (await source.OutputAvailableAsync())
            {
                var data = source.Receive();
                i++;

                foreach (var item in data)
                {
                    await dbManager.AddAsync(new YoloItem(item));
                }

                Progress = (float)i / imageAmount;

                //svc.ConfirmError($"i = {i}.  i / imageAmount = {(i / imageAmount):F1}. Progress = {Progress}", "Debug");
            }
        }
    }
}

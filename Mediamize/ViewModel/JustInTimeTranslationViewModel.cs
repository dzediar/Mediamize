using System.Text;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows.Interop;
using zComp.Core.Helpers;
using zComp.Core.Model;
using zComp.Wpf.ViewModel;
using System.IO;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using System.Reflection;
using System.Windows.Threading;
using zComp.Wpf.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Net;
using CommunityToolkit.Mvvm.DependencyInjection;
using Mediamize.Model;
using Mediamize.ViewModel;

namespace Mediamize.ViewModel
{
    public class JustInTimeTranslationViewModel : CardViewModel
    {
#if DEBUG

        private List<string> cultures;

        public JustInTimeTranslationViewModel()
        {
            Width = 700;
            Height = 500;

            SaveCommand = new RelayCommand(Save);
            ClearCommand = new RelayCommand(Clear);
        }

        protected override void UserRefresh()
        {
            base.UserRefresh();
        }

        public override void RefreshAllCommands()
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        protected override void FirstLoad()
        {
            base.FirstLoad();
        }

        public void Add(string translationCode)
        {
#if !DEBUG
            return;
#endif

            if (cultures == null)
            {
                cultures = Repository.ManagedCultures.Keys.Select(c => c.Name).Distinct().ToList();
            }

            if (!Translations.Any(t => t.Code == translationCode))
            {
                cultures.ForEach(c => Translations.Add(new TranslationLabel()
                {
                    Culture = c,
                    Code = translationCode,
                    Label = null
                }));
            }

            if (!this.IsLoaded && MMApplicationViewModel.Instance.IsConnected)
            {
                ShowModal();
            }

            RefreshAllCommands();
        }

        private uint showModalCounter = 0;

        public void ShowModal()
        {
            uint cpt;

            lock (this)
            {
                cpt = ++showModalCounter;
            }

            Task.Run(() =>
            {
                Thread.Sleep(1000);

                lock (this)
                {
                    if (cpt != showModalCounter)
                    {
                        return;
                    }
                }

                Messaging.ShowModal(this);

                //if (Messaging.ShowModal(this) == true)
                //{
                //    //Save();
                //}
            });
        }

        public void Clear()
        {
            Translations.Clear();
        }

        public void Save()
        {
            if (Translations == null || Translations.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder();
            var cultures = Translations.Select(l => l.Culture).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()
                .ToDictionary(c => c, c => Translations.Where(l => l.Culture == c).ToList());

            if (cultures.Any(c => c.Value.GroupBy(l => l.Code).Any(g => g.Count() > 1)))
            {
                Messaging.ShowDialogBox(DialogType.Error, Labels["DOUBLED_TRANSLATIONS_CODES"]);
                return;
            }

            foreach (var c in cultures)
            {
                var labelsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), @$"..\..\..\Labels\Labels-{c.Key}.txt");

                LabelsDictionary.AppendLabels(labelsPath, c.Value);

                var cultureDict = Repository.ManagedCultures.FirstOrDefault(ld => ld.Key.Name == c.Key);

                if (cultureDict.Key != null)
                {
                    cultureDict.Value.InternalDictionary.Clear();
                    cultureDict.Value.AddLabelsFromFile(@$"D:\Mes Programmes\C#\magiczee\zComp\zComp.Wpf\Labels\Labels-{c.Key}.txt");
                    cultureDict.Value.AddLabelsFromFile(labelsPath);
                }
            }

            Translations.Clear();

            Repository.NotifyManagedCulturesChanged();

            RefreshAllCommands();

            Close();
        }

        private ObservableCollection<TranslationLabel> translations;

        public ObservableCollection<TranslationLabel> Translations
        {
            get
            {
                if (translations == null)
                {
                    Translations = new ObservableCollection<TranslationLabel>();
                }

                return translations;
            }
            private set
            {
                translations = value;
                OnPropertyChanged(nameof(Translations));
            }
        }

        public RelayCommand SaveCommand { get; private set; }

        public RelayCommand ClearCommand { get; private set; }

#endif
    }
}
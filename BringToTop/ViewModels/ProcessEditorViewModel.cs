using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace BringToTop.ViewModels
{
    public class ProcessEditorViewModel : UXViewModel
    {
        private readonly SourceCache<ProcessViewModel, int> _processSourceCache = new SourceCache<ProcessViewModel, int>(p => p.ProcessId);

        public ObservableCollectionExtended<ProcessViewModel> Processes { get; }

        private ProcessViewModel _selectedProcess;
        public ProcessViewModel SelectedProcess
        {
            get => _selectedProcess;
            set => this.RaiseAndSetIfChanged(ref _selectedProcess, value);
        }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public ProcessEditorViewModel()
        {
            Processes = new ObservableCollectionExtended<ProcessViewModel>();

            var cache = _processSourceCache
                .Connect()
                .Sort(SortExpressionComparer<ProcessViewModel>.Ascending(p => p.WindowName))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(Processes)
                .Subscribe();

            RefreshCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await Task.Run(() =>
                {
                    var newProcesses = Process.GetProcesses()
                        .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                        .Where(p => p.Id != Process.GetCurrentProcess().Id)
                        .ToDictionary(p => p.Id);

                    Debug.WriteLine($"Found {newProcesses.Count} processes");

                    _processSourceCache.Edit(updater =>
                    {
                        var newIds = newProcesses.Keys;
                        var oldIds = updater.Keys;

                        var idsToRemove = newIds.Except(oldIds);

                        foreach (var processKeyValue in newProcesses)
                        {
                            var oldProcess = updater.KeyValues
                                .SingleOrDefault(kv => kv.Key == processKeyValue.Key)
                                .Value;

                            if (oldProcess == null)
                            {
                                updater.AddOrUpdate(new ProcessViewModel(processKeyValue.Value));
                            }
                            else
                            {
                                oldProcess.Load(processKeyValue.Value);
                            }
                        }

                        updater.RemoveKeys(idsToRemove);
                    });

                    if (SelectedProcess == null || !Processes.Contains(SelectedProcess))
                    {
                        SelectedProcess = Processes.FirstOrDefault();
                    }
                });
            });

            // Refresh process list every second
            Observable.Merge(
                Observable.Start(() => { }),
                Observable.Interval(new TimeSpan(0, 0, 1))
                    .Select(ticks => Unit.Default))
                .InvokeCommand(RefreshCommand);
        }
    }
}

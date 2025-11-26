using System;
using System.Threading;
using System.Threading.Tasks;
using R3;
using R3Ext;

namespace R3Ext.SampleApp.ViewModels
{
    public class InteractionViewModel : RxObject
    {
        public Interaction<string, bool> ConfirmDelete { get; } = new();

        public RxCommand<string, Unit> DeleteFileCommand { get; }

        public ReactiveProperty<string> LastAction { get; } = new(string.Empty);

        public InteractionViewModel()
        {
            DeleteFileCommand = RxCommand<string, Unit>.CreateFromTask(async (fileName, ct) =>
            {
                var ok = await ConfirmDelete.Handle($"Delete '{fileName}'?").FirstAsync(ct);
                LastAction.Value = ok ? $"Deleted {fileName}" : "Cancelled";
                if (!ok)
                {
                    return Unit.Default;
                }

                await DeleteFileAsync(fileName, ct);
                return Unit.Default;
            });
        }

        private Task DeleteFileAsync(string fileName, CancellationToken ct)
            => Task.Delay(250, ct);
    }
}

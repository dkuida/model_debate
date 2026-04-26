using System.Threading;
using System.Threading.Tasks;

namespace ModelDebate.Manager.Debate.Interface;

public interface IDebateManager
{
    #region Public

    Task RunAsync(SeedMessage seed, CancellationToken ct);

    #endregion
}

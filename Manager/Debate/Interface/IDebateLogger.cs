using System.Threading;
using System.Threading.Tasks;

namespace ModelDebate.Manager.Debate.Interface;

public interface IDebateLogger
{
    #region Public

    Task LogAsync(IDebateMessage message, CancellationToken ct);

    #endregion
}

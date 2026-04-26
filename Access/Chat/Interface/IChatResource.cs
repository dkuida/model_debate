using System.Threading;
using System.Threading.Tasks;

namespace ModelDebate.Access.Chat.Interface;

public interface IChatResource
{
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
}

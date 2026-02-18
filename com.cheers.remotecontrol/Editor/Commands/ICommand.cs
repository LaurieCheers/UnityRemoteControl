using System.Threading.Tasks;

namespace Unity.RemoteControl.Editor.Commands
{
    public interface ICommand
    {
        string Name { get; }
        Task<Response> ExecuteAsync(Request request);
    }
}

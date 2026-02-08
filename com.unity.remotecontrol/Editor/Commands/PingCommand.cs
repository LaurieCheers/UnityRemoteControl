using System.Threading.Tasks;

namespace Unity.RemoteControl.Editor.Commands
{
    public class PingCommand : ICommand
    {
        public string Name => "ping";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var result = await MainThreadDispatcher.EnqueueAsync(() => new PingResponse
            {
                message = "pong",
                unityVersion = UnityEngine.Application.unityVersion,
                projectName = UnityEngine.Application.productName
            });

            return Response.Success(request.id, result);
        }

        [System.Serializable]
        private class PingResponse
        {
            public string message;
            public string unityVersion;
            public string projectName;
        }
    }
}

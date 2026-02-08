using System.Threading.Tasks;
using UnityEditor;

namespace Unity.RemoteControl.Editor.Commands
{
    public class DeletePrefabCommand : ICommand
    {
        public string Name => "delete_prefab";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var path = request.GetParam<string>("path", null);

            if (string.IsNullOrEmpty(path))
                return Response.Error(request.id, "Missing required parameter: path");

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                if (!AssetDatabase.DeleteAsset(path))
                    return $"Failed to delete prefab: {path}";

                return null;
            });

            if (result != null)
                return Response.Error(request.id, result);

            return Response.Success(request.id, new { deleted = true, path });
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;

namespace Unity.RemoteControl.Editor.Commands
{
    public class ListPrefabsCommand : ICommand
    {
        public string Name => "list_prefabs";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var folder = request.GetParam<string>("folder", null);

            var prefabs = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var searchFolder = string.IsNullOrEmpty(folder) ? "Assets" : folder;
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });

                var result = new List<PrefabInfo>();
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);

                    result.Add(new PrefabInfo
                    {
                        name = name,
                        path = path,
                        guid = guid
                    });
                }

                return result;
            });

            return Response.Success(request.id, prefabs);
        }
    }
}

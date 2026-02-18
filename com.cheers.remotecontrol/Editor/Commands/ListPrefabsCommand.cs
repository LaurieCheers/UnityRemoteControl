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
            var offset = request.GetParam<int>("offset", 0);
            var limit = request.GetParam<int>("limit", 100);

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var searchFolder = string.IsNullOrEmpty(folder) ? "Assets" : folder;
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });

                var total = guids.Length;
                var prefabs = new List<PrefabInfo>();

                var start = System.Math.Min(offset, total);
                var end = System.Math.Min(start + limit, total);

                for (int i = start; i < end; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);

                    prefabs.Add(new PrefabInfo
                    {
                        name = name,
                        path = path,
                        guid = guids[i]
                    });
                }

                return new PrefabListResult
                {
                    total = total,
                    offset = start,
                    limit = limit,
                    prefabs = prefabs
                };
            });

            return Response.Success(request.id, result);
        }
    }
}

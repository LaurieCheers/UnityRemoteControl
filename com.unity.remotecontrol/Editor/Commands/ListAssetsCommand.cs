using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;

namespace Unity.RemoteControl.Editor.Commands
{
    public class ListAssetsCommand : ICommand
    {
        public string Name => "list_assets";

        public async Task<Response> ExecuteAsync(Request request)
        {
            var folder = request.GetParam<string>("folder", null);
            var typeFilter = request.GetParam<string>("type", null);
            var offset = request.GetParam<int>("offset", 0);
            var limit = request.GetParam<int>("limit", 100);

            var result = await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var searchFolder = string.IsNullOrEmpty(folder) ? "Assets" : folder;
                var filter = string.IsNullOrEmpty(typeFilter) ? "" : $"t:{typeFilter}";
                var guids = AssetDatabase.FindAssets(filter, new[] { searchFolder });

                var total = guids.Length;
                var assets = new List<AssetInfo>();

                var start = System.Math.Min(offset, total);
                var end = System.Math.Min(start + limit, total);

                for (int i = start; i < end; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                    assets.Add(new AssetInfo
                    {
                        name = name,
                        path = path,
                        guid = guids[i],
                        type = asset != null ? asset.GetType().Name : "Unknown"
                    });
                }

                return new AssetListResult
                {
                    total = total,
                    offset = start,
                    limit = limit,
                    assets = assets
                };
            });

            return Response.Success(request.id, result);
        }
    }
}

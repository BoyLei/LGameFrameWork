using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LGameFramework.Resource
{
    /// <summary>Unity 主线程 API。每次 Load/Instantiate 成功后，调用方拥有一个需要 Dispose 的句柄。</summary>
    public interface IResourceService : IDisposable
    {
        Task InitializeAsync();
        Task<ResourceHandle<T>> LoadAsync<T>(string address) where T : UnityEngine.Object;
        Task<ResourceHandle<GameObject>> InstantiateAsync(string address, Transform parent = null);
        Task<long> GetDownloadSizeAsync(string addressOrLabel);
        Task DownloadDependenciesAsync(string addressOrLabel);
    }
}
